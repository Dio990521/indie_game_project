using UnityEngine;
using IndieGame.Core.Utilities;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// SaveManager 启动引导器：
    /// 在游戏启动早期自动确保场景中存在 SaveManager，避免因场景漏放预制体导致
    /// “自动存档 / 标题读档列表 / 读档恢复”链路失效。
    ///
    /// 说明：
    /// - 若场景里已经有 SaveManager，此引导器不会重复创建；
    /// - 自动创建的对象会挂到 [GameSystem] 根节点并常驻。
    /// </summary>
    public static class SaveManagerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureSaveManagerExists()
        {
            if (Object.FindAnyObjectByType<SaveManager>() != null)
            {
                return;
            }

            GameObject root = GameObject.Find("[GameSystem]");
            if (root == null)
            {
                root = new GameObject("[GameSystem]");
            }

            if (root.GetComponent<DontDestroyRoot>() == null)
            {
                root.AddComponent<DontDestroyRoot>();
            }

            GameObject saveManagerNode = new GameObject("SaveManager");
            saveManagerNode.transform.SetParent(root.transform, false);
            saveManagerNode.AddComponent<SaveManager>();
        }
    }
}
