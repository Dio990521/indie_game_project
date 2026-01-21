namespace IndieGame.Core
{
    public abstract class BaseState<T>
    {
        public virtual void OnEnter(T context) { }
        public virtual void OnUpdate(T context) { }
        public virtual void OnExit(T context) { }
        public virtual void OnInteract(T context) { }
    }
}
