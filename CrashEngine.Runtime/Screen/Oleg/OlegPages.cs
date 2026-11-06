using System.Numerics;
using CrashEngine.Runtime.Character;
using CrashEngine.Runtime.Game;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public interface IOlegGame
{
    int GameMode { get; }
    int AutosaveState { get; }
    bool Vibration_30971C { get; set; }
    Vector2 ScreenOffset_309728 { get; set; }
    float SoundLevel_1E60A8(int group);
    void SetSoundLevel_1E5FE8(float level, int group);
    int OutputMode_309AC8 { get; }
    void SetOutputMode_1E6190(int mode);
    CharacterPad? Pad_34 { get; }
    void RequestMovie_17B868(int fadeTicks, int movie);
    bool RequestGallery_17BBD0(int fadeTicks, string path, int first, int last);
    bool RequestRestart_17B640(int kind);
    bool QuitToTitle_17B828();
    bool DisableAutosave_17BCB0();
    bool SaveFromPause_17BF88();
    bool LoadFromPause_17BDD8();
    bool NewGame_17BEB0();
    void Draw_177908(OverlayQueue overlay);
}

// Amedo
public class OlegPage : MenuPage
{
    public readonly Oleg Oleg_3095C8;

    // Amedo
    public OlegPage(Oleg oleg, int titleText, MenuPage? parent, GameTexts? texts) : base(titleText, 1)
    {
        Oleg_3095C8 = oleg;
        Texts = texts;
        Parent_18 = parent;
    }

    // Amedo
    public OlegPage(Oleg oleg, string? title, MenuPage? parent, GameTexts? texts) : base(title, 1)
    {
        Oleg_3095C8 = oleg;
        Texts = texts;
        Parent_18 = parent;
    }

    protected static int HalfSecond => GameMath.Trunc(Widget.TicksPerSecond * 0.5f);

    // Amedo
    protected void ShowPauseMenu()
    {
        Oleg_3095C8.Hide_259BC0(Oleg_3095C8.Groups_08[Oleg.AllButOverlaysGroup], HalfSecond, 0);
        Oleg_3095C8.Show_259B20(Oleg_3095C8.Groups_08[Oleg.PauseGroup], HalfSecond, 0);
    }
}

// Amedo
public sealed class ActionItem : LinkItem
{
    public readonly Oleg Oleg_3095C8;
    public int Action_14;

    // Amedo
    public ActionItem(Oleg oleg, int text, int id, int action) : base(text, id, null, 1)
    {
        Oleg_3095C8 = oleg;
        Action_14 = action;
    }

    // Amedo
    public override MenuPage? Activate(int col, MenuPage page)
    {
        var oleg = Oleg_3095C8;
        int half = GameMath.Trunc(Widget.TicksPerSecond * 0.5f);
        switch (Action_14)
        {
            case 0: ShowGroup(oleg, Oleg.ScreenPositionGroup, half); break;
            case 1: ShowGroup(oleg, Oleg.OptionsGroup, half); break;
            case 2: ShowGroup(oleg, Oleg.QuitGroup, half); break;
            case 3: oleg.Game_3095C8?.QuitToTitle_17B828(); break;
            case 4: ShowGroup(oleg, Oleg.DisableAutosaveGroup, half); break;
            case 5: oleg.Game_3095C8?.DisableAutosave_17BCB0(); break;
        }
        return base.Activate(col, page);
    }

    private static void ShowGroup(Oleg oleg, int group, int half)
    {
        oleg.Hide_259BC0(oleg.Groups_08[Oleg.AllButOverlaysGroup], half, 0);
        oleg.Show_259B20(oleg.Groups_08[group], half, 0);
    }
}

// Amedo
public sealed class PausePage : OlegPage
{
    public const int OptionsItem = 0x20, SaveItem = 1, LoadItem = 2, DisableAutosaveItem = 0xA, QuitItem = 0xB, ResumeItem = 0xC;

    public readonly SaveGamePage Save_50;
    public readonly LoadGamePage Load_A4;

