using Silk.NET.OpenGL;

namespace CrashEngine.Core;

public class Scene
{
    private readonly List<Entity> _roots = new();
    public IReadOnlyList<Entity> Roots => _roots;

    protected virtual void Build()         { }
    protected virtual void OnLoad()        { }
    protected virtual void OnUpdate()      { }
    public    virtual void OnImGuiRender() { }

    protected void AddRoot(Entity e) => AddEntity(e);

    public void AddEntity(Entity e)
    {
        e.Scene = this;
        _roots.Add(e);
    }

    public void LoadAndAddRoot(Entity e)
    {
        AddEntity(e);
        e.Load();
    }

    public void RemoveRoot(Entity e)
    {
        if (_roots.Remove(e))
            e.Destroy();
    }

    public T? FindFirst<T>() where T : Component
    {
        foreach (var e in _roots)
        {
            var c = FindInTree<T>(e);
            if (c != null) return c;
        }
        return null;
    }

    private static T? FindInTree<T>(Entity e) where T : Component
    {
        var c = e.Get<T>();
        if (c != null) return c;
        foreach (var ch in e.Children)
        {
            c = FindInTree<T>(ch);
            if (c != null) return c;
        }
        return null;
    }

    internal void Load(GL gl)
    {
        Build();
        OnLoad();
        foreach (var e in _roots) e.Load();
    }

    internal void Update()
    {
        OnUpdate();
        foreach (var e in _roots) e.Update();
    }

    internal void FixedUpdate()
    {
        foreach (var e in _roots) e.FixedUpdate();
    }

    internal void Render(GL gl)
    {
        foreach (var e in _roots) e.Render(gl, translucentPass: false);
        foreach (var e in _roots) e.Render(gl, translucentPass: true);
    }

    internal void Resize(int w, int h)
    {
        foreach (var e in _roots) e.Resize(w, h);
    }

    internal void Destroy()
    {
        foreach (var e in _roots) e.Destroy();
    }
}
