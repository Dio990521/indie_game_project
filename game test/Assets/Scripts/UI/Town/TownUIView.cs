using System.Collections;
using UnityEngine;
using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Town;
using IndieGame.UI.Camp;

namespace IndieGame.UI.Town
{
    /// <summary>
    /// 城镇 UI 视图：
    /// 全屏覆盖的城镇菜单，包含 6 个功能按钮。
    /// 当前仅【离开】按钮有实际功能，其余按钮输出 Log 占位。
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
        private readonly TownActionID[] _activeIds = new TownActionID[6];

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
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
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
        /// 按钮点击处理：通过索引映射到功能 ID，分派对应逻辑。
        /// </summary>
        private void HandleButtonClick(TownActionButtonClickEvent evt)
        {
            if (evt.Index < 0 || evt.Index >= ButtonDefs.Length) return;

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
                    DebugTools.Log("[城镇] 旅馆 —— 功能待实现");
                    break;
                case TownActionID.Teleport:
                    DebugTools.Log("[城镇] 传送 —— 功能待实现");
                    break;
                case TownActionID.Leave:
                    // 通知 TownState 退出城镇，回到玩家回合
                    EventBus.Raise(new TownLeaveRequestedEvent());
                    break;
            }
        }
    }
}
