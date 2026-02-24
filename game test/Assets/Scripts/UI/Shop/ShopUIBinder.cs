using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.Shop
{
    /// <summary>
    /// 商店 UI 绑定器（Binder）：
    /// 严格只做引用容器，不写任何业务逻辑。
    ///
    /// 约束说明：
    /// 1) 不订阅事件；
    /// 2) 不处理输入；
    /// 3) 不做数据计算；
    /// 4) 仅通过只读属性向 Controller 暴露组件引用。
    /// </summary>
    public class ShopUIBinder : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Left List")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private GameObject emptyStateNode;

        [Header("Right Detail")]
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button buyButton;

        [Header("Bottom Gold")]
        [SerializeField] private TMP_Text goldValueText;

        [Header("Quantity Popup")]
        [SerializeField] private GameObject quantityPopupRoot;
        [SerializeField] private TMP_Text quantityValueText;
        [SerializeField] private TMP_Text totalPriceValueText;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        public CanvasGroup CanvasGroup => canvasGroup;
        public Transform ListRoot => listRoot;
        public GameObject SlotPrefab => slotPrefab;
        public GameObject EmptyStateNode => emptyStateNode;
        public TMP_Text DescriptionText => descriptionText;
        public Button BuyButton => buyButton;
        public TMP_Text GoldValueText => goldValueText;
        public GameObject QuantityPopupRoot => quantityPopupRoot;
        public TMP_Text QuantityValueText => quantityValueText;
        public TMP_Text TotalPriceValueText => totalPriceValueText;
        public Button DecreaseButton => decreaseButton;
        public Button IncreaseButton => increaseButton;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;
    }
}
