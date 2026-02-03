using UnityEngine;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 简易视图基类：
    /// 提供 Show/Hide 接口，便于统一 UI 生命周期管理。
    /// </summary>
    public abstract class View : MonoBehaviour
    {
        public virtual void Show() { }
        public virtual void Hide() { }
    }
}
