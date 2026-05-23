using System.Collections;
using UnityEngine;
using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.Data;
using IndieGame.Gameplay.Town;
using IndieGame.UI.Camp;

namespace IndieGame.UI.Town
{
    /// <summary>
    /// 城镇 UI 视图（View）：
    /// <para>
    /// 仅负责"如何显示"：全屏覆盖菜单、6 个功能按钮、淡入淡出、背景图切换。
    /// 所有跨系统业务（行动点 / 日期 / 自动存档 / 传送 / 相机 / 移动）已迁移到 <see cref="TownUIController"/>。
    /// </para>
    /// <para>
    /// 与 Controller 的协作约定：
    /// - 当前城镇上下文（nodeId、TownTile）由 TownState.OnEnter 通过 Configure() 注入，
    ///   Controller 通过 <see cref="CurrentNodeId"/> / <see cref="CurrentTownTile"/> 读取；
    /// - Controller 通过 <see cref="CanvasGroup"/> 直接控制淡入淡出，避免 View 暴露过多动画 API；
    /// - Controller 通过 <see cref="RebuildMenuForOverlay"/> 在黑屏期间重建按钮（不触发淡入协程）。
    /// </para>
    /// </summary>
    public class TownUIView : View
    {
        [Header("Binder")]
        [SerializeField] private TownUIBinder binder;

        [Header("商店配置")]
        [SerializeField] private string _materialShopID = "town_material_shop";
        [SerializeField] private string _itemShopID     = "town_item_shop";

        // CanvasGroup 控制淡入淡出
        private CanvasGroup _canvasGroup;

        // 当前城镇数据（由 TownState.OnEnter → Configure() 注入；传送完成后也会刷新）。
        private int _currentNodeId = -1;
        private TownTile _currentTownTile;

        // 硬编码按钮配置：(功能ID, 显示名称)
        private static readonly (TownActionID Id, string Label)[] ButtonDefs =
        {
            (TownActionID.MaterialShop, "素材店"),
            (TownActionID.ItemShop,     "道具店"),
            (TownActionID.Tavern,       "酒馆"),
            (TownActionID.Inn,          "旅馆"),
            (TownActionID.Teleport,     "传送"),
            (TownActionID.Leave,        "离开"),
        };

        // 当前已生成按钮对应的功能 ID 列表（用于索引映射）
        private readonly TownActionID[] _activeIds = new TownActionID[ButtonDefs.Length];

        // ===== 对 Controller 暴露的访问点（只读） =====

        /// <summary> 当前 CanvasGroup（供 Controller 直接控制 alpha/blocksRaycasts/interactable 与 DoTween 动画）。 </summary>
        public CanvasGroup CanvasGroup => _canvasGroup;

        /// <summary> 当前所在城镇节点 ID（由 TownState.OnEnter 或传送完成时注入）。 </summary>
        public int CurrentNodeId => _currentNodeId;

        /// <summary> 当前所在城镇配置数据（同上）。 </summary>
        public TownTile CurrentTownTile => _currentTownTile;

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[TownUIView] 缺失 binder 引用。");
                return;
            }

            _canvasGroup = binder.CanvasGroup != null
                ? binder.CanvasGroup
                : GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 初始隐藏
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TownActionButtonClickEvent>(HandleButtonClick);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TownActionButtonClickEvent>(HandleButtonClick);
        }

        /// <summary>
        /// 配置当前城镇数据。
        /// <para>调用时机：</para>
        /// <para>1) TownState.OnEnter 注入初始数据；</para>
        /// <para>2) TownUIController 在传送黑屏期间热切换背景图，无需退出重进 TownState。</para>
        /// </summary>
        public void Configure(int nodeId, TownTile townTile)
        {
            _currentNodeId   = nodeId;
            _currentTownTile = townTile;

            if (binder != null && binder.BgImage != null)
            {
                Sprite bg = townTile != null ? townTile.townBackground : null;
                binder.BgImage.sprite  = bg;
                binder.BgImage.enabled = bg != null;
            }
        }

        /// <summary>
        /// 显示城镇菜单：生成按钮并淡入。
        /// </summary>
        public override void Show()
        {
            InitializeButtons();
            StopAllCoroutines();
            StartCoroutine(FadeInRoutine());
        }

        /// <summary>
        /// 隐藏城镇菜单。
        /// </summary>
        public override void Hide()
        {
            if (_canvasGroup == null) return;
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        /// <summary>
        /// 黑屏覆盖期间的菜单重建：
        /// 仅重生成按钮，不重复触发 Show() 的淡入协程（避免 StopAllCoroutines 中断
        /// Controller 正在执行的旅馆/传送协程）。
        /// </summary>
        public void RebuildMenuForOverlay()
        {
            InitializeButtons();
        }

        private IEnumerator FadeInRoutine()
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.25f);
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>
        /// 动态生成按钮：清除旧按钮，按 ButtonDefs 顺序实例化。
        /// </summary>
        private void InitializeButtons()
        {
            if (binder == null || binder.ButtonContainer == null || binder.ButtonPrefab == null)
            {
                DebugTools.LogError("[TownUIView] binder 配置不完整，无法初始化按钮。");
                return;
            }

            // 清除旧按钮
            for (int i = binder.ButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(binder.ButtonContainer.GetChild(i).gameObject);

            // 生成新按钮
            for (int i = 0; i < ButtonDefs.Length; i++)
            {
                _activeIds[i] = ButtonDefs[i].Id;
                TownActionButton btn = Instantiate(binder.ButtonPrefab, binder.ButtonContainer);
                btn.Setup(ButtonDefs[i].Label, null, i);
            }
        }

        /// <summary>
        /// 隐藏城镇菜单并发送打开商店请求。
        /// </summary>
        private void OpenShop(string shopId)
        {
            if (string.IsNullOrWhiteSpace(shopId))
            {
                DebugTools.LogWarning("[TownUIView] 商店 ID 未配置。");
                return;
            }
            Hide();
            EventBus.Raise(new OpenShopUIRequestEvent { ShopID = shopId });
        }

        /// <summary>
        /// 按钮点击处理：把每个按钮按 ActionID 转发为对应事件，让 Controller / 系统接管业务。
        /// View 自身不再编排跨系统流程。
        /// </summary>
        private void HandleButtonClick(TownActionButtonClickEvent evt)
        {
            if (evt.Index < 0 || evt.Index >= _activeIds.Length) return;

            TownActionID id = _activeIds[evt.Index];

            switch (id)
            {
                case TownActionID.MaterialShop:
                    OpenShop(_materialShopID);
                    break;
                case TownActionID.ItemShop:
                    OpenShop(_itemShopID);
                    break;
                case TownActionID.Tavern:
                    DebugTools.Log("[城镇] 酒馆 —— 功能待实现");
                    break;
                case TownActionID.Inn:
                    // 转发为业务事件：由 TownUIController 接管编排
                    // （黑屏 / 行动点 / 日期 / 自动存档 / 重显菜单）。
                    EventBus.Raise(new InnSleepRequestedEvent());
                    break;
                case TownActionID.Teleport:
                    // 转发为业务事件：由 TownUIController 接管编排
                    // （选单 / 黑屏 / 移动 / 相机 / 切背景）。
                    EventBus.Raise(new TownTeleportRequestedEvent());
                    break;
                case TownActionID.Leave:
                    // 通知 TownState 退出城镇，回到玩家回合。
                    EventBus.Raise(new TownLeaveRequestedEvent());
                    break;
            }
        }
    }
}
