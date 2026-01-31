using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Core
{
    /// <summary>
    /// 场景注册表（ScriptableObject）：
    /// 维护“场景名 -> 场景模式(GameMode)”的映射，
    /// 提供给 SceneLoader 在加载前判断场景类型与加载策略。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Scene/Scene Registry")]
    public class SceneRegistrySO : ScriptableObject
    {
        [Serializable]
        private struct SceneModeEntry
        {
            // 场景名（需与 Build Settings 中一致）
            public string SceneName;
            // 场景类型（Menu/Board/Exploration）
            public GameMode Mode;
        }

        // 配置表：在 Inspector 中维护
        [SerializeField] private List<SceneModeEntry> entries = new List<SceneModeEntry>();

        /// <summary>
        /// 获取指定场景名对应的 GameMode。
        /// 未配置时默认返回 Exploration。
        /// </summary>
        public GameMode GetGameMode(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return GameMode.Exploration;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!string.Equals(entries[i].SceneName, sceneName, StringComparison.Ordinal))
                {
                    continue;
                }
                return entries[i].Mode;
            }
            return GameMode.Exploration;
        }
    }
}
