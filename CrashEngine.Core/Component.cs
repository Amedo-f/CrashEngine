using Silk.NET.OpenGL;

namespace CrashEngine.Core;

public abstract class Component
{
    public Entity    Entity    { get; internal set; } = null!;
    public Transform Transform => Entity.Transform;
    public bool      Enabled   { get; set; } = true;

    public virtual bool IsTranslucent => false;

    public virtual void OnLoad()         { }
    public virtual void OnUpdate()       { }
    public virtual void OnFixedUpdate()  { }
    public virtual void OnRender(GL gl)  { }
    public virtual void OnResize(int w, int h) { }
    public virtual void OnDestroy()      { }
}
