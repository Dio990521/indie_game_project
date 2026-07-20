using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 土墙道具（改变地形）：
    /// 在落点生成一堵限时土墙（池化预制体，带 Carve 的 NavMeshObstacle），
    /// 阻断敌人的 NavMesh 寻路路径。墙体朝向垂直于"主角→落点"方向（拦在敌人来路上）。
    /// 到时自动回池（NavMeshObstacle 随失活自动取消雕孔）。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Item/Earth Wall")]
    public class EarthWallItemSO : CombatItemSO
    {
        [Header("土墙")]
        [Tooltip("土墙预制体（须挂 NavMeshObstacle(Carve) + TimedPooledEffect 组件）")]
        public GameObject WallPrefab;

        [Tooltip("土墙持续时长（秒）")]
        public float Duration = 8f;

        public override void Execute(CombatManager manager, Vector3 point)
        {
            if (manager == null || WallPrefab == null) return;

            // 朝向：面向"主角→落点"方向（墙体预制体以 X 轴为长边，即墙面垂直拦截该方向）
            Quaternion rotation = Quaternion.identity;
            RosterMember protagonist = manager.Roster.Protagonist;
            if (protagonist != null && protagonist.FieldUnit != null)
            {
                Vector3 dir = point - protagonist.FieldUnit.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            GameObject instance = manager.SpawnPooledEffect(WallPrefab, point, rotation);
            if (instance == null) return;

            TimedPooledEffect timed = instance.GetComponent<TimedPooledEffect>();
            if (timed != null)
            {
                timed.Begin(Duration);
            }
        }
    }
}
