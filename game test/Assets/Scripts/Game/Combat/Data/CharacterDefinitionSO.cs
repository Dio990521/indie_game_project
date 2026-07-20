using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 我方角色定义（名册成员的静态配置）：
    /// 描述一名可上场角色的战斗体预制体、数值配置、默认武器与入场技。
    /// Phase 1 使用 Prefab/StatConfig/DefaultWeaponData/IsProtagonist/EntrySkill/RedeployCooldown；
    /// 生产特长字段为 Phase 2 道具系统预留。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Character Definition")]
    public class CharacterDefinitionSO : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("角色唯一 ID（名册/存档用）")]
        public string ID;

        [Tooltip("角色显示名（HUD 名册槽用）")]
        public string DisplayName;

        [Tooltip("头像（HUD 名册槽显示）")]
        public Sprite Portrait;

        [Header("战斗体")]
        [Tooltip("战斗体预制体：挂 CombatUnit + CharacterStats + NavMeshAgent 及各战斗组件")]
        public GameObject CombatUnitPrefab;

        [Tooltip("数值配置（生成战斗体后注入 CharacterStats）")]
        public CharacterStatConfigSO StatConfig;

        [Tooltip("默认武器战斗数据（主角优先读取已装备武器的 CombatData，为空时用本字段兜底；同伴直接使用本字段）")]
        public WeaponCombatDataSO DefaultWeaponData;

        [Tooltip("是否主角：主角固定 0 号槽、战斗开始即在场、不可下场、死亡即战败")]
        public bool IsProtagonist;

        [Header("调度")]
        [Tooltip("下场后再次上场的冷却时间（秒）")]
        public float RedeployCooldown = 8f;

        [Tooltip("入场技（可空）：上场瞬间自动以战斗体为施法者释放一次")]
        public SkillSO EntrySkill;

        [Header("后台生产特长")]
        [Tooltip("该角色擅长生产的战斗道具候选（战斗中随机产出其一；超过 2 个只取前 2 个，对应\"战前最多配置 2 个配方\"的设计）")]
        public List<CombatItemSO> ProducibleItems = new List<CombatItemSO>();
    }
}
