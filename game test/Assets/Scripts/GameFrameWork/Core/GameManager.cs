using UnityEngine;
using System;
using IndieGame.Core.Utilities; // 引用之前的单例模板

namespace IndieGame.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        // 当前状态
        public GameState CurrentState { get; private set; } = GameState.FreeRoam;

        // 事件：当状态改变时触发 (其他脚本订阅这个事件)
        public static event Action<GameState> OnStateChanged;

        protected override void Awake()
        {
            base.Awake();
            // 初始化
            Application.targetFrameRate = 60;
        }

        private void Start()
        {
            // 游戏开始时，广播一次默认状态
            ChangeState(GameState.FreeRoam);
        }

        /// <summary>
        /// 切换游戏状态的统一入口
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;
            Debug.Log($"<color=orange>[GameManager] State Changed to: {newState}</color>");

            // 触发事件，通知所有监听者
            OnStateChanged?.Invoke(newState);
        }
        
    }
}