using IndieGame.Core;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Stats;
using UnityEngine;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 背包界面左侧角色属性面板的控制器：
    /// 监听 HP / EXP / 等级 / 行动点 / 武器装备变更事件，刷新 View。
    /// 与 PlayerHudController 的设计保持一致（事件驱动 + 懒绑定玩家引用）。
    /// </summary>
    public class InventoryStatsPanelController : EventBusMonoBehaviour
    {
        [Header("View")]
        [SerializeField] private InventoryStatsPanelView view;

        // 玩家引用，用于过滤 HP / EXP 事件，以及在等级变化、装备变化时重新读取基础属性
        private GameObject _currentPlayer;
        private CharacterStats _currentPlayerStats;

        private void Awake()
        {
            if (view == null)
                view = GetComponent<InventoryStatsPanelView>();
        }

        protected override void Bind()
        {
            Subscribe<HealthChangedEvent>(HandleHealthChanged);
            Subscribe<ExpChangedEvent>(HandleExpChanged);
            Subscribe<LevelChangedEvent>(HandleLevelChanged);
            Subscribe<ActionPointChangedEvent>(HandleActionPointChanged);
            Subscribe<WeaponEquippedEvent>(HandleWeaponChanged);
            Subscribe<WeaponUnequippedEvent>(HandleWeaponChanged);
            Subscribe<ArmorEquippedEvent>(HandleArmorChanged);
            Subscribe<ArmorUnequippedEvent>(HandleArmorChanged);
            Subscribe<InventoryOpenedEvent>(HandleInventoryOpened);
            // 面板在装备界面复用同一份预制体实例时，靠这个事件驱动初次刷新（背包走 InventoryOpenedEvent）
            Subscribe<EquipmentUIOpenedEvent>(HandleInventoryOpened);
        }

        private void Update()
        {
            // 玩家对象可能在本组件 Awake 之后才创建，持续尝试绑定直到成功
            if (_currentPlayerStats != null) return;
            if (TryBindPlayer()) RefreshAll();
        }

        // ─── 事件处理 ───────────────────────────────────────

        private void HandleHealthChanged(HealthChangedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            view?.RefreshHealth(evt.Current, evt.Max);
        }

        private void HandleExpChanged(ExpChangedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            view?.RefreshExp(evt.Current, evt.Required);
        }

        private void HandleLevelChanged(LevelChangedEvent evt)
        {
            // 升级会重算基础属性（攻击/防御/抗性/移速/幸运），需要重新读取
            if (!IsCurrentPlayer(evt.Owner)) return;
            RefreshStatValues();
        }

        private void HandleActionPointChanged(ActionPointChangedEvent evt)
        {
            view?.RefreshActionPoints(evt.CurrentPoints, evt.MaxPoints);
        }

        private void HandleWeaponChanged(WeaponEquippedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            RefreshStatValues();
        }

        private void HandleWeaponChanged(WeaponUnequippedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            RefreshStatValues();
        }

        private void HandleArmorChanged(ArmorEquippedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            RefreshStatValues();
        }

        private void HandleArmorChanged(ArmorUnequippedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            RefreshStatValues();
        }

        private void HandleInventoryOpened(InventoryOpenedEvent evt)
        {
            RefreshAll();
        }

        private void HandleInventoryOpened(EquipmentUIOpenedEvent evt)
        {
            RefreshAll();
        }

        // ─── 数据刷新 ────────────────────────────────────────

        private void RefreshAll()
        {
            if (!TryBindPlayer()) return;

            view?.RefreshHealth(_currentPlayerStats.CurrentHP, _currentPlayerStats.MaxHP);
            view?.RefreshExp(_currentPlayerStats.CurrentEXP, _currentPlayerStats.CurrentRequiredEXP);
            RefreshStatValues();

            ActionPointSystem apSystem = ActionPointSystem.Instance;
            if (apSystem != null)
                view?.RefreshActionPoints(apSystem.CurrentActionPoints, apSystem.MaxActionPoints);
        }

        private void RefreshStatValues()
        {
            if (!TryBindPlayer()) return;
            view?.RefreshAttack(_currentPlayerStats.Attack.Value);
            view?.RefreshDefense(_currentPlayerStats.Defense.Value);
            view?.RefreshResistance(_currentPlayerStats.Resistance.Value);
            view?.RefreshMoveSpeed(_currentPlayerStats.MoveSpeed.Value);
            view?.RefreshLuck(_currentPlayerStats.Luck.Value);
        }

        // ─── 工具 ────────────────────────────────────────────

        private bool TryBindPlayer()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.CurrentPlayer == null) return false;
            if (_currentPlayer == gm.CurrentPlayer && _currentPlayerStats != null) return true;

            _currentPlayer = gm.CurrentPlayer;
            _currentPlayerStats = _currentPlayer.GetComponent<CharacterStats>();
            return _currentPlayerStats != null;
        }

        private bool IsCurrentPlayer(GameObject owner)
        {
            if (owner == null) return false;
            if (_currentPlayer == null) TryBindPlayer();
            return owner == _currentPlayer;
        }
    }
}