    // Amedo
    public PausePage(Oleg oleg, GameTexts? texts) : base(oleg, 0x18, Exit_3B6B50, texts)
    {
        Save_50 = new SaveGamePage(oleg, texts);
        Load_A4 = new LoadGamePage(oleg, texts);
        var options = new ActionItem(oleg, 0xB, OptionsItem, 1);
        var save = new LinkItem(0x19, SaveItem, Save_50, 1);
        var load = new LinkItem(0xA, LoadItem, Load_A4, 1);
        var disable = new ActionItem(oleg, 0x16, DisableAutosaveItem, 4);
        var quit = new ActionItem(oleg, 0x1A, QuitItem, 2);
        var resume = new BackItem(0x1B, ResumeItem, Exit_3B6B50, 1);
        Add_25B710(options);
        Add_25B710(save);
        Add_25B710(load);
        Add_25B710(disable);
        Add_25B710(quit);
        Add_25B710(resume);
        SetFirstItem_25B9C0(ResumeItem);
    }

    // Amedo
    public override void Frame(int col, MenuInput input, MenuSounds? sounds)
    {
        var item = Find_2573E0(DisableAutosaveItem)!;
        int state = Oleg_3095C8.Game_3095C8?.AutosaveState ?? 0;
        item.Shown_08 = (byte)(state is 1 or 2 ? 0xFF : 0);
    }
}

// Amedo
public sealed class MainMenuPage : OlegPage
{
    public const int NewGameItem = 0, LoadItem = 2, OptionsItem = 0x20;

    public readonly NewGamePage NewGame_50;
    public readonly LoadGamePage Load_A4;

    // Amedo
    public MainMenuPage(Oleg oleg, GameTexts? texts) : base(oleg, "crash twinsanity", Exit_3B6B50, texts)
    {
        NewGame_50 = new NewGamePage(oleg, texts);
        Load_A4 = new LoadGamePage(oleg, texts);
        var newGame = new LinkItem(9, NewGameItem, NewGame_50, 1);
        var load = new LinkItem(10, LoadItem, Load_A4, 1);
        var options = new ActionItem(oleg, 0xB, OptionsItem, 1);
        Add_25B710(newGame);
        Add_25B710(load);
        Add_25B710(options);
    }
}

// Amedo
public sealed class NewGamePage : OlegPage
{
    // Amedo
    public NewGamePage(Oleg oleg, GameTexts? texts) : base(oleg, 9, null, texts) { }

    // Amedo
    public override void Entered(int col, int mode) => Oleg_3095C8.Game_3095C8?.NewGame_17BEB0();
}

// Amedo
public sealed class SaveGamePage : OlegPage
{
    // Amedo
    public SaveGamePage(Oleg oleg, GameTexts? texts) : base(oleg, 0x19, null, texts) { }

    // Amedo
    public override void Entered(int col, int mode) => Oleg_3095C8.Game_3095C8?.SaveFromPause_17BF88();
}

// Amedo
public sealed class LoadGamePage : OlegPage
{
    // Amedo
    public LoadGamePage(Oleg oleg, GameTexts? texts) : base(oleg, 0xA, null, texts) { }

    // Amedo
    public override void Entered(int col, int mode) => Oleg_3095C8.Game_3095C8?.LoadFromPause_17BDD8();
}

// Amedo
public sealed class SaveSlotsPage : SaveSlotsPageBase
{
    public readonly SaveSlotWidget[] Widgets_58;
    public readonly RingWidget[] Rings_5C;

    // Amedo
    public SaveSlotsPage(Oleg oleg, SaveManager owner, MenuWidget menu) : base(owner)
    {
        var after = menu.Next_1C;
        int count = owner.Device_14.SlotCount;
        Widgets_58 = new SaveSlotWidget[count];
        Rings_5C = new RingWidget[count];
        Widget previous = menu;
        for (int i = 0; i < count; i++)
        {
            var summary = owner.Banks_580[i].Summary_2C;
            var item = new SaveChoiceItem(0, 0x100 + i, SaveChoiceItem.Accept, owner);
            bool even = (i & 1) == 0;
            var position = new Vector2(even ? 0.3f : 0.7f, i * 0.09f + BitConverter.Int32BitsToSingle(0x3E851EB8));
            var offset = new Vector2(even ? -1f : 1f, 0f);
            var ring = new RingWidget(4, 1, 0x20, null, 0.5f);
            var widget = new SaveSlotWidget(this, item, oleg, summary);
            Rings_5C[i] = ring;
            Widgets_58[i] = widget;
            ring.SlideIn_25A7C8(Colour.GetColor_11BF10(15), position, Vector2.One, offset);
            widget.SlideIn_25A7C8(Colour.GetColor_11BF10(15), position, Vector2.One, offset);
            oleg.AddPanelRings_16A288(new Vector2(0.2f, 0.075f), ring);
            previous.Next_1C = ring;
            ring.Next_1C = widget;
            previous = widget;
            Add_25B710(item);
        }
        previous.Next_1C = after;
        AddCancelItems_2A77A0();
    }
}

