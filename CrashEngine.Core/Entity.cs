using Silk.NET.OpenGL;

namespace CrashEngine.Core;

public class Entity
{
    public string Name    { get; set; }
    public bool   Active  { get; set; } = true;
    public int    DrawSlot { get; set; } = -1; // Amedo
    public Transform Transform { get; } = new();

    public Scene?  Scene  { get; internal set; }

    private readonly List<Component> _components = new();
    private readonly List<Entity>    _children   = new();

    public IReadOnlyList<Component> Components => _components;
    public IReadOnlyList<Entity>    Children   => _children;
    public Entity?                  Parent     { get; private set; }

    public Entity(string name = "Entity") => Name = name;

    public T Add<T>(T c) where T : Component
    {
        c.Entity = this;
        _components.Add(c);
        return c;
    }

    // Amedo 2026-10-01
    public T? Get<T>() where T : Component
    {
        foreach (var c in _components)
            if (c is T found) return found;
        return null;
    }

    public bool Has<T>() where T : Component
    {
        foreach (var c in _components)
            if (c is T) return true;
        return false;
    }

    public bool RemoveComponent<T>() where T : Component
    {
        var c = Get<T>();
        return c is not null && _components.Remove(c);
    }

    public void AddChild(Entity child)
    {
        child.Parent             = this;
        child.Transform.Parent   = Transform;
        child.Scene              = Scene;
        _children.Add(child);
    }

    public void RemoveChild(Entity child)
    {
        if (_children.Remove(child))
            child.Parent = null;
    }

    internal void Load()
    {
        foreach (var c  in _components) c.OnLoad();
        foreach (var ch in _children)   ch.Load();
    }

    internal void Update()
    {
        if (!Active) return;
        foreach (var c  in _components) { if (c.Enabled) c.OnUpdate(); }
        foreach (var ch in _children)   ch.Update();
    }

    internal void FixedUpdate()
    {
        if (!Active) return;
        foreach (var c  in _components) { if (c.Enabled) c.OnFixedUpdate(); }
        foreach (var ch in _children)   ch.FixedUpdate();
    }

    // Amedo 2026-10-01
    internal void Render(GL gl, List<Component> translucent, List<Component> overlay, RenderIsolation? isolation = null, bool inside = false) // Amedo 2026-10-04
    {
        if (!Active) return;
        if (!inside && isolation is not null) // Amedo 2026-10-04
        {
            if (isolation.Path.Contains(this))
            {
                foreach (var ch in _children) ch.Render(gl, translucent, overlay, isolation);
                return;
            }
            if (!ReferenceEquals(this, isolation.Target) && !isolation.Keep.Contains(this)) return;
            inside = true;
        }
        foreach (var c in _components)
        {
            if (!c.Enabled) continue;
            if (c.IsOverlay) overlay.Add(c); // Amedo 2026-10-02
            else if (c.IsTranslucent) translucent.Add(c);
            else c.OnRender(gl);
        }
        foreach (var ch in _children) ch.Render(gl, translucent, overlay, isolation, inside);
    }

    // Amedo
    internal void RenderDepth(GL gl, RenderIsolation? isolation = null, bool inside = false)
    {
        if (!Active) return;
        if (!inside && isolation is not null)
        {
            if (isolation.Path.Contains(this))
            {
                foreach (var ch in _children) ch.RenderDepth(gl, isolation);
                return;
            }
            if (!ReferenceEquals(this, isolation.Target) && !isolation.Keep.Contains(this)) return;
            inside = true;
        }
        foreach (var c in _components)
            if (c.Enabled && !c.IsOverlay) c.OnRenderDepth(gl);
        foreach (var ch in _children) ch.RenderDepth(gl, isolation, inside);
    }

    internal void Resize(int w, int h)
    {
        foreach (var c  in _components) c.OnResize(w, h);
        foreach (var ch in _children)   ch.Resize(w, h);
    }

    internal void Destroy()
    {
        foreach (var c  in _components) c.OnDestroy();
        foreach (var ch in _children)   ch.Destroy();
    }
}
