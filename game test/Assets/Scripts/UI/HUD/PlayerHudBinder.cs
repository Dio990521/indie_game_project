using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.Hud
{
    /// <summary>
    /// 玩家 HUD 绑定器（Binder）：
    /// 严格遵循项目 Binder 约束，只负责在 Inspector 中收集引用并对外提供只读访问。
    ///
    /// 约束说明：
    /// 1) 不写任何业务逻辑；
    /// 2) 不订阅事件；
    /// 3) 不做数值计算；
    /// 4) 仅作为 View/Controller 的“引用容器”。
    /// </summary>
    public class PlayerHudBinder : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject rootNode;

        [Header("HP")]
        [SerializeField] private Image hpFillImage;
        [SerializeField] private TMP_Text hpValueText;

        [Header("EXP")]
        [SerializeField] private Image expFillImage;
        [SerializeField] private TMP_Text expValueText;

        public GameObject RootNode => rootNode;
        public Image HpFillImage => hpFillImage;
        public TMP_Text HpValueText => hpValueText;
        public Image ExpFillImage => expFillImage;
        public TMP_Text ExpValueText => expValueText;
    }
}