// Amedo
public sealed class QuitPage : OlegPage
{
    // Amedo
    public QuitPage(Oleg oleg, MenuPage? parent, GameTexts? texts) : base(oleg, 0x45, parent, texts)
    {
        var yes = new ActionItem(oleg, 3, 0xB, 3);
        var no = new BackItem(2, 0x1E, parent, 1);
        Add_25B710(yes);
        Add_25B710(no);
        SetFirstItem_25B9C0(no.Id);
    }

    // Amedo
    public override bool Back(int col)
    {
        ShowPauseMenu();
        return true;
    }
}

// Amedo
public sealed class DisableAutosavePage : OlegPage
{
    // Amedo
    public DisableAutosavePage(Oleg oleg, MenuPage? parent, GameTexts? texts) : base(oleg, 0x60, parent, texts)
    {
        var yes = new ActionItem(oleg, 3, 0xA, 5);
        var no = new BackItem(2, 0x1E, parent, 1);
        Add_25B710(yes);
        Add_25B710(no);
        SetFirstItem_25B9C0(no.Id);
    }

    // Amedo
    public override bool Back(int col)
    {
        ShowPauseMenu();
        return true;
    }
}

// Amedo
public sealed class NoticePage : OlegPage
{
    // Amedo
    public NoticePage(Oleg oleg, MenuPage? parent, GameTexts? texts) : base(oleg, "", parent, texts) => Add_25B710(new LinkItem(5, 0xC, Exit_3B6B50, 1));
}

// Amedo
public sealed class OptionsPage : OlegPage
{
    public readonly GameOptionsPage Game_50;
    public readonly GraphicsOptionsPage Graphics_A0;
    public readonly SoundOptionsPage Sound_F4;

    // Amedo
    public OptionsPage(Oleg oleg, MenuPage? parent, GameTexts? texts) : base(oleg, 0xB, parent, texts)
    {
        Game_50 = new GameOptionsPage(oleg, this, texts);
        Graphics_A0 = new GraphicsOptionsPage(oleg, this, texts);
        Sound_F4 = new SoundOptionsPage(oleg, this, texts);
        Add_25B710(new LinkItem(0xD, 6, Graphics_A0, 1));
        Add_25B710(new LinkItem(0xE, 7, Sound_F4, 1));
        Add_25B710(new LinkItem(0xF, 8, Game_50, 1));
        Add_25B710(new BackItem(4, 0x1F, parent, 1));
    }

    // Amedo
    public override bool Back(int col)
    {
        var oleg = Oleg_3095C8;
        oleg.Hide_259BC0(oleg.Groups_08[Oleg.AllButOverlaysGroup], HalfSecond, 0);
        int group = oleg.Game_3095C8?.GameMode == Oleg.MainMenuMode ? Oleg.MainMenuGroup : Oleg.PauseGroup;
        oleg.Show_259B20(oleg.Groups_08[group], HalfSecond, 0);
        return true;
    }
}

// Amedo
public sealed class GameOptionsPage : OlegPage
{
    public const int VibrationItem = 0x1C;

    // Amedo
    public GameOptionsPage(Oleg oleg, MenuPage parent, GameTexts? texts) : base(oleg, 0xF, parent, texts)
    {
        Add_25B710(new ToggleItem(0x15, VibrationItem, 2, 3, true, 0, 1));
        Add_25B710(new BackItem(4, 0x1E, parent, 1));
    }

    // Amedo
    public override void Entered(int col, int mode) =>
        Find_2573E0(VibrationItem)!.SetFlag_25BD40(col, Oleg_3095C8.Game_3095C8?.Vibration_30971C == true ? 1 : 0);

    // Amedo
    public override void Frame(int col, MenuInput input, MenuSounds? sounds)
    {
        Find_2573E0(VibrationItem)!.GetFlag_25BD68(col, out bool on);
        if (Oleg_3095C8.Game_3095C8 is { } game) game.Vibration_30971C = on;
    }
}

