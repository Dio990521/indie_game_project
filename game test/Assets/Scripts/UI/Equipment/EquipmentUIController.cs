using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Equipment;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Stats;

namespace IndieGame.UI.Equipment
{
    /// <summary>
    /// 装备界面控制器：
    /// 负责 Tab 过滤、选中状态、详情面板数据格式化、装备/卸下操作；具体控件赋值全部转交 View。
    /// 物品格复用背包的 InventorySlotUI；数值面板复用 InventoryStatsPanel 三件套（不在本类范围内，
    /// 由 EquipmentUIOpenedEvent 驱动其独立刷新）。
    /// </summary>
    public class EquipmentUIController : EventBusMonoBehaviour
    {
        [SerializeField] private EquipmentUIBinder binder;
        [SerializeField] private EquipmentUIView view;
        // 网格最小展示格数：装备类物品数量远少于背包全部物品，池子按需扩容，此值只影响“至少铺多少个空格”
        [SerializeField] private int slotMinCount = 12;

        // ── 状态 ────────────────────────────────────────────────────────
        private EquipmentType _currentTab = EquipmentType.Weapon;
        private InventorySlot _selectedSlot;
        private IReadOnlyList<InventorySlot> _cachedSlots;
        private readonly List<InventorySlot> _filteredSlots = new List<InventorySlot>();
        private bool _isVisible;

        // 玩家身上的装备控制器（懒解析，随玩家对象变化重新解析）
        private GameObject _playerOwner;
        private WeaponEquipController _playerWeaponEquip;
        private ArmorEquipController _playerArmorEquip;

        // ── 生命周期 ─────────────────────────────────────────────────────

        private void Awake()
        {
            if (view == null) view = GetComponent<EquipmentUIView>();
            if (binder == null) DebugTools.LogError("[EquipmentUIController] Missing binder reference.");

            view?.SetVisible(false);
            BindButtons();
        }

        protected override void Bind()
        {
            Subscribe<OpenEquipmentUIEvent>(HandleOpenEquipmentUI);
            Subscribe<CloseEquipmentUIEvent>(HandleCloseEquipmentUI);
            Subscribe<OnInventoryChanged>(HandleInventoryChanged);
            Subscribe<WeaponEquippedEvent>(HandleWeaponEquipChanged);
            Subscribe<WeaponUnequippedEvent>(HandleWeaponUnequipChanged);
            Subscribe<ArmorEquippedEvent>(HandleArmorEquipChanged);
            Subscribe<ArmorUnequippedEvent>(HandleArmorUnequipChanged);
        }

        // ── 按钮绑定 ─────────────────────────────────────────────────────

        private void BindButtons()
        {
            if (binder == null) return;

            Button[] tabs = binder.CategoryTabButtons;
            if (tabs != null)
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    int tabIndex = i; // 闭包捕获
                    tabs[i]?.onClick.AddListener(() => SwitchTab((EquipmentType)tabIndex));
                }
            }

