using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CrashEngine.Core;

// Amedo 2026-10-01
public static class JitWarmup
{
    private static int _started;

    public static int Methods { get; private set; }
    public static double Milliseconds { get; private set; }
    public static bool Done { get; private set; }

    public static void Start(params Assembly[] assemblies)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        var thread = new Thread(() => Run(assemblies)) { IsBackground = true, Name = "JIT warm-up", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    private static void Run(Assembly[] assemblies)
    {
        var clock = Stopwatch.StartNew();
        int count = 0;
        var generics = new HashSet<Type>(); // Amedo
        foreach (var assembly in assemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }
            foreach (var type in types)
            {
                if (type.ContainsGenericParameters) continue;
                count += Prepare(type, null);
                foreach (var field in type.GetFields(All)) CollectGeneric(field.FieldType, generics); // Amedo
            }
        }
        foreach (var type in generics) count += PrepareGeneric(type, 0); // Amedo
        Methods = count;
        Milliseconds = clock.Elapsed.TotalMilliseconds;
        Done = true;
    }

    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static int Prepare(Type type, RuntimeTypeHandle[]? instantiation)
    {
        int count = 0;
        foreach (var method in type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All)))
        {
            if (method.IsAbstract || method.ContainsGenericParameters || (method.IsStatic && method is ConstructorInfo) || method.GetMethodBody() is null) continue;
            try
            {
                if (instantiation is null) RuntimeHelpers.PrepareMethod(method.MethodHandle);
                else RuntimeHelpers.PrepareMethod(method.MethodHandle, instantiation);
                count++;
            }
            catch
            {
            }
        }
        return count;
    }

    // Amedo
    private static void CollectGeneric(Type type, HashSet<Type> found)
    {
        if (type.HasElementType)
        {
            CollectGeneric(type.GetElementType()!, found);
            return;
        }
        if (!type.IsGenericType || type.ContainsGenericParameters || !found.Add(type)) return;
        foreach (var argument in type.GetGenericArguments()) CollectGeneric(argument, found);
    }

    private static readonly HashSet<Type> Comparers = new(); // Amedo

    // Amedo
    private static int PrepareComparer(Type type)
    {
        if (!Comparers.Add(type)) return 0;
        try
        {
            if (typeof(EqualityComparer<>).MakeGenericType(type).GetProperty("Default")!.GetValue(null) is not { } comparer) return 0;
            var concrete = comparer.GetType();
            return Prepare(concrete, concrete.IsGenericType ? concrete.GetGenericArguments().Select(a => a.TypeHandle).ToArray() : null);
        }
        catch
        {
            return 0;
        }
    }

    // Amedo
    private static int PrepareGeneric(Type type, int depth)
    {
        var arguments = type.GetGenericArguments();
        int count = Prepare(type, arguments.Select(a => a.TypeHandle).ToArray());
        if (depth == 0)
            foreach (var argument in arguments)
                if (argument.IsValueType) count += PrepareComparer(argument);
        if (depth >= 2) return count;
        foreach (var nested in type.GetGenericTypeDefinition().GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!nested.IsGenericTypeDefinition || nested.GetGenericArguments().Length != arguments.Length) continue;
            Type closed;
            try { closed = nested.MakeGenericType(arguments); }
            catch { continue; }
            count += PrepareGeneric(closed, depth + 1);
        }
        return count;
    }
}