// Amedo
public sealed class GraphicsOptionsPage : OlegPage
{
    public const int CentreItem = 0x17, WidescreenItem = 0x18;
    private readonly PcGraphicsOptions _pc = new(); // Amedo

    // Amedo
    public GraphicsOptionsPage(Oleg oleg, MenuPage parent, GameTexts? texts) : base(oleg, 0xD, parent, texts)
    {
        Add_25B710(new ActionItem(oleg, 0x11, CentreItem, 0));
        Add_25B710(new ToggleItem(0x10, WidescreenItem, 0, 1, true, 0, 1));
        _pc.AddTo(this); // Amedo
        Add_25B710(new BackItem(4, 0x1E, parent, 1));
    }

    // Amedo
    public override void Entered(int col, int mode)
    {
        Find_2573E0(WidescreenItem)!.SetFlag_25BD40(col, OverlayQueue.Widescreen_30A744 ? 1 : 0);
        _pc.Load(col);
    }

    // Amedo
    public override void Frame(int col, MenuInput input, MenuSounds? sounds)
    {
        Find_2573E0(WidescreenItem)!.GetFlag_25BD68(col, out bool on);
        OverlayQueue.Widescreen_30A744 = on;
        _pc.Store(col); // Amedo
    }
}

// Amedo
public sealed class SoundOptionsPage : OlegPage
{
    public const int EffectsItem = 0x19, MusicItem = 0x1A, OutputItem = 0x1B;

    // Amedo
    public SoundOptionsPage(Oleg oleg, MenuPage parent, GameTexts? texts) : base(oleg, 0xE, parent, texts)
    {
        var effects = new ValueItem(0x12, EffectsItem, 0, 10, false, 10, 1);
        var music = new ValueItem(0x13, MusicItem, 0, 10, false, 10, 1);
        var output = new ChoiceItem(0x14, OutputItem, 3, true, 0, 1);
        var back = new BackItem(4, 0x1E, parent, 1);
        output.SetChoiceText_25BFE0(0, 6);
        output.SetChoiceText_25BFE0(1, 7);
        output.SetChoiceText_25BFE0(2, 8);
        Add_25B710(effects);
        Add_25B710(music);
        Add_25B710(output);
        Add_25B710(back);
    }

    // Amedo
    public override void Entered(int col, int mode)
    {
        var game = Oleg_3095C8.Game_3095C8;
        float effects = game?.SoundLevel_1E60A8(0) ?? 1f, music = game?.SoundLevel_1E60A8(2) ?? 1f;
        Find_2573E0(EffectsItem)!.SetInt(col, GameMath.Trunc(effects * 10f + 0.5f));
        Find_2573E0(MusicItem)!.SetInt(col, GameMath.Trunc(music * 10f + 0.5f));
        Find_2573E0(OutputItem)!.SetInt(col, game?.OutputMode_309AC8 ?? 1);
    }

    // Amedo
    public override void Frame(int col, MenuInput input, MenuSounds? sounds)
    {
        var effects = Find_2573E0(EffectsItem)!;
        var music = Find_2573E0(MusicItem)!;
        int value;
        if (Item_25A238(Selected_0C[col])!.Id == MusicItem) music.GetInt(col, out value);
        else effects.GetInt(col, out value);
        float level = value * 0.1f;
        if (Oleg_3095C8.Game_3095C8 is not { } game) return;
        game.SetSoundLevel_1E5FE8(level, 0);
        game.SetSoundLevel_1E5FE8(level, 1);
        game.SetSoundLevel_1E5FE8(level, 3);
    }

    // Amedo
    public override void Left(int col, int mode)
    {
        Find_2573E0(EffectsItem)!.GetInt(col, out int e);
        Find_2573E0(MusicItem)!.GetInt(col, out int m);
        Find_2573E0(OutputItem)!.GetInt(col, out int output);
        if (Oleg_3095C8.Game_3095C8 is not { } game) return;
        float effects = e * 0.1f, music = m * 0.1f;
        game.SetSoundLevel_1E5FE8(effects, 0);
        game.SetSoundLevel_1E5FE8(effects, 1);
        game.SetSoundLevel_1E5FE8(effects, 3);
        game.SetSoundLevel_1E5FE8(music, 2);
        game.SetOutputMode_1E6190(output);
    }
}

