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
        if (UsesFixedUpdate(c.GetType())) // Amedo 2026-10-27
            lock (FixedUpdaters)
            {
                FixedUpdaters.Add(new WeakReference<Component>(c));
                FixedUpdatersVersion++;
            }
        return c;
    }

    internal static readonly List<WeakReference<Component>> FixedUpdaters = new();
    internal static int FixedUpdatersVersion;
    private static readonly Dictionary<Type, bool> FixedUpdateTypes = new();

    private static bool UsesFixedUpdate(Type type)
    {
        lock (FixedUpdateTypes)
        {
            if (!FixedUpdateTypes.TryGetValue(type, out bool uses))
                FixedUpdateTypes[type] = uses = type.GetMethod(nameof(Component.OnFixedUpdate))!.DeclaringType != typeof(Component);
            return uses;
        }
    }

    // Amedo 2026-10-01
    // Amedo 2026-10-27
    public T? Get<T>() where T : Component
    {
        for (int i = 0; i < _components.Count; i++)
            if (_components[i] is T found) return found;
        return null;
    }

    public bool Has<T>() where T : Component
    {
        for (int i = 0; i < _components.Count; i++)
            if (_components[i] is T) return true;
        return false;
    }

    public bool RemoveComponent<T>() where T : Component
    {
        var c = Get<T>();
        if (c is not null) // Amedo 2026-10-27
            lock (FixedUpdaters)
                for (int i = 0; i < FixedUpdaters.Count; i++)
                    if (FixedUpdaters[i].TryGetTarget(out var listed) && ReferenceEquals(listed, c))
                    {
                        FixedUpdaters.RemoveAt(i);
                        FixedUpdatersVersion++;
                        break;
                    }
        return c is not null && _components.Remove(c);
    }

    public void AddChild(Entity child)
    {
        child.Parent             = this;
        child.Transform.Parent   = Transform;
        child.Scene              = Scene;
        _children.Add(child);
    }

    // Amedo
    public void InsertChild(int index, Entity child)
    {
        child.Parent             = this;
        child.Transform.Parent   = Transform;
        child.Scene              = Scene;
        _children.Insert(Math.Clamp(index, 0, _children.Count), child);
    }

    public void RemoveChild(Entity child)
    {
        if (_children.Remove(child))
            child.Parent = null;
    }

    public void ForgetChildren() => _children.Clear();

    internal void Load()
    {
        foreach (var c  in _components) c.OnLoad();
        foreach (var ch in _children)   ch.Load();
    }

    internal void Update()
    {
        if (!Active) return;
        for (int i = 0; i < _components.Count; i++) { var c = _components[i]; if (c.Enabled) c.OnUpdate(); }
        for (int i = 0; i < _children.Count; i++) _children[i].Update();
    }

    // Amedo 2026-10-01
    internal void Render(GL gl, List<Component> translucent, List<Component> overlay, RenderIsolation? isolation = null, bool inside = false) // Amedo 2026-10-04
    {
        if (!Active) return;
        if (!inside && isolation is not null) // Amedo 2026-10-04
        {
            if (isolation.Path.Contains(this))
            {
                for (int i = 0; i < _children.Count; i++) _children[i].Render(gl, translucent, overlay, isolation);
                return;
            }
            if (!ReferenceEquals(this, isolation.Target) && !isolation.Keep.Contains(this)) return;
            inside = true;
        }
        for (int i = 0; i < _components.Count; i++)
        {
            var c = _components[i];
            if (!c.Enabled || Scene.RenderFilter?.Invoke(c) == false) continue;
            if (c.IsOverlay) overlay.Add(c); // Amedo 2026-10-02
            else if (c.IsTranslucent) translucent.Add(c);
            else c.OnRender(gl);
        }
        for (int i = 0; i < _children.Count; i++) _children[i].Render(gl, translucent, overlay, isolation, inside);
    }

    // Amedo
    internal void RenderDepth(GL gl, RenderIsolation? isolation = null, bool inside = false)
    {
        if (!Active) return;
        if (!inside && isolation is not null)
        {
            if (isolation.Path.Contains(this))
            {
                for (int i = 0; i < _children.Count; i++) _children[i].RenderDepth(gl, isolation);
                return;
            }
            if (!ReferenceEquals(this, isolation.Target) && !isolation.Keep.Contains(this)) return;
            inside = true;
        }
        for (int i = 0; i < _components.Count; i++)
        {
            var c = _components[i];
            if (c.Enabled && !c.IsOverlay) c.OnRenderDepth(gl);
        }
        for (int i = 0; i < _children.Count; i++) _children[i].RenderDepth(gl, isolation, inside);
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
