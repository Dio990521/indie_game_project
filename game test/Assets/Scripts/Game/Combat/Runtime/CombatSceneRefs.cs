using UnityEngine;
using Unity.Cinemachine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗场景引用聚合：
    /// 收拢战斗场景内的场景对象引用（出生点/放置指示器/战斗相机），
    /// CombatManager 通过本组件访问场景对象，避免任何 Find 调用。
    /// 挂在战斗场景的 [CombatSceneRefs] 物体上，Inspector 赋值。
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatSceneRefs : MonoBehaviour
    {
        [Header("出生点")]
        [Tooltip("主角战斗体出生点")]
        [SerializeField] private Transform playerSpawnPoint;

        [Tooltip("敌人出生点数组（EncounterSO.SpawnEntry.SpawnPointIndex 对应此数组下标）")]
        [SerializeField] private Transform[] enemySpawnPoints;

        [Header("放置")]
        [Tooltip("上场放置指示器")]
        [SerializeField] private PlacementIndicatorController placementIndicator;

        [Header("相机")]
        [Tooltip("战斗虚拟相机（Priority 高于常驻主 vcam；战斗开始后 Follow 主角战斗体，场景卸载自动切回）")]
        [SerializeField] private CinemachineCamera battleCamera;

        public Transform PlayerSpawnPoint => playerSpawnPoint;
        public Transform[] EnemySpawnPoints => enemySpawnPoints;
        public PlacementIndicatorController PlacementIndicator => placementIndicator;
        public CinemachineCamera BattleCamera => battleCamera;

        /// <summary>
        /// 按索引取敌人出生点（越界时回退 0 号或场景原点）。
        /// </summary>
        public Vector3 GetEnemySpawnPosition(int index)
        {
            if (enemySpawnPoints == null || enemySpawnPoints.Length == 0) return Vector3.zero;
            int clamped = Mathf.Clamp(index, 0, enemySpawnPoints.Length - 1);
            Transform point = enemySpawnPoints[clamped];
            return point != null ? point.position : Vector3.zero;
        }
    }
}