// Amedo
public sealed class ScreenPositionPage : OlegPage
{
    // Amedo
    public ScreenPositionPage(Oleg oleg, MenuPage? parent, GameTexts? texts) : base(oleg, "", parent, texts) =>
        Add_25B710(new BackItem(5, 0x1F, parent, 1));

    // Amedo
    public override void Frame(int col, MenuInput input, MenuSounds? sounds)
    {
        if (Oleg_3095C8.Game_3095C8 is not { } game) return;
        var p = game.ScreenOffset_309728;
        if (input.Has_25BBA8(MenuInput.Left, false)) p.X -= 0.015f;
        else if (input.Has_25BBA8(MenuInput.Right, false)) p.X += 0.015f;
        if (input.Has_25BBA8(MenuInput.Up, false)) p.Y -= 0.03f;
        else if (input.Has_25BBA8(MenuInput.Down, false)) p.Y += 0.03f;
        game.ScreenOffset_309728 = p;
    }

    // Amedo
    public override bool Back(int col)
    {
        var oleg = Oleg_3095C8;
        oleg.Hide_259BC0(oleg.Groups_08[Oleg.AllButOverlaysGroup], HalfSecond, 0);
        oleg.Show_259B20(oleg.Groups_08[Oleg.OptionsGroup], HalfSecond, 0);
        return true;
    }
}

// Amedo
public sealed class LevelsPage : OlegPage
{
    public const int Levels = 5;

    public readonly LevelWidget?[] Widgets_50 = new LevelWidget?[Levels];
    public readonly RingWidget?[] Rings_64 = new RingWidget?[Levels];

    // Amedo
    public LevelsPage(Oleg oleg, int index, MenuWidget menu, GameTexts? texts) : base(oleg, "", Exit_3B6B50, texts)
    {
        var after = menu.Next_1C;
        var items = new MenuItem?[Levels];
        float y0, dy, even, odd;
        int[]? ids = index switch
        {
            0 => [0, 1, 3, 4],
            1 => [6, 7, 9, 10],
            2 => [0xD, 0xF, 0x11, 0x12],
            3 => [0x14, 0x15, 0x16, 0x17],
            _ => null,
        };
        if (ids is null)
        {
            even = 0.4f;
            odd = 0.6f;
            y0 = 0.2f;
            dy = 0.125f;
        }
        else
        {
            y0 = dy = 0.15f;
            (even, odd) = index is 0 or 2 ? (0.6f, 0.4f) : (0.4f, 0.6f);
            for (int i = 0; i < ids.Length; i++) items[i] = new LinkItem("", ids[i], null, 1);
        }
        Widget previous = menu;
        for (int i = 0; i < Levels; i++)
        {
            if (items[i] is not { } item)
            {
                Rings_64[i] = null;
                Widgets_50[i] = null;
                continue;
            }
            var position = new Vector2((i & 1) == 0 ? even : odd, y0 + dy * i);
            var offset = new Vector2(position.X < 0.5f ? -1f : 1f, 0f);
            var ring = new RingWidget(4, 1, 0x20, null, 0.5f);
            var widget = new LevelWidget(this, item, oleg);
            Rings_64[i] = ring;
            Widgets_50[i] = widget;
            ring.SlideIn_25A7C8(Colour.GetColor_11BF10(15), position, Vector2.One, offset);
            widget.SlideIn_25A7C8(Colour.GetColor_11BF10(15), position, new Vector2(BitConverter.Int32BitsToSingle(0x3F19999A), 0.1f), offset);
            oleg.AddPanelRings_16A288(new Vector2(0.3f, 0.05f), ring);
            previous.Next_1C = ring;
            ring.Next_1C = widget;
            previous = widget;
            Add_25B710(item);
        }
        previous.Next_1C = after;
    }
}

// Amedo
public sealed class MovieItem : LinkItem
{
    public readonly Oleg Oleg_3095C8;
    public int Movie_14;

    // Amedo
    public MovieItem(Oleg oleg, int text, int id, int movie) : base(text, id, null, 1)
    {
        Oleg_3095C8 = oleg;
        Movie_14 = movie;
    }

