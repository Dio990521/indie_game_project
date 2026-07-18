using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 遭遇配置：
    /// 描述一场战斗的敌人波次与（测试用）我方名册。
    /// 正式流程中名册应来自玩家的"已解锁角色"数据（Phase 2 接入存档），
    /// PlayerRoster 字段用于测试场景与兜底。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Encounter")]
    public class EncounterSO : ScriptableObject
    {
        /// <summary>
        /// 单条刷怪配置：某个出生点刷 N 只某种敌人。
        /// </summary>
        [Serializable]
        public class SpawnEntry
        {
            [Tooltip("敌人定义")]
            public EnemyDefinitionSO Enemy;

            [Tooltip("数量")]
            [Min(1)] public int Count = 1;

            [Tooltip("出生点索引（对应 CombatSceneRefs.EnemySpawnPoints 数组）")]
            [Min(0)] public int SpawnPointIndex;
        }

        /// <summary>
        /// 一波敌人：延迟到达后生成本波全部条目。
        /// </summary>
        [Serializable]
        public class Wave
        {
            [Tooltip("本波的刷怪条目")]
            public List<SpawnEntry> Spawns = new List<SpawnEntry>();

            [Tooltip("与上一波（或战斗开始）之间的间隔秒数；第 0 波通常为 0")]
            [Min(0f)] public float DelayAfterPrevious;
        }

        [Header("敌方波次")]
        [Tooltip("按顺序生成的敌人波次（Phase 1 测试建议 1 波、2~3 只）")]
        public List<Wave> Waves = new List<Wave>();

        [Header("我方名册（测试/兜底）")]
        [Tooltip("参战角色定义列表（≤5 人，须包含且仅包含一名 IsProtagonist=true 的主角）")]
        public List<CharacterDefinitionSO> PlayerRoster = new List<CharacterDefinitionSO>();
    }
}
