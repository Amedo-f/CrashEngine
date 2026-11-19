using System.Text.RegularExpressions;

namespace CrashLauncher;

// Amedo 2026-11-19
public static partial class KeyUseScan
{
    public const int FirstSpecialKey = 0xF2;

    public readonly record struct KeyRange(int From, int To)
    {
        public bool Contains(int index) => index >= From && index <= To;
    }

    public static List<KeyRange> OwnKeys(string code)
    {
        var list = new List<KeyRange>();
        foreach (Match m in FocusToKey().Matches(code))
            if (!ReadsOtherObject(m.Groups[2].Value) && Index(m.Groups[1].Value) is { } k) list.Add(new(k, k));
        foreach (Match m in KeyCall().Matches(code))
        {
            var args = m.Groups[1].Value;
            if (Arg(args, "key") is { } k) { list.Add(new(k, k)); continue; }
            int? first = Arg(args, "first"), last = Arg(args, "last");
            if (first is not null || last is not null) list.Add(new(first ?? 0, last ?? FirstSpecialKey - 1));
        }
        foreach (Match m in KeyCondition().Matches(code))
            if (int.TryParse(m.Groups[1].Value, out int n) && n >= 1 && n - 1 < FirstSpecialKey) list.Add(new(n - 1, n - 1));
        return list;
    }

    private static bool ReadsOtherObject(string args) => args.Contains("ofFocus: true") || args.Contains("ofSlot: true");

    private static int? Index(string text) => int.TryParse(text, out int n) && n < FirstSpecialKey ? n : null;

    private static int? Arg(string args, string name)
    {
        var m = Regex.Match(args, $@"\b{name}: (\d+)");
        return m.Success ? Index(m.Groups[1].Value) : null;
    }

    [GeneratedRegex(@"\bSetFocusToKey2?\(key: (\d+)([^)]*)\)")]
    private static partial Regex FocusToKey();

    [GeneratedRegex(@"\bSetKey(?:NearestPlayer)?\(([^)]*)\)")]
    private static partial Regex KeyCall();

    [GeneratedRegex(@"ConditionId\.(?:CurrentKeyEquals|MeToKeySqrDist), (\d+)")]
    private static partial Regex KeyCondition();
}
