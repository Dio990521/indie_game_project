using UnityEngine;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// AutoSaveService 启动引导器：
    /// 作用是在运行时保证全局自动存档服务存在，避免业务层发出 AutoSaveRequestedEvent 后无人处理。
    ///
    /// 挂载策略：
    /// 1) 若场景中已存在 AutoSaveService：直接复用；
    /// 2) 否则优先挂到 SaveManager 所在对象（便于把“存档相关服务”集中管理）；
    /// 3) 若 SaveManager 尚未可用，则创建独立常驻对象兜底。
    /// </summary>
    public static class AutoSaveServiceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAutoSaveServiceExists()
        {
            if (Object.FindAnyObjectByType<AutoSaveService>() != null)
            {
                return;
            }

            SaveManager saveManager = Object.FindAnyObjectByType<SaveManager>();
            if (saveManager != null)
            {
                if (saveManager.GetComponent<AutoSaveService>() == null)
                {
                    saveManager.gameObject.AddComponent<AutoSaveService>();
                }
                return;
            }

            GameObject node = new GameObject("AutoSaveService");
            node.AddComponent<AutoSaveService>();
            Object.DontDestroyOnLoad(node);
        }
    }
}
