using System.Text;

namespace CrashEngine.Runtime.Screen;

// Amedo 2026-09-30
public sealed class GameTexts
{
    public const string AgentLabPath = @"Language\AgentLab\American.txt", CodePath = @"Language\Code\American.txt";
    public const string CreditsPath = @"Language\Credits\American.txt"; // Amedo 2026-10-01
    public const string AgentLabFile = "AgentLab.txt", CodeFile = "Code.txt", CreditsFile = "Credits.txt";
    public static string AgentLabPathFor(string language) => $@"Language\AgentLab\{language}.txt"; // Amedo
    public static string CodePathFor(string language) => $@"Language\Code\{language}.txt"; // Amedo
    public static string CreditsPathFor(string language) => $@"Language\Credits\{language}.txt"; // Amedo

    private readonly string[] _lines;

    public GameTexts(byte[] file) : this(Encoding.Latin1.GetString(file)) { }

    // Amedo 2026-10-01
    public GameTexts(string text)
    {
        _lines = text.TrimStart('﻿')
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Trim().Length > 0)
            .ToArray();
    }

    public static GameTexts? FromLanguage(string? directory, string file) =>
        directory is not null && File.Exists(Path.Combine(directory, file)) ? new GameTexts(File.ReadAllText(Path.Combine(directory, file), Encoding.UTF8)) : null;

    public int Count => _lines.Length;

    // Amedo 2026-10-01
    public static void SetLine(string file, int index, string text)
    {
        text = text.Replace("\r\n", "~").Replace('\n', '~').Replace('\r', '~');
        if (text.Trim().Length == 0) throw new ArgumentException("a text line can't be empty (empty lines don't count in the game's text files)");
        var lines = File.Exists(file)
            ? File.ReadAllText(file, Encoding.UTF8).TrimStart('﻿').Split('\n').Select(l => l.TrimEnd('\r')).ToList()
            : new List<string>();
        int n = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim().Length == 0 || ++n != index) continue;
            lines[i] = text;
            Write(file, lines);
            return;
        }
        while (lines.Count > 0 && lines[^1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
        for (; n < index - 1; n++) lines.Add("zzz");
        lines.Add(text);
        Write(file, lines);
    }

    private static void Write(string file, List<string> lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
    }

    public string this[int index] => index >= 0 && index < _lines.Length ? _lines[index] : "";
}
