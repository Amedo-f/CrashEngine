using System.Collections;
using System.Reflection;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;

namespace CrashLauncher;

// Amedo
public static class InspectorUndoData
{
    public static byte[] ItemBytes(BaseTwinItem item)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.Default, true)) item.Write(w);
        return ms.ToArray();
    }

    public static void RestoreItem(BaseTwinItem item, byte[] bytes)
    {
        var pack = (item as PS2AnyObject)?.BehaviourPack;
        if (item is Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter starter) starter.Assigners.Clear(); // Amedo
        using var ms = new MemoryStream(bytes);
        using var r = new BinaryReader(ms);
        item.Read(r, bytes.Length);
        if (item is PS2AnyObject obj && pack is not null && !ReferenceEquals(obj.BehaviourPack, pack))
        {
            pack.Commands = obj.BehaviourPack.Commands;
            obj.BehaviourPack = pack;
        }
    }

    private static readonly Dictionary<Type, MemberInfo[]> MemberCache = new();
    private static readonly HashSet<string> SkippedMembers = ["Albedo", "Entity", "Enabled", "ClothClock", "RuntimeShadowSlots", "RuntimeFlags", "RuntimeShadowActive", "Mesh"];

    public static MemberInfo[] MembersOf(Type t)
    {
        if (MemberCache.TryGetValue(t, out var m)) return m;
        var list = new List<MemberInfo>();
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.GetIndexParameters().Length == 0 && p.CanRead && p.GetMethod!.IsPublic && !SkippedMembers.Contains(p.Name) &&
                ((p.CanWrite && p.SetMethod!.IsPublic) || IsValueList(p.PropertyType))) list.Add(p);
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (!SkippedMembers.Contains(f.Name) && (!f.IsInitOnly || IsValueList(f.FieldType))) list.Add(f);
        return MemberCache[t] = list.ToArray();
    }

    public static bool IsValue(Type t) => t.IsValueType || t == typeof(string);
    public static bool IsValueList(Type t) =>
        (t.IsArray && IsValue(t.GetElementType()!)) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) && IsValue(t.GetGenericArguments()[0]));

    public static object?[] ReadMembers(object o)
    {
        var ms = MembersOf(o.GetType());
        var v = new object?[ms.Length];
        for (int i = 0; i < ms.Length; i++)
        {
            object? x = ms[i] is PropertyInfo p ? p.GetValue(o) : ((FieldInfo)ms[i]).GetValue(o);
            v[i] = x switch
            {
                Array a when IsValue(a.GetType().GetElementType()!) => a.Clone(),
                IList l when IsValueList(l.GetType()) => l.Cast<object?>().ToArray(),
                _ => x,
            };
        }
        return v;
    }

    public static void WriteMembers(object o, object?[] v)
    {
        var ms = MembersOf(o.GetType());
        for (int i = 0; i < ms.Length; i++)
        {
            var type = ms[i] is PropertyInfo pi ? pi.PropertyType : ((FieldInfo)ms[i]).FieldType;
            object? cur = ms[i] is PropertyInfo p ? p.GetValue(o) : ((FieldInfo)ms[i]).GetValue(o);
            if (IsValueList(type) && v[i] is not null && cur is not null)
            {
                if (cur is Array arr && v[i] is Array src && arr.Length == src.Length) { Array.Copy(src, arr, arr.Length); continue; }
                if (cur is IList list && v[i] is object?[] items) { list.Clear(); foreach (var it in items) list.Add(it); continue; }
            }
            if (ms[i] is PropertyInfo prop) { if (prop.CanWrite) prop.SetValue(o, v[i] is object?[] ov && type.IsArray ? ToArray(ov, type) : v[i]); }
            else if (ms[i] is FieldInfo f && !f.IsInitOnly) f.SetValue(o, v[i]);
        }
    }

    public static Array ToArray(object?[] v, Type t)
    {
        var a = Array.CreateInstance(t.GetElementType()!, v.Length);
        for (int i = 0; i < v.Length; i++) a.SetValue(v[i], i);
        return a;
    }

    public static bool SameValues(object?[] a, object?[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] is Array x && b[i] is Array y) { if (!x.Cast<object?>().SequenceEqual(y.Cast<object?>())) return false; continue; }
            if (a[i] is object?[] lx && b[i] is object?[] ly) { if (!lx.SequenceEqual(ly)) return false; continue; }
            if (!Equals(a[i], b[i])) return false;
        }
        return true;
    }
}
