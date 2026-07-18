using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 上场放置指示器（战斗场景对象，CombatSceneRefs 引用）：
    /// - 半径圈：显示以主角为圆心的可放置范围；
    /// - 落点标记：跟随指向输入移动，按落点合法性变色（绿=可放置，红=不可）。
    /// 两个视觉件为常驻子物体（贴地 Quad + 半透明 Unlit 材质），无逐帧实例化；
    /// 变色用 MaterialPropertyBlock，避免运行时复制材质。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlacementIndicatorController : MonoBehaviour
    {
        [Header("视觉件（常驻子物体）")]
        [Tooltip("可放置范围圈（缩放匹配放置半径，直径 = 半径 × 2）")]
        [SerializeField] private Transform rangeCircle;

        [Tooltip("落点标记（跟随指向输入）")]
        [SerializeField] private Transform pointMarker;

        [Tooltip("落点标记的渲染器（用于合法性变色）")]
        [SerializeField] private Renderer pointMarkerRenderer;

        [Header("配色")]
        [Tooltip("落点合法颜色")]
        [SerializeField] private Color validColor = new Color(0.2f, 1f, 0.4f, 0.6f);

        [Tooltip("落点非法颜色")]
        [SerializeField] private Color invalidColor = new Color(1f, 0.25f, 0.2f, 0.6f);

        [Tooltip("贴地高度偏移（防 z-fighting）")]
        [SerializeField] private float groundOffset = 0.05f;

        // 变色用属性块（避免材质实例化）
        private MaterialPropertyBlock _propertyBlock;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_BaseColor");
        // 兼容 Built-in/Unlit 常见颜色属性名
        private static readonly int LegacyColorPropertyId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            Hide();
        }

        /// <summary>
        /// 显示指示器：范围圈落在基准点，直径匹配放置半径。
        /// </summary>
        public void Show(Vector3 origin, float radius)
        {
            if (rangeCircle != null)
            {
                rangeCircle.gameObject.SetActive(true);
                rangeCircle.position = origin + Vector3.up * groundOffset;
                // 约定：范围圈 Quad 的原始尺寸为 1×1 米，缩放 = 直径
                rangeCircle.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            }
            if (pointMarker != null)
            {
                pointMarker.gameObject.SetActive(true);
                pointMarker.position = origin + Vector3.up * groundOffset;
            }
        }

        /// <summary>
        /// 更新落点位置与合法性配色（放置态每帧调用）。
        /// </summary>
        public void UpdatePoint(Vector3 point, bool valid)
        {
            if (pointMarker != null)
            {
                pointMarker.position = point + Vector3.up * groundOffset;
            }
            if (pointMarkerRenderer != null && _propertyBlock != null)
            {
                Color color = valid ? validColor : invalidColor;
                pointMarkerRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(ColorPropertyId, color);
                _propertyBlock.SetColor(LegacyColorPropertyId, color);
                pointMarkerRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>
        /// 隐藏指示器（放置态退出时调用）。
        /// </summary>
        public void Hide()
        {
            if (rangeCircle != null) rangeCircle.gameObject.SetActive(false);
            if (pointMarker != null) pointMarker.gameObject.SetActive(false);
        }
    }
}
