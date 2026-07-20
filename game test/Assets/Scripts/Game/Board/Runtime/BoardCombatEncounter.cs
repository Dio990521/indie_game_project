using UnityEngine;
using IndieGame.Gameplay.Combat;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 棋盘战斗遭遇标记（挂在棋盘 NPC/敌人实体上）：
    /// 玩家在棋盘上与该实体相遇（BoardEntityInteractionEvent）时，
    /// BoardGameManager 检测到本组件即携带遭遇配置进入战斗场景。
    /// 战斗胜利后该实体默认从棋盘销毁（击败即移除；Phase 2 简化处理，不进存档）。
    /// 注意：移除必须用 Destroy 而不能用 SetActive(false)——棋盘 NPC 多为场景根物体，
    /// 返回棋盘时 SceneLoader.SetBoardSceneRootsActive(true) 会全量重新激活所有根物体，
    /// 仅隐藏的实体会被复活。
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardCombatEncounter : MonoBehaviour
    {
        [Tooltip("本实体触发的遭遇配置（波次与我方名册）")]
        [SerializeField] private EncounterSO encounter;

        [Tooltip("战斗场景名（需已加入 Build Settings 且在 SceneRegistry 中注册为 Combat）")]
        [SerializeField] private string combatSceneName = "CombatTest";

        [Tooltip("战斗胜利后是否把本实体从棋盘销毁（击败即移除）")]
        [SerializeField] private bool removeOnVictory = true;

        public EncounterSO Encounter => encounter;
        public string CombatSceneName => combatSceneName;
        public bool RemoveOnVictory => removeOnVictory;
    }
}
