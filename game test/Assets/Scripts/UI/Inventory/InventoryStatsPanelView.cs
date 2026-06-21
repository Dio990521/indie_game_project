using UnityEngine;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 背包界面左侧角色属性面板的视图层（View）：
    /// 只负责"如何显示"，不订阅 EventBus，不关心数据来源。
    /// </summary>
    public class InventoryStatsPanelView : MonoBehaviour
    {
        [Header("Binder")]
        [SerializeField] private InventoryStatsPanelBinder binder;

        /// <summary> 刷新生命值显示，格式为 "current / max"。 </summary>
        public void RefreshHealth(int current, int max)
        {
            if (binder == null || binder.HpValueText == null) return;
            binder.HpValueText.text = $"{Mathf.Max(0, current)} / {Mathf.Max(1, max)}";
        }

        /// <summary> 刷新经验值显示，格式为 "current / required"。 </summary>
        public void RefreshExp(int current, int required)
        {
            if (binder == null || binder.ExpValueText == null) return;
            binder.ExpValueText.text = $"{Mathf.Max(0, current)} / {Mathf.Max(1, required)}";
        }

        /// <summary> 刷新行动点显示，格式为 "current / max"。 </summary>
        public void RefreshActionPoints(int current, int max)
        {
            if (binder == null || binder.ApValueText == null) return;
            binder.ApValueText.text = $"{Mathf.Max(0, current)} / {Mathf.Max(0, max)}";
        }

        /// <summary> 刷新攻击力数值（四舍五入为整数显示）。 </summary>
        public void RefreshAttack(float value) => SetStatText(binder?.AttackValueText, value);

        /// <summary> 刷新防御力数值。 </summary>
        public void RefreshDefense(float value) => SetStatText(binder?.DefenseValueText, value);

        /// <summary> 刷新抗性数值。 </summary>
        public void RefreshResistance(float value) => SetStatText(binder?.ResistanceValueText, value);

        /// <summary> 刷新移动速度数值。 </summary>
        public void RefreshMoveSpeed(float value) => SetStatText(binder?.MoveSpeedValueText, value);

        /// <summary> 刷新幸运值数值。 </summary>
        public void RefreshLuck(float value) => SetStatText(binder?.LuckValueText, value);

        private static void SetStatText(TMPro.TMP_Text label, float value)
        {
            if (label == null) return;
            // 成长曲线可能产生小数，统一四舍五入为整数显示，保持与 HP/EXP 一致的风格
            label.text = Mathf.RoundToInt(value).ToString();
        }
    }
}
