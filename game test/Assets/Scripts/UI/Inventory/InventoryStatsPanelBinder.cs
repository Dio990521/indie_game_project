using TMPro;
using UnityEngine;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 背包界面左侧角色属性面板的 UI 引用容器。
    /// 严格遵守 Binder 规范：仅暴露引用，不写任何逻辑。
    /// </summary>
    public class InventoryStatsPanelBinder : MonoBehaviour
    {
        [Header("HP / EXP / AP")]
        [SerializeField] private TMP_Text hpValueText;
        [SerializeField] private TMP_Text expValueText;
        [SerializeField] private TMP_Text apValueText;

        [Header("Stats")]
        [SerializeField] private TMP_Text attackValueText;
        [SerializeField] private TMP_Text defenseValueText;
        [SerializeField] private TMP_Text resistanceValueText;
        [SerializeField] private TMP_Text moveSpeedValueText;
        [SerializeField] private TMP_Text luckValueText;

        public TMP_Text HpValueText => hpValueText;
        public TMP_Text ExpValueText => expValueText;
        public TMP_Text ApValueText => apValueText;

        public TMP_Text AttackValueText => attackValueText;
        public TMP_Text DefenseValueText => defenseValueText;
        public TMP_Text ResistanceValueText => resistanceValueText;
        public TMP_Text MoveSpeedValueText => moveSpeedValueText;
        public TMP_Text LuckValueText => luckValueText;
    }
}
