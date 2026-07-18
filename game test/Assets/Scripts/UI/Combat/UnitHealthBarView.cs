using UnityEngine;
using UnityEngine.UI;
using IndieGame.Core;
using IndieGame.Gameplay.Combat;

namespace IndieGame.UI.Combat
{
    /// <summary>
    /// 单位头顶血条（挂在战斗体预制体的世界空间 Canvas 上）：
    /// 订阅 HealthChangedEvent 按归属过滤刷新；OnEnable 时主动从 CharacterStats 拉初值
    /// （规避"CharacterStats 广播早于本组件订阅"的时序问题，对象池复用时同样适用）；
    /// LateUpdate 朝相机 billboard。
    /// </summary>
    public class UnitHealthBarView : EventBusMonoBehaviour
    {
        [Tooltip("血条填充（fillAmount）")]
        [SerializeField] private Image fillImage;

        [Tooltip("满血时是否隐藏血条（减少画面噪音）")]
        [SerializeField] private bool hideWhenFull = true;

        [Tooltip("血条根物体（显隐控制）。注意：必须是本组件的子物体，不能是本物体自身，" +
                 "否则隐藏后本组件被禁用、无法再收到血量事件恢复显示。为空则不做满血隐藏")]
        [SerializeField] private GameObject barRoot;

        // 归属战斗单位（父链缓存；对象池复用时父链不变，缓存一次即可）
        private CombatUnit _unit;
        // billboard 相机缓存
        private Camera _camera;

        private void Awake()
        {
            _unit = GetComponentInParent<CombatUnit>();
            // 安全保护：barRoot 指向自身会导致隐藏后组件失活、事件断链
            if (barRoot == gameObject) barRoot = null;
        }

        protected override void Bind()
        {
            Subscribe<HealthChangedEvent>(HandleHealthChanged);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _camera = Camera.main;
            // 主动拉取当前值（对象池复用/订阅时序兜底）
            if (_unit != null && _unit.Stats != null)
            {
                Refresh(_unit.Stats.CurrentHP, _unit.Stats.MaxHP);
            }
        }

        private void HandleHealthChanged(HealthChangedEvent evt)
        {
            if (_unit == null || evt.Owner != _unit.gameObject) return;
            Refresh(evt.Current, evt.Max);
        }

        private void Refresh(int current, int max)
        {
            float percent = max > 0 ? (float)current / max : 0f;
            if (fillImage != null) fillImage.fillAmount = Mathf.Clamp01(percent);
            if (barRoot != null)
            {
                // 满血（可选）与死亡时隐藏，受击后显示
                bool visible = percent > 0f && (!hideWhenFull || percent < 1f);
                barRoot.SetActive(visible);
            }
        }

        private void LateUpdate()
        {
            // billboard：血条始终面向相机
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }
            transform.rotation = _camera.transform.rotation;
        }
    }
}
