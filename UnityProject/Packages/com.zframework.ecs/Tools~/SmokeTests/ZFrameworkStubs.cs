namespace ZFramework
{
    public interface IUpdateModule
    {
        void Update(float elapseSeconds, float realElapseSeconds);
    }

    public abstract class Module
    {
        public virtual int Priority => 0;
        public abstract void OnInit();
        public abstract void Shutdown();
    }
}
