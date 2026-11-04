using System.Numerics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed partial class AgentLabWorld
{
    private readonly Dictionary<int, PS2BehaviourCommandsSequence?> _pickupSequences = new();
    private readonly Dictionary<int, PickupAgent.Settings> _pickupSettings = new();
    private readonly List<object> _sphere = new();
    private float _pickupClock;
    private int _pickupPhase;

    public RuntimeParticles? Particles { get; set; }
    public bool ClassicAku { get; set; } // Amedo 2026-10-04
    public float GoldenSeconds { get; set; } = 8f; // Amedo 2026-10-04
    public bool AkuTouchBreak { get; set; } // Amedo 2026-10-04
    public RuntimeSounds? Sounds { get => _sounds; set { _sounds = value; Oleg.Sounds_6F8.Player = value; } } // Amedo
    private RuntimeSounds? _sounds;
    public RuntimeMusic? Music { get; set; }
    public Screen.GameScreen? Screen { get => _screen; set { _screen = value; Oleg = NewOleg(value); if (value is not null) LoadLogo_173B60(); } } // Amedo
    private Screen.GameScreen? _screen;
    public Screen.Oleg.Oleg Oleg { get; private set; } // Amedo
    public int PickupFrame { get; private set; }
    public int WumpaPending { get => Oleg.WumpaPending_311; private set => Oleg.WumpaPending_311 = (byte)value; } // Amedo

    // Amedo
    private Screen.Oleg.Oleg NewOleg(Screen.GameScreen? screen)
    {
        var oleg = new Screen.Oleg.Oleg(screen?.Font, Progress, screen?.CodeTexts, screen?.Icons) { Unimplemented = Unimplemented, Game_3095C8 = this };
        if (screen is not null)
        {
            oleg.LanguageName_309758 = screen.DiscLanguage;
            AudioTrack_309754 = Math.Max(0, Array.FindIndex(PlaySettings.PalLanguages, l => string.Equals(l, screen.DiscLanguage, StringComparison.OrdinalIgnoreCase))); // Amedo
            oleg.ReadPicture = screen.ReadPicture;
        }
        var manager = new Game.SaveManager(oleg, SaveController_594, this, screen?.CodeTexts, SavesDirectory, screen?.IconFileSize ?? 0, Log); // Amedo
        Game.SaveCode.SetTexts_100600(); // Amedo
        SaveManager_309724 = manager; // Amedo
        oleg.Initialise_1700D8(Pad1, screen?.Font, screen?.Font, screen?.FrontendSounds, manager);
        oleg.SetUpParticles_16D368(screen?.ParticlePage_0); // Amedo
        oleg.Sounds_6F8.Player = _sounds;
        if (screen is not null) screen.Oleg = oleg;
        return oleg;
    }
    public int NowTicks => (int)Math.Round(Now * 9000.0);

    private void TickPickupClock(double dt)
    {
        int ticks = (int)Math.Round(dt * 9000.0);
        _pickupClock += ticks * (1f / 9000f);
        if (0.8f <= _pickupClock)
            while (0.8f <= _pickupClock) _pickupClock -= 0.8f;
        PickupFrame = GameMath.Trunc(_pickupClock * 60f);
        PickupFrameFraction = _pickupClock * 60f - PickupFrame; // Amedo 2026-09-30
    }

    public float PickupFrameFraction { get; private set; } // Amedo 2026-09-30

    public int NextPickupPhase()
    {
        int p = _pickupPhase;
        _pickupPhase = p + 8 < 0x30 ? p + 8 : p - 0x28;
        return p;
    }

    private PS2BehaviourCommandsSequence? PickupSequence(int global)
    {
        if (_pickupSequences.TryGetValue(global, out var found)) return found;
        found = null;
        foreach (var rm in AllRm2()) // Amedo 2026-09-28
        {
            var sec = rm?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                        ?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);
            for (int i = 0; sec is not null && i < sec.GetItemsAmount(); i++)
                if (sec.GetItem(i) is PS2BehaviourCommandsSequence s && s.Key == ITwinBehaviourCommandsSequence.InstanceType.Pickup
                    && s.IndexInGlobalStorage == global)
                {
                    found = s;
                    break;
                }
            if (found is not null) break;
        }
        _pickupSequences[global] = found;
        return found;
    }

    public PickupAgent.Settings PickupSettings(int global)
    {
        if (_pickupSettings.TryGetValue(global, out var s)) return s;
        s = default;
        if (PickupSequence(global) is { } seq)
            foreach (var c in seq.Commands)
                if (c is PS2BehaviourCommand cmd && cmd.CommandIndex == 539 && cmd.Arguments.Count >= 4)
                {
                    float r = BitConverter.UInt32BitsToSingle(cmd.Arguments[0]);
                    s = new(r, r * r, BitConverter.UInt32BitsToSingle(cmd.Arguments[1]),
                            BitConverter.UInt32BitsToSingle(cmd.Arguments[2]), cmd.Arguments[3]);
                    break;
                }
        _pickupSettings[global] = s;
        return s;
    }

    public IReadOnlyList<ITwinBehaviourCommand>? PickupPack(int global, int state)
    {
        if (PickupSequence(global) is not { } seq) return null;
        ushort id = (ushort)(PickupAgent.FirstId + state * 2);
        foreach (var kv in seq.BehaviourPacks)
            if (kv.Key == id) return kv.Value.Commands;
        return null;
    }

    public void PickUpWumpa(AgentLabAgent source, int n)
    {
        source.TakeCrateWumpa_122550(n); // Amedo
        WumpaPending = (WumpaPending + n) & 0xFF;
        Log?.Invoke($"[AgentLab {Now:0.00}s] wumpa {(n < 0 ? "" : "+")}{n} by {source.Entity.Name} (pending {WumpaPending})");
    }

    // Amedo 2026-10-04
    public void PickUpHealth_11A270(AgentLabAgent source)
    {
        if (PlayerCharacter is not { } c) return;
        int hp = c.HitPoints + 1;
        if (4 <= hp)
        {
            if (!c.Invincible) Music?.GoldenStart_269818();
            c.SetInvincible(1);
            hp = 3;
        }
        c.HitPoints = hp;
        Log?.Invoke($"[AgentLab {Now:0.00}s] health +1 by {source.Entity.Name} -> {hp}{(c.Invincible ? " (invincible)" : "")}");
    }
}