    // Amedo
    public MovieItem(Oleg oleg, string name, int id, int movie) : base(name, id, null, 1)
    {
        Oleg_3095C8 = oleg;
        Movie_14 = movie;
    }

    // Amedo
    public override MenuPage? Activate(int col, MenuPage page)
    {
        Oleg_3095C8.Game_3095C8?.RequestMovie_17B868(GameMath.Trunc(Widget.TicksPerSecond * 0.5f), Movie_14);
        return null;
    }
}

// Amedo
public sealed class PictureItem : LinkItem
{
    public readonly Oleg Oleg_3095C8;
    public byte First_14, Last_15;
    public string Path_18;

    // Amedo
    public PictureItem(Oleg oleg, int text, int id, string path, int first, int last) : base(text, id, null, 1)
    {
        Oleg_3095C8 = oleg;
        Path_18 = path;
        First_14 = (byte)first;
        Last_15 = (byte)last;
    }

    // Amedo
    public PictureItem(Oleg oleg, string name, int id, string path, int first, int last) : base(name, id, null, 1)
    {
        Oleg_3095C8 = oleg;
        Path_18 = path;
        First_14 = (byte)first;
        Last_15 = (byte)last;
    }

    // Amedo
    public override MenuPage? Activate(int col, MenuPage page)
    {
        Oleg_3095C8.Game_3095C8?.RequestGallery_17BBD0(GameMath.Trunc(Widget.TicksPerSecond * 0.5f), Path_18, First_14, Last_15);
        return null;
    }
}

// Amedo
public class GemExtrasPage : OlegPage
{
    public const int Entries = 16;

    public readonly int Gem_50;

    // Amedo
    protected GemExtrasPage(Oleg oleg, int gem, MenuPage owner, GameTexts? texts) : base(oleg, gem + 0x59, owner, texts) => Gem_50 = gem;

    protected bool Unlocked(int level) => (Oleg_3095C8.Progress_390.LevelWords_34[level] & 0xFF & (1u << Gem_50)) != 0;

    // Amedo
    public override void Entered(int col, int mode)
    {
        for (int i = 0; i < Entries; i++) Find_2573E0(i)!.Selectable_09 = (byte)(Unlocked(i) ? 0xFF : 0);
    }
}

// Amedo
public sealed class BossesPage : GemExtrasPage
{
    // Amedo
    public BossesPage(Oleg oleg, int gem, MenuPage owner, GameTexts? texts) : base(oleg, gem, owner, texts)
    {
        var back = new BackItem(4, 0x1F, owner, 1);
        for (int i = 0; i < Entries; i++) Add_25B710(new PictureItem(oleg, 0x62 + i, i, @"Extras\Bosses\Boss", i + 1, i + 1));
        Add_25B710(back);
    }
}

// Amedo
public sealed class ConceptPage : GemExtrasPage
{
    // Amedo
    public ConceptPage(Oleg oleg, int gem, MenuPage owner, GameTexts? texts) : base(oleg, gem, owner, texts)
    {
        var back = new BackItem(4, 0x1F, owner, 1);
        for (int i = 0; i < Entries; i++) Add_25B710(new PictureItem(oleg, 0x72 + i, i, @"Extras\Concept\Concept", 1 + 2 * i, 2 + 2 * i));
        Add_25B710(back);
    }
}

// Amedo
public sealed class EnemiesPage : GemExtrasPage
{
    // Amedo
    public EnemiesPage(Oleg oleg, int gem, MenuPage owner, GameTexts? texts) : base(oleg, gem, owner, texts)
    {
        var back = new BackItem(4, 0x1F, owner, 1);
        for (int i = 0; i < Entries; i++) Add_25B710(new PictureItem(oleg, 0x82 + i, i, @"Extras\Enemies\Enemy", 1 + 2 * i, 2 + 2 * i));
        Add_25B710(back);
    }
}

