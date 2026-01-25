using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Core
{
    [CreateAssetMenu(menuName = "IndieGame/Scene/Scene Registry")]
    public class SceneRegistrySO : ScriptableObject
    {
        [Serializable]
        private struct SceneModeEntry
        {
            public string SceneName;
            public GameMode Mode;
        }

        [SerializeField] private List<SceneModeEntry> entries = new List<SceneModeEntry>();

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
