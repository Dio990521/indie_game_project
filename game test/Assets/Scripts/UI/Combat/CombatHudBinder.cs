using TMPro;
using UnityEngine;

namespace IndieGame.UI.Combat
{
    /// <summary>
    /// 战斗 HUD 绑定器（MVB 的 Binder：只持有序列化引用，无任何逻辑）。
    /// </summary>
    public class CombatHudBinder : MonoBehaviour
    {
        [Header("根节点")]
        [Tooltip("整体显隐控制的 CanvasGroup")]
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Header("名册栏")]
        [Tooltip("名册槽位的父容器（横向布局）")]
        [SerializeField] private Transform rosterSlotContainer;

        [Tooltip("名册槽位预制体")]
        [SerializeField] private RosterSlotUI rosterSlotPrefab;

        [Header("结算")]
        [Tooltip("结算面板根物体")]
        [SerializeField] private GameObject resultPanel;

        [Tooltip("结算文本（胜利/失败）")]
        [SerializeField] private TMP_Text resultText;

        [Header("操作提示")]
        [Tooltip("放置态操作提示文本（可空）")]
        [SerializeField] private TMP_Text placementHintText;

        public CanvasGroup RootCanvasGroup => rootCanvasGroup;
        public Transform RosterSlotContainer => rosterSlotContainer;
        public RosterSlotUI RosterSlotPrefab => rosterSlotPrefab;
        public GameObject ResultPanel => resultPanel;
        public TMP_Text ResultText => resultText;
        public TMP_Text PlacementHintText => placementHintText;
    }
}
