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

    private readonly List<Component> _translucent = new(); // Amedo 2026-09-26

    // Amedo 2026-10-01
    public static bool InOpaquePass { get; private set; }
    public static event Action<GL>? AfterOpaque;

    internal void Render(GL gl)
    {
        _translucent.Clear(); // Amedo 2026-10-01
        _overlay.Clear(); // Amedo 2026-10-02
        InOpaquePass = true; // Amedo 2026-10-01
        Transform.BeginRenderCache(); // Amedo
        try
        {
            try
            {
                foreach (var e in _roots) e.Render(gl, _translucent, _overlay, Isolation); // Amedo 2026-10-04
            }
            finally
            {
                InOpaquePass = false;
            }
            AfterOpaque?.Invoke(gl); // Amedo 2026-10-01
            _sorted.Clear(); // Amedo 2026-10-01
            for (int i = 0; i < _translucent.Count; i++) _sorted.Add((_translucent[i].RenderOrder, SlotOf(_translucent[i].Entity), i)); // Amedo
            _sorted.Sort(ByOrderThenIndex);
            foreach (var (_, _, index) in _sorted) _translucent[index].OnRender(gl);
        }
        finally
        {
            Transform.EndRenderCache(); // Amedo
        }
        for (int i = 0; i < _overlay.Count; i++) _overlay[i].OnRender(gl); // Amedo 2026-10-02
    }

    // Amedo
    public void RenderDepth(GL gl)
    {
        foreach (var e in _roots) e.RenderDepth(gl, Isolation);
    }

    private readonly List<Component> _overlay = new(); // Amedo 2026-10-02

    // Amedo 2026-10-04
    public RenderIsolation? Isolation { get; set; }

    // Amedo 2026-10-01
    private readonly List<(int Order, int Slot, int Index)> _sorted = new();
    private static readonly Comparison<(int Order, int Slot, int Index)> ByOrderThenIndex =
        (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : a.Index.CompareTo(b.Index); // Amedo

    // Amedo
    private static int SlotOf(Entity? e)
    {
        for (; e is not null; e = e.Parent)
            if (e.DrawSlot != -1) return e.DrawSlot;
        return -1;
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