// Amedo
public sealed class MoviesPage : GemExtrasPage
{
    private static readonly int[] Movies_2E6B80 = [14, 15, 16, 17, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

    // Amedo
    public MoviesPage(Oleg oleg, int gem, MenuPage owner, GameTexts? texts) : base(oleg, gem, owner, texts)
    {
        var back = new BackItem(4, 0x1F, owner, 1);
        for (int i = 0; i < Entries; i++) Add_25B710(new MovieItem(oleg, 0x92 + i, i, Movies_2E6B80[i]));
        Add_25B710(back);
    }

    // Amedo
    public override void Entered(int col, int mode)
    {
        var progress = Oleg_3095C8.Progress_390;
        for (int i = 0; i < Entries; i++)
        {
            bool open = Unlocked(i);
            if (0xC < i) open = open && 0x17 < ((progress.Word_04 >> 21) & 0x1F);
            Find_2573E0(i)!.Selectable_09 = (byte)(open ? 0xFF : 0);
        }
    }
}

// Amedo
public sealed class StoryboardsPage : GemExtrasPage
{
    private static readonly (string Path, int Count)[] Storyboards_2E6BC0 =
    [
        (@"Extras\Storyboards\02-Jungle\Jungle", 8), (@"Extras\Storyboards\01-NSanity\NSanity", 11), (@"Extras\Storyboards\03-Cavern\Cavern", 14),
        (@"Extras\Storyboards\04-Totem\Totem", 15), (@"Extras\Storyboards\06-IceClimb\IceClimb", 17), (@"Extras\Storyboards\05-IceHub\IceHub", 15),
        (@"Extras\Storyboards\07-Slip\Slip", 9), (@"Extras\Storyboards\08-HighSeas\HighSeas", 14), (@"Extras\Storyboards\09-Academy\Academy", 8),
        (@"Extras\Storyboards\10-Boiler\Boiler", 7), (@"Extras\Storyboards\11-Classroom\Classroom", 9), (@"Extras\Storyboards\12-Rooftop\Rooftop", 7),
        (@"Extras\Storyboards\13-Twinsanity\Twinsanity", 5), (@"Extras\Storyboards\14-Rockslide\Rockslide", 6), (@"Extras\Storyboards\15-Pursuit\Pursuit", 6),
        (@"Extras\Storyboards\16-AntAgony\AntAgony", 10),
    ];

    // Amedo
    public StoryboardsPage(Oleg oleg, int gem, MenuPage owner, GameTexts? texts) : base(oleg, gem, owner, texts)
    {
        var back = new BackItem(4, 0x1F, owner, 1);
        for (int i = 0; i < Entries; i++) Add_25B710(new PictureItem(oleg, 0x47 + i, i, Storyboards_2E6BC0[i].Path, 1, Storyboards_2E6BC0[i].Count));
        Add_25B710(back);
    }
}

// Amedo
public sealed class UnseenPage : GemExtrasPage
{
    // Amedo
    public UnseenPage(Oleg oleg, int gem, MenuPage owner, GameTexts? texts) : base(oleg, gem, owner, texts)
    {
        var back = new BackItem(4, 0x1F, owner, 1);
        for (int i = 0; i < Entries; i++) Add_25B710(new PictureItem(oleg, 0xA2 + i, i, @"Extras\Unseen\Unseen", 1 + 3 * i, 3 + 3 * i));
        Add_25B710(back);
    }
}

// Amedo
public sealed class ExtrasPage : OlegPage
{
    public const int HundredItem = 0x14, McDonaldsItem = 0x16;

    public readonly BossesPage Bosses_50;
    public readonly ConceptPage Concept_A4;
    public readonly EnemiesPage Enemies_F8;
    public readonly MoviesPage Movies_14C;
    public readonly StoryboardsPage Storyboards_1A0;
    public readonly UnseenPage Unseen_1F4;
    public bool Unlocked_248;

    // Amedo
    public ExtrasPage(Oleg oleg, GameTexts? texts) : base(oleg, 0x17, Exit_3B6B50, texts)
    {
        Bosses_50 = new BossesPage(oleg, 0, this, texts);
        Concept_A4 = new ConceptPage(oleg, 3, this, texts);
        Enemies_F8 = new EnemiesPage(oleg, 2, this, texts);
        Movies_14C = new MoviesPage(oleg, 1, this, texts);
        Storyboards_1A0 = new StoryboardsPage(oleg, 4, this, texts);
        Unseen_1F4 = new UnseenPage(oleg, 5, this, texts);
        Unlocked_248 = false;
        var hundred = new MovieItem(oleg, 0x61, HundredItem, 0x12);
        var spyro = new MovieItem(oleg, "spyro trailer", 0x15, 0x13);
        var mcdonalds = new PictureItem(oleg, "mcdonalds extras", McDonaldsItem, @"Extras\McDonalds", 1, 1);
        var back = new BackItem(0x1B, 0x1E, Exit_3B6B50, 1);
        Add_25B710(new LinkItem(0x59, 0xE, Bosses_50, 1));
        Add_25B710(new LinkItem(0x5A, 0xF, Movies_14C, 1));
        Add_25B710(new LinkItem(0x5B, 0x10, Enemies_F8, 1));
        Add_25B710(new LinkItem(0x5C, 0x11, Concept_A4, 1));
        Add_25B710(new LinkItem(0x5D, 0x12, Storyboards_1A0, 1));
        Add_25B710(new LinkItem(0x5E, 0x13, Unseen_1F4, 1));
        Add_25B710(hundred);
        Add_25B710(spyro);
        Add_25B710(mcdonalds);
        Add_25B710(back);
    }