            binder.EquipButton?.onClick.AddListener(HandleEquipButtonClicked);
            binder.CloseButton?.onClick.AddListener(CloseAndNotify);
        }

        private void CloseAndNotify() => EventBus.Raise(new CloseEquipmentUIEvent());

        // ── 打开 / 关闭 ──────────────────────────────────────────────────

        private void HandleOpenEquipmentUI(OpenEquipmentUIEvent evt)
        {
            _isVisible = true;
            transform.SetAsLastSibling();
            view?.SetVisible(true);
            RebuildGrid();
            RefreshEquippedSlots();
            EventBus.Raise(new EquipmentUIOpenedEvent());
        }

        private void HandleCloseEquipmentUI(CloseEquipmentUIEvent evt)
        {
            _isVisible = false;
            view?.SetVisible(false);
            EventBus.Raise(new EquipmentUIClosedEvent());
        }

        // ── 背包数据变更 ─────────────────────────────────────────────────

        private void HandleInventoryChanged(OnInventoryChanged evt)
        {
            _cachedSlots = evt.Slots;
            if (_isVisible) RebuildGrid();
        }

        // ── Tab 切换 ─────────────────────────────────────────────────────

        private void SwitchTab(EquipmentType tab)
        {
            if (_currentTab == tab) return;
            _currentTab = tab;
            view?.SetTabHighlight((int)tab);

            _selectedSlot = null;
            view?.ClearDetail();
            RefreshEquipButton();

            RebuildGrid();
            view?.ScrollGridToTop();
        }

        // ── 网格重建 ─────────────────────────────────────────────────────

        private void RebuildGrid()
        {
            _filteredSlots.Clear();
            if (_cachedSlots != null)
            {
                for (int i = 0; i < _cachedSlots.Count; i++)
                {
                    InventorySlot slot = _cachedSlots[i];
                    if (slot?.Item == null) continue;
                    if (!SlotMatchesTab(slot)) continue;
                    _filteredSlots.Add(slot);
                }
            }

            view?.DisplayGrid(_filteredSlots, slotMinCount, _selectedSlot, OnSlotClicked);

            if (_selectedSlot != null && !IsSelectedSlotStillValid())
            {
                _selectedSlot = null;
                view?.ClearDetail();
                RefreshEquipButton();
            }
            else if (_selectedSlot != null)
            {
                RefreshDetailPanel();
            }
        }

        private bool SlotMatchesTab(InventorySlot slot)
        {
            return slot.Item is EquipmentItemSO equipment && equipment.SlotType == _currentTab;
        }

        private bool IsSelectedSlotStillValid()
        {
            if (_cachedSlots == null || _selectedSlot == null) return false;
            for (int i = 0; i < _cachedSlots.Count; i++)
            {
                if (_cachedSlots[i] == _selectedSlot) return true;
            }
            return false;
        }

        // ── 选中 & 详情面板 ──────────────────────────────────────────────

        private void OnSlotClicked(InventorySlot slot)
        {
            _selectedSlot = slot;
            view?.SetSelectionHighlight(slot);
            RefreshDetailPanel();
        }

        private void RefreshDetailPanel()
        {
            if (_selectedSlot?.Item is not EquipmentItemSO item)
            {
                view?.ClearDetail();
                RefreshEquipButton();
                return;
            }

            string name = !string.IsNullOrWhiteSpace(_selectedSlot.CustomName)
                ? _selectedSlot.CustomName
                : item.GetLocalizedName();

            view?.SetDetail(
                item.Icon,
                name,
                SlotTypeToDisplayName(item.SlotType),
                ItemRarityUtility.GetColor(item.Rarity),
                ItemRarityUtility.GetDisplayName(item.Rarity),
                item.Description,
                BuildModifierLines(item.Modifiers));

            RefreshEquipButton();
        }

        private static List<string> BuildModifierLines(List<StatModifierData> modifiers)
        {
            List<string> lines = new List<string>(modifiers?.Count ?? 0);
            if (modifiers == null) return lines;

            for (int i = 0; i < modifiers.Count; i++)
            {
                string sign = modifiers[i].Value >= 0 ? "+" : string.Empty;
                lines.Add($"{StatTypeDisplayUtility.GetDisplayName(modifiers[i].Type)} {sign}{modifiers[i].Value:0.##}");
            }
            return lines;
        }

        private static string SlotTypeToDisplayName(EquipmentType type)
        {
            return type switch
            {
                EquipmentType.Weapon => "武器",
                EquipmentType.Armor  => "防具",
                EquipmentType.Recipe => "配方",
                _                    => "未知"
            };
        }

        // ── 装备 / 卸下 ──────────────────────────────────────────────────

        private void RefreshEquipButton()
        {
            if (_selectedSlot?.Item is not EquipmentItemSO item)
            {
                view?.SetEquipButton("装备", false);
                return;
            }

            // 配方部位尚未实现任何 Controller，即使将来出现配方物品也暂不可装备
            if (item.SlotType == EquipmentType.Recipe)
            {
                view?.SetEquipButton("装备", false);
                return;
            }

            bool isEquipped = IsSlotEquipped(_selectedSlot, item.SlotType);
            view?.SetEquipButton(isEquipped ? "卸下" : "装备", true);
        }

        private bool IsSlotEquipped(InventorySlot slot, EquipmentType type)
        {
            TryBindPlayer();
            return type switch
            {
                EquipmentType.Weapon => _playerWeaponEquip != null && _playerWeaponEquip.CurrentWeaponSlot == slot,
                EquipmentType.Armor  => _playerArmorEquip != null && _playerArmorEquip.CurrentArmorSlot == slot,
                _                    => false
            };
        }

        private void HandleEquipButtonClicked()
        {
            if (_selectedSlot?.Item is not EquipmentItemSO item) return;
            if (!TryBindPlayer()) return;

            switch (item.SlotType)
            {
                case EquipmentType.Weapon when _playerWeaponEquip != null:
                    if (_playerWeaponEquip.CurrentWeaponSlot == _selectedSlot) _playerWeaponEquip.Unequip();
                    else _playerWeaponEquip.Equip(_selectedSlot);
                    break;
                case EquipmentType.Armor when _playerArmorEquip != null:
                    if (_playerArmorEquip.CurrentArmorSlot == _selectedSlot) _playerArmorEquip.Unequip();
                    else _playerArmorEquip.Equip(_selectedSlot);
                    break;
                default:
                    // 配方部位尚未实现，或玩家身上缺少对应的 EquipController 组件
                    break;
            }
        }

        // ── 已装备槽位联动 ───────────────────────────────────────────────

        private void HandleWeaponEquipChanged(WeaponEquippedEvent evt) => HandleWeaponEquipStateChanged(evt.Owner);
        private void HandleWeaponUnequipChanged(WeaponUnequippedEvent evt) => HandleWeaponEquipStateChanged(evt.Owner);
        private void HandleArmorEquipChanged(ArmorEquippedEvent evt) => HandleArmorEquipStateChanged(evt.Owner);
        private void HandleArmorUnequipChanged(ArmorUnequippedEvent evt) => HandleArmorEquipStateChanged(evt.Owner);

        private void HandleWeaponEquipStateChanged(GameObject owner)
        {
            if (!IsCurrentPlayer(owner)) return;
            RefreshEquippedSlots();
            RefreshEquipButton();
        }

        private void HandleArmorEquipStateChanged(GameObject owner)
        {
            if (!IsCurrentPlayer(owner)) return;
            RefreshEquippedSlots();
            RefreshEquipButton();
        }

        private void RefreshEquippedSlots()
        {
            TryBindPlayer();
            view?.SetEquippedWeapon(_playerWeaponEquip?.CurrentWeapon, HandleUnequipWeaponClicked);
            view?.SetEquippedArmor(_playerArmorEquip?.CurrentArmor, HandleUnequipArmorClicked);
            view?.ClearEquippedRecipeSlots();
        }

        private void HandleUnequipWeaponClicked() => _playerWeaponEquip?.Unequip();
        private void HandleUnequipArmorClicked() => _playerArmorEquip?.Unequip();

        // ── 玩家引用解析 ─────────────────────────────────────────────────

        private bool TryBindPlayer()
        {
            GameObject player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            if (player == null) return false;
            if (_playerOwner == player) return true;

            _playerOwner = player;
            _playerWeaponEquip = player.GetComponent<WeaponEquipController>();
            _playerArmorEquip = player.GetComponent<ArmorEquipController>();
            return true;
        }

        private bool IsCurrentPlayer(GameObject owner)
        {
            if (owner == null) return false;
            TryBindPlayer();
            return owner == _playerOwner;
        }
    }
}
