using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 限时池化效果（通用）：
    /// 启动后倒计时，到时/战斗停止自动经 CombatManager 回池。
    /// 土墙道具用它管理生命周期——墙体失活时其 NavMeshObstacle 的雕孔自动取消，
    /// NavMesh 路径随之恢复，无需额外处理。
    /// </summary>
    [DisallowMultipleComponent]
    public class TimedPooledEffect : MonoBehaviour
    {
        private float _endTime;
        private bool _active;

        /// <summary>
        /// 启动倒计时（由道具 Execute 在池取出后调用）。
        /// </summary>
        public void Begin(float duration)
        {
            _endTime = Time.time + Mathf.Max(0.1f, duration);
            _active = true;
        }

        private void Update()
        {
            if (!_active) return;

            CombatManager manager = CombatManager.Instance;
            if (manager == null || !manager.BattleRunning || Time.time >= _endTime)
            {
                _active = false;
                if (manager != null)
                {
                    manager.ReleasePooledEffect(gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