    // Amedo
    public override void Entered(int col, int mode)
    {
        var progress = Oleg_3095C8.Progress_390;
        int percent = progress.Percent_1795E8();
        var hundred = Find_2573E0(HundredItem)!;
        for (int gem = 0; gem < GameProgress.Gems; gem++)
            Find_2573E0(0xE + gem)!.Shown_08 = (byte)(progress.GemsFound_1795A8(gem) != 0 ? 0xFF : 0);
        if (percent == 100)
        {
            hundred.Shown_08 = 0xFF;
            hundred.Word_04 = (hundred.Word_04 & 0xFF000FFFu) | 0x61000;
        }
        else
        {
            hundred.Shown_08 = 0;
            hundred.Word_04 = (hundred.Word_04 & 0xFF000FFFu) | 0x58000;
        }
        hundred.Name_00 = null;
    }

    // Amedo
    public override void Frame(int col, MenuInput input, MenuSounds? sounds)
    {
        var item = Find_2573E0(McDonaldsItem)!;
        if (Unlocked_248)
        {
            item.Selectable_09 = 0xFF;
            return;
        }
        if (Oleg_3095C8.Game_3095C8?.Pad_34 is { } pad && pad.GetButtonState_2B3548(23, false) && !pad.GetButtonState_2B3548(22, false) &&
            !pad.GetButtonState_2B3548(16, false) && !pad.GetButtonState_2B3548(17, false) && !pad.GetButtonState_2B3548(18, false) &&
            !pad.GetButtonState_2B3548(19, false) && !pad.GetButtonState_2B3548(20, false) && !pad.GetButtonState_2B3548(21, false) &&
            pad.GetButtonState_2B3548(0, false) && !pad.GetButtonState_2B3548(1, false) && pad.GetButtonState_2B3548(2, false) &&
            !pad.GetButtonState_2B3548(3, false) && pad.GetButtonState_2B3548(7, false) && pad.GetButtonState_2B3548(6, false) &&
            !pad.GetButtonState_2B3548(4, false) && !pad.GetButtonState_2B3548(5, false))
            Unlocked_248 = true;
        item.Selectable_09 = 0;
    }
}

// Amedo
public sealed class RestartItem : ChoiceItem
{
    public readonly Oleg Oleg_3095C8;
    public uint Word_50;

    // Amedo
    public RestartItem(Oleg oleg, int text, int id, int kind) : base(text, id, 0, true, 0, 1)
    {
        Oleg_3095C8 = oleg;
        Word_50 = (Word_50 & 0xFFFFFF01u) | 1u | (((uint)kind & 0x7F) << 1);
    }

    // Amedo
    public override MenuPage? Activate(int col, MenuPage page)
    {
        if ((Word_50 & 1) != 0) Oleg_3095C8.Game_3095C8?.RequestRestart_17B640((int)((Word_50 >> 1) & 0x7F));
        return null;
    }
}

// Amedo
public sealed class GameOverPage : OlegPage
{
    public const int ContinueItem = 9, QuitItem = 0xB;

    // Amedo
    public GameOverPage(Oleg oleg, GameTexts? texts) : base(oleg, 0x44, null, texts)
    {
        var restart = new RestartItem(oleg, 5, ContinueItem, 2);
        var quit = new ActionItem(oleg, 0x1A, QuitItem, 3);
        Add_25B710(restart);
        Add_25B710(quit);
        SetFirstItem_25B9C0(restart.Id);
    }
}
