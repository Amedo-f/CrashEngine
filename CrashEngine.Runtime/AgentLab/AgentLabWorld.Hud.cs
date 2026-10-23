using CrashEngine.Runtime.Character;
using CrashEngine.Runtime.Screen;
using CrashEngine.Runtime.Screen.Oleg;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-10-01
public sealed partial class AgentLabWorld
{
    public const ushort OneUpSound = 0x10A;

    public int Wumpa => Progress.Wumpa; // Amedo
    public int HudMode { get => Progress.HudMode; set => Progress.HudMode = value; } // Amedo
    public bool BottomShown { get => (long)Word_00 < 0; set => Word_00 = value ? Word_00 | (1ul << 63) : Word_00 & ~(1ul << 63); } // Amedo
    public int BottomHideTicks { get; set; } // Amedo

    private readonly OlegClock _olegClock = new(); // Amedo
    private Action? _oneUpSound;

    private static int Seconds(float s) => CharacterController.Cvt(s * GameScreen.TicksPerSecond);

    // Amedo
    public void FadeoutScreen_1124D0(uint flags, float duration, float red, float green, float blue)
    {
        switch (flags & 7)
        {
            case 0: Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.FaderGroup], Seconds(duration), 0); break;
            case 1: Oleg.Show_259B20(Oleg.Groups_08[Oleg.FaderGroup], Seconds(duration), 0); break;
            default: return;
        }
        if ((flags & 8) == 0) return;
        Oleg.Fader_13B8.ShownColour_44 = Colour.Make_11BF30(red, green, blue);
        Oleg.Fader_13B8.HiddenColour_40 = 0u;
    }

    private bool TriangleHeld => Pad1?.GetButtonState_2B3548(TriangleButton, false) ?? false; // Amedo

    // Amedo
    private void StepHud_177168(int frameTicks)
    {
        if (GameMode == ModePlay && HudMode == 2) WormTicks -= frameTicks;
        if (GameMode == ModePlay && HudMode == 0 && TriangleHeld)
            Oleg.Show_259B20(Oleg.Groups_08[Oleg.BothCountersGroup], Seconds(0.25f), Seconds(3f));
        _olegClock.Now_04 = GcNowTicks; // Amedo
        _olegClock.FrameTicks_08 = frameTicks;
        Oleg.Update_1714A8(_olegClock, PlayerCharacter, RunState_309718, _oneUpSound ??= PlayOneUp); // Amedo
    }

    private void PlayOneUp() => Sounds?.Play2D(OneUpSound, 0.2f, 0f, "1-up (100 wumpa)");

    // Amedo
    public bool ShowBottomText_176C90(int fade)
    {
        if (GameMode != ModePlay) return false;
        BottomShown = true;
        Oleg.TextLine_120C.Text_08 = "";
        Oleg.Hide_259BC0(ulong.MaxValue, fade, 0);
        Oleg.Show_259B20(Oleg.Groups_08[Oleg.BottomTextGroup], fade, 0);
        return true;
    }

    // Amedo
    public bool HideBottomText_17BA78(int fade)
    {
        if (!BottomShown) return false;
        BottomShown = false;
        BottomHideTicks = fade;
        Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.BottomTextGroup], fade, 0);
        return true;
    }

    // Amedo
    public void DisplayBottomText_120F28(int index, System.Numerics.Vector2 position, System.Numerics.Vector3 colour, float seconds, string? styleKey = null) =>
        SetBottomText(Screen?.AgentLabTexts?[index] ?? "", position, colour, seconds, styleKey);

    // Amedo
    public void DisplayBottomTextInstance_112A20(uint subtype, System.Numerics.Vector2 position, System.Numerics.Vector3 colour, float seconds, string? styleKey = null) =>
        SetBottomText(Screen?.AgentLabTexts?[GameScreen.InstanceLine(subtype)] ?? "", position, colour, seconds, styleKey);

    // Amedo
    public void ClearBottomText_120FF8() => Oleg.TextLine_120C.Text_08 = "";

    private void SetBottomText(string text, System.Numerics.Vector2 position, System.Numerics.Vector3 colour, float seconds, string? styleKey)
    {
        var line = Oleg.TextLine_120C;
        line.Text_08 = text;
        line.StyleKey = styleKey;
        line.Duration_2C = CharacterController.Cvt(seconds * GameScreen.TicksPerSecond);
        line.Colour_14 = Colour.Make_11BF30(colour.X, colour.Y, colour.Z);
        line.Position_18 = position;
        line.Scale_20 = new System.Numerics.Vector2(0.75f, 0.75f);
    }

    // Amedo
    private void ChangeLives_1712D8(int n, bool fromWumpa) => Oleg.AddLives_1712D8(n, fromWumpa, _oneUpSound ??= PlayOneUp);

    // Amedo
    public void AddCrystal_122370()
    {
        bool fresh = Game.GameProgress.MarkCrystal_17A198(ref Progress.LevelWord(Progress.Level));
        Log?.Invoke($"[AgentLab {Now:0.00}s] crystal in level {Progress.Level}{(fresh ? "" : " (already had it)")}");
        Oleg.PickupSprite_310 = 0x14;
        Oleg.Show_259B20(Oleg.Groups_08[Oleg.PickupGroup], Seconds(0.3f), Seconds(1.5f));
        Oleg.PlayPickupEffect_16D0D0(0x25);
    }

    // Amedo
    public void AddGem_11A188(int gem)
    {
        bool fresh = Game.GameProgress.MarkGem_17A1C0(ref Progress.LevelWord(Progress.Level), gem & 0x1F);
        Log?.Invoke($"[AgentLab {Now:0.00}s] gem {gem} in level {Progress.Level}{(fresh ? "" : " (already had it)")}");
        Oleg.PickupSprite_310 = (byte)(gem + 13);
        Oleg.PickupGem_312 = gem & 0xF;
        Oleg.Show_259B20(Oleg.Groups_08[Oleg.PickupGroup], Seconds(0.3f), Seconds(1.5f));
        Oleg.PlayPickupEffect_16D0D0(0x25);
    }

    public int BossHealthNow => Progress.BossHealthNow; // Amedo
    public int BossHealthMax => Progress.BossHealthMax; // Amedo
    public float BossIconWidth => Progress.BossIconWidth_74; // Amedo

    public void EnableBossMode_176D40(float iconWidth, int health, ushort iconOgi)
    {
        Progress.BossIconWidth_74 = iconWidth;
        HudMode = 1;
        Progress.BossHealthMax = health & 7;
        Progress.BossHealthNow = health & 7;
        Log?.Invoke($"[AgentLab {Now:0.00}s] boss mode: health {BossHealthNow}, icon OGI 0x{iconOgi:X4}");
        SetHudIcon_1711A8(0, iconOgi);
        if (GameMode == ModePlay) Oleg.Show_259B20(Oleg.Groups_08[Oleg.BossGroup], Seconds(0.25f), 0);
    }

    public void DamageBoss_1796B8(int n)
    {
        Progress.AddHealth_1796B8(n);
        Log?.Invoke($"[AgentLab {Now:0.00}s] boss health {(n < 0 ? "" : "+")}{n} -> {BossHealthNow}/{BossHealthMax}");
    }

    public void ExitBossMode_17BAD0()
    {
        if (HudMode != 1) return;
        HudMode = 0;
        Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.BossGroup], Seconds(0.25f), 0);
    }

    public int WormTicks { get => Progress.WormTicks_7C; private set => Progress.WormTicks_7C = value; } // Amedo
    public int WormCount => Progress.WormCount; // Amedo
    public int WormTotal => Progress.WormTotal; // Amedo

    public void StartWhackaworm_176E30(int ticks, int target, ushort iconOgi)
    {
        HudMode = 2;
        WormTicks = ticks;
        Progress.WormTotal = target & 0x3F;
        Progress.WormCount = target & 0x3F;
        Log?.Invoke($"[AgentLab {Now:0.00}s] whack-a-worm: {WormCount}/{WormTotal} in {ticks / 9000.0:0.##}s");
        SetHudIcon_1711A8(1, iconOgi);
        if (GameMode == ModePlay) Oleg.Show_259B20(Oleg.Groups_08[Oleg.WormGroup], Seconds(0.25f), 0);
    }

    public void ProgressWhackaworm_179748(int n) => Progress.AddToCount_179748(n);

    public void EndWhackaworm_17BB50()
    {
        if (HudMode != 2) return;
        HudMode = 0;
        Oleg.Hide_259BC0(Oleg.Groups_08[Oleg.WormGroup], Seconds(0.25f), 0);
    }

    public GameFont.Page? OgiIconForTest(ushort ogiId) => BossIcon_1711A8(ogiId);

    // Amedo
    private void SetHudIcon_1711A8(int slot, ushort ogiId)
    {
        if (ogiId == 0xFFFF)
        {
            Oleg.SetHudIcon_1711A8(slot, null);
            return;
        }
        if (BossIcon_1711A8(ogiId) is { } material) Oleg.SetHudIcon_1711A8(slot, material);
    }

    private GameFont.Page? BossIcon_1711A8(ushort ogiId)
    {
        if (ogiId == 0xFFFF) return null;
        foreach (var rm in AllRm2())
        {
            var ogi = rm?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                        ?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION) is { } ogis && ogis.ContainsItem(ogiId)
                ? ogis.GetItem<BaseTwinItem>(ogiId) as PS2AnyOGI : null;
            if (ogi is null || ogi.RigidModelIds.Count == 0) continue;
            foreach (var gfxRm in AllRm2())
            {
                var gfx = gfxRm?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
                if (gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION) is not { } rigids || !rigids.ContainsItem(ogi.RigidModelIds[0])) continue;
                if (rigids.GetItem<PS2AnyRigidModel>(ogi.RigidModelIds[0]) is not { Materials.Count: > 0 } rigid) return null;
                if (gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION)?.GetItem<PS2AnyMaterial>(rigid.Materials[0]) is not { Shaders.Count: > 0 } material) return null;
                uint textureId = material.Shaders[0].TextureId;
                foreach (var texRm in AllRm2())
                    if (texRm?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION)
                            ?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION) is { } textures
                        && textures.ContainsItem(textureId) && textures.GetItem<BaseTwinItem>(textureId) is PS2AnyTexture texture)
                        return GameFont.Decode(texture);
                return null;
            }
        }
        return null;
    }

    private void ResetHud_179E60() => Oleg.Reset_179E60();
}
