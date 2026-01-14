using System.Collections;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime; // 为了访问 BoardGameManager

namespace IndieGame.Gameplay.Board.Events
{
    /// <summary>
    /// 抽象的棋盘事件基类。
    /// 所有的具体事件（如：Log, 转向, 获得金币）都继承自它。
    /// </summary>
    public abstract class BoardEventSO : ScriptableObject
    {
        [TextArea] public string description;

        /// <summary>
        /// 执行事件逻辑。返回 IEnumerator 是为了支持延时（如等待动画播放完）。
        /// </summary>
        /// <param name="manager">提供上下文，让事件能访问角色、位置等信息</param>
        public abstract IEnumerator Execute(BoardGameManager manager, Transform targetContext);
    }
}