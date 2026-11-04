using System.Text;

namespace CrashEngine.ScriptGen;

// Amedo 2026-11-04: script name -> C# class name and folder (COM_GENERIC_CHARACTER_WALK -> Generic/COM_GENERIC_CHARACTER_WALK)
public static class Naming
{
    public static string ClassName(string scriptName)
    {
        var sb = new StringBuilder();
        foreach (char c in scriptName) sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, "Script_");
        return sb.ToString();
    }

    public static string Folder(string scriptName)
    {
        var parts = scriptName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        string key = parts.Length >= 3 && parts[0] == "COM" ? parts[1] : "Other";
        return key.Length == 0 ? "Other" : char.ToUpperInvariant(key[0]) + key[1..].ToLowerInvariant();
    }
}
