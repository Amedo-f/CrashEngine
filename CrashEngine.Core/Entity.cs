using Silk.NET.OpenGL;

namespace CrashEngine.Core;

public class Entity
{
    public string Name    { get; set; }
    public bool   Active  { get; set; } = true;
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

    public T? Get<T>() where T : Component => _components.OfType<T>().FirstOrDefault();

    public bool Has<T>() where T : Component => _components.OfType<T>().Any();

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

    internal void Render(GL gl, bool translucentPass)
    {
        if (!Active) return;
        foreach (var c  in _components) { if (c.Enabled && c.IsTranslucent == translucentPass) c.OnRender(gl); }
        foreach (var ch in _children)   ch.Render(gl, translucentPass);
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
