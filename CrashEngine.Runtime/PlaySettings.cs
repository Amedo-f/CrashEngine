using CrashEngine.Core;
using System.Text.Json;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashEngine.Runtime;

// Amedo 2026-10-04
public sealed class ProjectPlayValues
{
    public uint? DoubleJumpMask { get; set; }
    public int? AkuAkuInvincibilitySeconds { get; set; }
    public bool AkuAkuTouchBreak { get; set; }
    public uint? AkuAkuGoldenMusicId { get; set; }
    public int? AkuAkuGoldenMusicVolume { get; set; }
    public bool ClassicAkuAku { get; set; }
    public string? Language { get; set; }
    public string? DiscLanguage { get; set; } // Amedo
    public bool CratesReceiveShadows { get; set; } // Amedo

    public static ProjectPlayValues From(object? project) =>
        project is null ? new() : JsonSerializer.Deserialize<ProjectPlayValues>(JsonSerializer.Serialize(project, project.GetType())) ?? new();

    public static ProjectPlayValues FromTson(string? tson)
    {
        if (tson is null || !File.Exists(tson)) return new();
        try { return JsonSerializer.Deserialize<ProjectPlayValues>(File.ReadAllText(tson)) ?? new(); }
        catch (JsonException) { return new(); }
    }
}

// Amedo 2026-10-04
public sealed class PlaySettings
{
    public const string NtscExe = "SLUS_209.09";
    public const string PalExe = "SLES_525.68"; // Amedo
    public const string AmericanLanguage = "American"; // Amedo
    public static readonly string[] PalLanguages = ["English", "French", "German", "Spanish", "Italian"]; // Amedo
    public const uint DefaultJumpRules = 0x44;
    public const int DefaultGoldenSeconds = 8;
    public const int InfiniteGoldenSeconds = 0;
    public const int MaxGoldenSeconds = 255;
    public const int DefaultGoldenMusicVolume = 75;
    public const uint InfiniteGoldenBits = 0x49740000u;

    private const uint CreateDamageCommandIndex = 514;
    private const uint TriggerBalancedCrateFallingCommandIndex = 519;
    private const int DamageAmountArgIndex = 7;
    private const uint ClassicBlastAmount = 0x00000008;
    private const uint CrateExplodeBehaviourId = 0x002F;

    public uint JumpRules { get; init; } = DefaultJumpRules;
    public bool ClassicAku { get; init; }
    public float GoldenSeconds { get; init; } = DefaultGoldenSeconds;
    public bool AkuTouchBreak { get; init; }
    public int GoldenMusic { get; init; } = -1;
    public float GoldenMusicVolume { get; init; } = DefaultGoldenMusicVolume / 100f;
    public string? MusicDirectory { get; init; }
    public string? LanguageDirectory { get; init; }
    public string DiscLanguage { get; init; } = AmericanLanguage; // Amedo
    public string? SavesDirectory { get; init; } // Amedo
    public bool BootStraight { get; init; } // Amedo

    public static PlaySettings Resolve(ProjectPlayValues project, string disc, string? projectFolder, PS2AnyTwinsanityRM2? globalRm2, string? savesDirectory = null) // Amedo
    {
        bool ntsc = File.Exists(GamePaths.FindOnDisk(disc, NtscExe)); // Amedo 2026-11-20
        bool patchable = ntsc || File.Exists(GamePaths.FindOnDisk(disc, PalExe)); // Amedo 2026-11-20
        string? language = null;
        if (projectFolder is not null && !string.IsNullOrWhiteSpace(project.Language) && Directory.Exists(Path.Combine(projectFolder, "Language", project.Language)))
            language = Path.Combine(projectFolder, "Language", project.Language);
        return new PlaySettings
        {
            JumpRules = patchable ? project.DoubleJumpMask ?? DefaultJumpRules : DefaultJumpRules, // Amedo
            ClassicAku = patchable && (ClassicFromData(globalRm2) ?? project.ClassicAkuAku),
            GoldenSeconds = Seconds(patchable ? project.AkuAkuInvincibilitySeconds ?? DefaultGoldenSeconds : DefaultGoldenSeconds),
            AkuTouchBreak = patchable && project.AkuAkuTouchBreak,
            GoldenMusic = patchable && project.AkuAkuGoldenMusicId is { } id ? (int)id : -1,
            GoldenMusicVolume = Math.Clamp(project.AkuAkuGoldenMusicVolume ?? DefaultGoldenMusicVolume, 1, 100) / 100f,
            MusicDirectory = GamePaths.FindOnDisk(disc, "Crash6"), // Amedo 2026-11-20
            LanguageDirectory = language,
            DiscLanguage = ntsc ? AmericanLanguage : Region.Pal.Language.DiscLanguage(project.DiscLanguage), // Amedo 2026-11-11
            SavesDirectory = savesDirectory ?? (projectFolder is null ? null : Path.Combine(projectFolder, "Saves")), // Amedo
            BootStraight = patchable && BootStraightPatched(disc, ntsc), // Amedo
        };
    }

    public static bool IsPalDisc(string disc) =>
        !File.Exists(GamePaths.FindOnDisk(disc, NtscExe)) && File.Exists(GamePaths.FindOnDisk(disc, PalExe)); // Amedo 2026-11-20

    // Amedo
    private static bool BootStraightPatched(string disc, bool ntsc)
    {
        string path = GamePaths.FindOnDisk(disc, ntsc ? NtscExe : PalExe); // Amedo 2026-11-20
        long offset = ntsc ? 0x783F4 : 0x78504;
        uint patched = ntsc ? 0x0C0B5A03u : 0x0C0B5AE3u;
        try
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < offset + 4) return false;
            fs.Seek(offset, SeekOrigin.Begin);
            Span<byte> word = stackalloc byte[4];
            fs.ReadExactly(word);
            return BitConverter.ToUInt32(word) == patched;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // Amedo
    public static string DetectDiscLanguage(string? crash6Directory) =>
        crash6Directory is null || File.Exists(GamePaths.FindOnDisk(crash6Directory, AmericanLanguage.ToUpperInvariant() + ".MH")) ? AmericanLanguage : PalLanguages[0];

    private static float Seconds(int s) =>
        s == InfiniteGoldenSeconds ? BitConverter.UInt32BitsToSingle(InfiniteGoldenBits) : Math.Clamp(s, 1, MaxGoldenSeconds);

    public static bool? ClassicFromData(PS2AnyTwinsanityRM2? globalRm2)
    {
        var graph = globalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION)
            ?.GetItem<PS2BehaviourGraph>(CrateExplodeBehaviourId);
        var bigBlast = graph is null ? null : FindBigBlast(graph);
        return bigBlast is null ? null : bigBlast.Arguments[DamageAmountArgIndex] == ClassicBlastAmount;
    }

    public static ITwinBehaviourCommand? FindBigBlast(PS2BehaviourGraph graph)
    {
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
            {
                if (!body.Commands.Any(c => c.CommandIndex == TriggerBalancedCrateFallingCommandIndex)) continue;
                var dmg = body.Commands.FirstOrDefault(c => c.CommandIndex == CreateDamageCommandIndex && c.Arguments.Count > DamageAmountArgIndex);
                if (dmg is not null) return dmg;
            }
        return null;
    }
}
