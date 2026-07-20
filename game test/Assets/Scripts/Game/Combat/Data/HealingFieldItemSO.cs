using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 范围持续治疗道具：
    /// 在落点生成一片治疗领域（池化预制体），持续期间按间隔为范围内的我方存活单位回血。
    /// 具体逐帧逻辑由预制体上的 HealingField 组件承载。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Item/Healing Field")]
    public class HealingFieldItemSO : CombatItemSO
    {
        [Header("治疗领域")]
        [Tooltip("领域预制体（须挂 HealingField 组件，运行时走对象池）")]
        public GameObject FieldPrefab;

        [Tooltip("治疗范围半径")]
        public float Radius = 3f;

        [Tooltip("每次跳血的治疗量")]
        public int HealPerTick = 5;

        [Tooltip("跳血间隔（秒）")]
        public float TickInterval = 1f;

        [Tooltip("领域持续时长（秒）")]
        public float Duration = 6f;

        public override void Execute(CombatManager manager, Vector3 point)
        {
            if (manager == null || FieldPrefab == null) return;

            GameObject instance = manager.SpawnPooledEffect(FieldPrefab, point, Quaternion.identity);
            if (instance == null) return;

            HealingField field = instance.GetComponent<HealingField>();
            if (field != null)
            {
                field.Begin(Radius, HealPerTick, TickInterval, Duration);
            }
        }
    }
}
