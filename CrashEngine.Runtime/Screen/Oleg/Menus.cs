using System.Globalization;
using System.Numerics;
using CrashEngine.Runtime.Character;
using CrashEngine.Runtime.Game;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public sealed class MenuInput
{
    public const int Accept = 0, Back = 1, Start = 2, Up = 3, Down = 4, Left = 5, Right = 6;

    public readonly ButtonBindings Bindings_00;
    public byte Edge_04, Held_05;

    // Amedo
    public MenuInput(ButtonBindings bindings)
    {
        Bindings_00 = bindings;
        Clear_25BB98();
    }

    // Amedo
    public void Clear_25BB98()
    {
        Held_05 = 0;
        Edge_04 = 0;
    }

    // Amedo
    public bool Has_25BBA8(int flag, bool edge) => ((edge ? Edge_04 : Held_05) & (byte)(1 << flag)) != 0;

    // Amedo
    public void Poll_257CC0(CharacterPad pad, bool allowStart)
    {
        for (int pass = 1; pass >= 0; pass--)
        {
            bool edge = pass == 1;
            bool accept = Bindings_00.Has_2B2FE8(pad, Accept, edge);
            bool back = Bindings_00.Has_2B2FE8(pad, Back, edge);
            bool up = Bindings_00.Has_2B2FE8(pad, Up, edge);
            bool down = Bindings_00.Has_2B2FE8(pad, Down, edge);
            bool left = Bindings_00.Has_2B2FE8(pad, Left, edge);
            bool right = Bindings_00.Has_2B2FE8(pad, Right, edge);
            bool start = allowStart && Bindings_00.Has_2B2FE8(pad, Start, edge);
            byte b = edge ? Edge_04 : Held_05;
            b = Set(b, 1 << Accept, accept);
            b = Set(b, 1 << Back, back);
            b = Set(b, 1 << Up, up);
            b = Set(b, 1 << Down, down);
            b = Set(b, 1 << Left, left);
            b = Set(b, 1 << Right, right);
            b = Set(b, 1 << Start, start);
            if (edge) Edge_04 = b;
            else Held_05 = b;
        }
    }

    private static byte Set(byte b, int bit, bool on) => (byte)(on ? b | bit : b & ~bit);
}

// Amedo
public interface IMenuSoundPlayer
{
    void PlaySound_1E5608(object sound, int group);
}

// Amedo
public sealed class MenuSounds
{
    public const int Slots = 13;
    public const int Refused = 0, Accepted = 1, DownStuck = 2, Down = 3, UpStuck = 4, Up = 5, RightStuck = 6, Right = 7;
    public const int LeftStuck = 8, LeftMoved = 9, Linked = 10, Back = 11;

    public readonly object?[] Sounds_00 = new object?[Slots];
    public readonly int[] Groups_34 = new int[Slots];
    public IMenuSoundPlayer? Player;

    // Amedo
    public MenuSounds()
    {
        for (int i = 0; i < Slots; i++) Groups_34[i] = 3;
    }

    // Amedo
    public void Play_25BE10(int slot)
    {
        if (Sounds_00[slot] is { } sound) Player?.PlaySound_1E5608(sound, Groups_34[slot]);
    }
}

// Amedo
public abstract class MenuItem
{
    public const uint EdgeInput = 0x10000000;

    public string? Name_00;
    public uint Word_04;
    public byte Shown_08, Selectable_09;
    public MenuPage? Linked_0C;
    public GameTexts? Texts;

    public int Id => (int)(Word_04 & 0xFFF);
    public int TextIndex => (int)((Word_04 >> 12) & 0xFFF);
    public int Columns => (int)((Word_04 >> 24) & 0xF);

    // Amedo
    protected MenuItem(int text, int id, int columns)
    {
        Word_04 = (uint)(id & 0xFFF) | (((uint)columns & 0xF) << 24) | EdgeInput | (((uint)text & 0xFFF) << 12);
        Shown_08 = Selectable_09 = (byte)((1 << columns) - 1);
    }

    public bool IsShown(int col) => ((Shown_08 >> col) & 1) != 0;
    public bool IsSelectable(int col) => ((Selectable_09 >> col) & 1) != 0;

    // Amedo
    public virtual MenuPage? Activate(int col, MenuPage page) => Linked_0C;

    public virtual void Entered(int col, int mode) { }
    public virtual void Left(int col, int mode) { }

    // Amedo
    public virtual MenuPage? Frame(int col, MenuPage page, MenuInput input, MenuSounds? sounds)
    {
        if ((input.Edge_04 & 1) == 0) return null;
        bool shown = IsShown(col);
        MenuPage? next = shown ? Activate(col, page) : null;
        if (sounds is not null) sounds.Play_25BE10(shown ? (next is null ? MenuSounds.Accepted : MenuSounds.Linked) : MenuSounds.Refused);
        return next;
    }

    public abstract MenuPage? LinkedPage();
    public abstract bool ValueText(int col, out string text);
    public abstract bool SetInt(int col, int value);
    public abstract bool GetInt(int col, out int value);
    public abstract bool SetFloat(int col, float value);
    public abstract bool GetFloat(int col, out float value);

    // Amedo
    public void SetFlag_25BD40(int col, int value) => SetInt(col, value);

    // Amedo
    public bool GetFlag_25BD68(int col, out bool flag)
    {
        flag = false;
        if (!GetInt(col, out int v)) return false;
        if (v == 0) return true;
        if (v != 1) return false;
        flag = true;
        return true;
    }

    public string Text => Name_00 ?? Texts?[TextIndex] ?? "";
}

// Amedo
public class LinkItem : MenuItem
{
    // Amedo
    public LinkItem(int text, int id, MenuPage? page, int columns) : base(text, id, columns) => Linked_0C = page;

    // Amedo
    public LinkItem(string name, int id, MenuPage? page, int columns) : base(0, id, columns)
    {
        Name_00 = name;
        Linked_0C = page;
    }

    public override MenuPage? LinkedPage() => Linked_0C;

    public override bool ValueText(int col, out string text)
    {
        text = "";
        return false;
    }

    public override bool SetInt(int col, int value) => false;

    public override bool GetInt(int col, out int value)
    {
        value = -1;
        return false;
    }

    public override bool SetFloat(int col, float value) => false;

    public override bool GetFloat(int col, out float value)
    {
        value = BitConverter.Int32BitsToSingle(0x7149F2CA);
        return false;
    }
}

// Amedo
public sealed class BackItem : LinkItem
{
    // Amedo
    public BackItem(int text, int id, MenuPage? page, int columns) : base(text, id, page, columns) { }

    // Amedo
    public override MenuPage? Activate(int col, MenuPage page)
    {
        page.Back(col);
        return base.Activate(col, page);
    }
}

// Amedo
public class ValueItem : MenuItem
{
    public readonly int[] Values_14 = new int[8];
    public int Min_34, Max_38;
    public bool Wrap_3C, Cycles_3D;
    private readonly string?[] _text = new string?[8];
    private readonly int[] _textValue = new int[8];

    // Amedo
    public ValueItem(int text, int id, int min, int max, bool wrap, int value, int columns) : base(text, id, columns) =>
        SetRange_25B390(min, max, wrap, value);

    // Amedo
    public void SetRange_25B390(int min, int max, bool wrap, int value)
    {
        Min_34 = min;
        Max_38 = max;
        Wrap_3C = wrap;
        for (int i = 0; i < Columns; i++) Values_14[i] = value;
    }

    // Amedo
    public override MenuPage? Activate(int col, MenuPage page)
    {
        if (Linked_0C is not null) return Linked_0C;
        if (!Cycles_3D) return null;
        int v = Values_14[col];
        if (v < Max_38) v++;
        else if (Wrap_3C) v = Min_34;
        else return null;
        SetInt(col, v);
        return null;
    }

    // Amedo
    public override MenuPage? Frame(int col, MenuPage page, MenuInput input, MenuSounds? sounds)
    {
        bool edge = (Word_04 & EdgeInput) != 0;
        bool left = input.Has_25BBA8(MenuInput.Left, edge);
        bool right = input.Has_25BBA8(MenuInput.Right, edge);
        if (left)
        {
            bool moved = false;
            int v = Values_14[col];
            if (0 < v || Wrap_3C)
            {
                v = Min_34 < v ? v - 1 : Max_38;
                SetInt(col, v);
                moved = true;
            }
            sounds?.Play_25BE10(moved ? MenuSounds.LeftMoved : MenuSounds.LeftStuck);
            return null;
        }
        if (right)
        {
            bool moved = false;
            int v = Values_14[col];
            if (v < Max_38 || Wrap_3C)
            {
                v = v < Max_38 ? v + 1 : Min_34;
                SetInt(col, v);
                moved = true;
            }
            sounds?.Play_25BE10(moved ? MenuSounds.Right : MenuSounds.RightStuck);
            return null;
        }
        return base.Frame(col, page, input, sounds);
    }

    public override MenuPage? LinkedPage() => null;

    // Amedo
    public override bool ValueText(int col, out string text)
    {
        int v = Values_14[col];
        if (_text[col] is null || _textValue[col] != v)
        {
            _textValue[col] = v;
            _text[col] = v.ToString(CultureInfo.InvariantCulture);
        }
        text = _text[col]!;
        return true;
    }

    public override bool SetInt(int col, int value)
    {
        Values_14[col] = value;
        return true;
    }

    public override bool GetInt(int col, out int value)
    {
        value = Values_14[col];
        return true;
    }

    public override bool SetFloat(int col, float value)
    {
        Values_14[col] = GameMath.Trunc(value);
        return true;
    }

    public override bool GetFloat(int col, out float value)
    {
        value = Values_14[col];
        return true;
    }
}

// Amedo
public class ChoiceItem : ValueItem
{
    public string[]? Strings_40;
    public bool UseStrings_44;
    public readonly int[] Choices_48;

    // Amedo
    public ChoiceItem(int text, int id, int count, bool wrap, int value, int columns) : base(text, id, 0, count - 1, wrap, value, columns)
    {
        Choices_48 = new int[count];
        Array.Fill(Choices_48, -1);
    }

    // Amedo
    public void SetChoiceText_25BFE0(int choice, int text) => Choices_48[choice] = text;

    // Amedo
    public override bool ValueText(int col, out string text)
    {
        text = "";
        if (Max_38 < Min_34) return false;
        int v = Values_14[col];
        text = UseStrings_44 && Strings_40 is { } strings ? strings[v] : Texts?[Choices_48[v]] ?? "";
        return true;
    }
}

// Amedo
public sealed class ToggleItem : ChoiceItem
{
    // Amedo
    public ToggleItem(int text, int id, int offText, int onText, bool wrap, int value, int columns) : base(text, id, 2, wrap, value, columns)
    {
        SetRange_25B390(0, 1, wrap, value);
        Cycles_3D = true;
        Choices_48[0] = offText;
        Choices_48[1] = onText;
    }
}

// Amedo
public class MenuPage
{
    public const uint Wrap = 0x1000, AcceptOn = 0x01000000, BackOn = 0x02000000, UpDownOn = 0x04000000, LeftRightOn = 0x08000000, StartExits = 0x80000000;

    public static readonly MenuPage Exit_3B6B50 = new("DUMMY-RESUME", 1); // Amedo
    private static MenuPage? Next_309D94;

    public string? Title_00;
    public readonly byte[] Column_04;
    public readonly byte[] Selected_0C;
    public uint Flags_14;
    public MenuPage? Parent_18, Left_1C, Right_20, Up_24, Down_28;
    public int LeftLink_2C = -1, RightLink_30 = -1, UpLink_34 = -1, DownLink_38 = -1;
    public readonly List<MenuItem> Items_3C = new(10);
    public GameTexts? Texts;

    public int Columns => (int)((Flags_14 >> 13) & 0x1F);
    public int TitleText => (int)(Flags_14 & 0x3FF);
    public int Mode => (int)((Flags_14 >> 28) & 7);

    // Amedo
    public MenuPage(string? title, int columns)
    {
        Title_00 = title;
        Column_04 = new byte[columns];
        Selected_0C = new byte[columns];
        Flags_14 = (Flags_14 & 0xFFFC1FFFu) | (((uint)columns & 0x1F) << 13);
        Initialise_257150();
    }

    // Amedo
    public MenuPage(int titleText, int columns) : this(null, columns) =>
        Flags_14 = (Flags_14 & ~0x3FFu) | ((uint)titleText & 0x3FF);

    // Amedo
    public static void StaticInit_2597C0()
    {
        Next_309D94 = null;
        var exit = Exit_3B6B50;
        exit.Flags_14 = (1u & 0x1F) << 13;
        exit.Initialise_257150();
    }

    // Amedo
    public void Initialise_257150()
    {
        uint f = Flags_14 | AcceptOn | BackOn | UpDownOn | LeftRightOn | Wrap;
        f = (f & 0xFF03FFFFu) | 0x7C0000u;
        f = (f & 0x8FFFFFFFu) | 0x10000000u | StartExits;
        Flags_14 = f;
        Parent_18 = null;
        for (int i = 0; i < Columns; i++)
        {
            Selected_0C[i] = 0;
            Column_04[i] = 0;
        }
        Left_1C = Right_20 = Up_24 = Down_28 = null;
        LeftLink_2C = RightLink_30 = UpLink_34 = DownLink_38 = -1;
    }

    public virtual void Entered(int col, int mode) { }
    public virtual void Frame(int col, MenuInput input, MenuSounds? sounds) { }
    public virtual void Left(int col, int mode) { }
    public virtual bool Back(int col) => false;
    public virtual void BeginFrame(int col) { }
    public virtual void EndFrame(int col) { }

    // Amedo
    public void Add_25B710(MenuItem item)
    {
        item.Texts ??= Texts;
        Items_3C.Add(item);
    }

    // Amedo
    public MenuItem? Item_25A238(int i) => i >= 0 && i < Items_3C.Count ? Items_3C[i] : null;

    // Amedo
    public MenuItem? ItemAgain_25A268(int i) => i >= 0 && i < Items_3C.Count ? Items_3C[i] : null;

    // Amedo
    public int IndexOf_257310(int id)
    {
        for (int i = 0; i < Items_3C.Count; i++)
            if (Items_3C[i].Id == id) return i;
        return -1;
    }

    // Amedo
    public MenuItem? Find_2573E0(int id)
    {
        foreach (var item in Items_3C)
            if (item.Id == id) return item;
        return null;
    }

    // Amedo
    public int ShownCount_2574A0(int col)
    {
        int n = 0;
        foreach (var item in Items_3C) n += (item.Shown_08 >> col) & 1;
        return n;
    }

    // Amedo
    public void Select_25B970(int col, int id)
    {
        int i = IndexOf_257310(id);
        if (0 <= i) Selected_0C[col] = (byte)i;
    }

    // Amedo
    public void SetFirstItem_25B9C0(int id)
    {
        int i = IndexOf_257310(id);
        if (0 <= i) Flags_14 = (Flags_14 & 0xFF03FFFFu) | (((uint)i & 0x3F) << 18);
    }

    // Amedo
    public bool SelectNext_257090(int col)
    {
        int count = Items_3C.Count;
        if (count <= 0) return false;
        int start = Selected_0C[col];
        int i = start + 1;
        while (true)
        {
            if (!(i < count))
            {
                if ((Flags_14 & Wrap) == 0) return false;
                i = 0;
            }
            if (i == start) return false;
            var item = Item_25A238(i);
            if (item is not null && item.IsShown(col) && item.IsSelectable(col))
            {
                Selected_0C[col] = (byte)i;
                return true;
            }
            i++;
        }
    }

    // Amedo
    public bool SelectPrevious_256FD0(int col)
    {
        int count = Items_3C.Count;
        if (count <= 0) return false;
        int start = Selected_0C[col];
        int i = start;
        while (true)
        {
            if (i != 0) i--;
            else
            {
                if ((Flags_14 & Wrap) == 0) return false;
                i = count - 1;
            }
            if (i == start) return false;
            var item = Item_25A238(i);
            if (item is not null && item.IsShown(col) && item.IsSelectable(col))
            {
                Selected_0C[col] = (byte)i;
                return true;
            }
        }
    }

    // Amedo
    public void SkipHidden_257230(int col)
    {
        int count = Items_3C.Count;
        var item = Item_25A238(Selected_0C[col]);
        for (int k = 0; k < count; k++)
        {
            if (item is not null && item.IsShown(col)) return;
            Selected_0C[col] = (byte)((Selected_0C[col] + 1) % count);
            item = Item_25A238(Selected_0C[col]);
        }
    }

    // Amedo
    public void Enter_257570(MenuPage? from)
    {
        int count = Items_3C.Count;
        uint mode = from is not null ? (from.Flags_14 >> 28) & 7 : 1u;
        Flags_14 = (Flags_14 & 0x8FFFFFFFu) | ((mode & 7) << 28);
        int m = (int)mode;
        for (int col = 0; col < Columns; col++)
        {
            Entered(col, m);
            if (m == 1)
            {
                int first = (int)((Flags_14 >> 18) & 0x3F);
                if ((uint)first < (uint)count) Selected_0C[col] = (byte)first;
            }
            for (int k = 0; k < count; k++)
            {
                int i = (int)((uint)(Selected_0C[col] + k) % (uint)count);
                var item = Item_25A238(i);
                if (item is not null && item.IsShown(col) && item.IsSelectable(col))
                {
                    Selected_0C[col] = (byte)i;
                    break;
                }
            }
            for (int i = 0; i < count; i++) Items_3C[i].Entered(col, m);
        }
    }

    // Amedo
    public void Leave_25BA18(MenuPage? to)
    {
        int count = Items_3C.Count;
        int m = Mode;
        for (int col = 0; col < Columns; col++)
        {
            Left(col, m);
            for (int i = 0; i < count; i++) Items_3C[i].Left(col, m);
        }
    }

    // Amedo
    private static MenuPage? FirstShown(MenuPage? start, MenuPage self, int col, bool left)
    {
        for (var p = start; p is not null && !ReferenceEquals(p, self); p = left ? p.Left_1C : p.Right_20)
            if (0 < p.ShownCount_2574A0(col)) return p;
        return null;
    }

    // Amedo
    public MenuPage? Step_257778(MenuInput input, MenuSounds? sounds, int col)
    {
        if (Next_309D94 is null) StepBody(input, sounds, col);
        var next = Next_309D94;
        Next_309D94 = null;
        return next;
    }

    private void StepBody(MenuInput input, MenuSounds? sounds, int col)
    {
        uint flags = Flags_14;
        if ((flags & StartExits) != 0 && input.Has_25BBA8(MenuInput.Start, true))
        {
            Next_309D94 = Exit_3B6B50;
            return;
        }
        bool back = (Flags_14 & BackOn) != 0 && input.Has_25BBA8(MenuInput.Back, true);
        bool up = false, down = false;
        if ((Flags_14 & UpDownOn) != 0)
        {
            up = input.Has_25BBA8(MenuInput.Up, true);
            down = input.Has_25BBA8(MenuInput.Down, true);
        }
        SkipHidden_257230(col);
        if (up)
        {
            bool moved = true;
            if (Up_24 is { } above && Selected_0C[col] == 0)
            {
                int i = above.IndexOf_257310(UpLink_34);
                Next_309D94 = above;
                above.Selected_0C[col] = (byte)(i == -1 ? above.Items_3C.Count - 1 : i);
            }
            else moved = SelectPrevious_256FD0(col);
            sounds?.Play_25BE10(moved ? MenuSounds.Up : MenuSounds.UpStuck);
        }
        else if (down)
        {
            bool moved = true;
            if (Down_28 is { } below && Selected_0C[col] == Items_3C.Count - 1)
            {
                int i = below.IndexOf_257310(DownLink_38);
                Next_309D94 = below;
                below.Selected_0C[col] = (byte)(i == -1 ? 0 : i);
            }
            else moved = SelectNext_257090(col);
            sounds?.Play_25BE10(moved ? MenuSounds.Down : MenuSounds.DownStuck);
        }
        var item = Item_25A238(Selected_0C[col]);
        bool left = false, right = false;
        if ((Flags_14 & LeftRightOn) != 0 && !up && !down)
        {
            left = input.Has_25BBA8(MenuInput.Left, false);
            right = input.Has_25BBA8(MenuInput.Right, false);
        }
        var leftPage = FirstShown(Left_1C, this, col, true);
        var rightPage = FirstShown(Right_20, this, col, false);
        if (left && leftPage is not null)
            Jump(leftPage, LeftLink_2C, col);
        else if (right && rightPage is not null)
            Jump(rightPage, RightLink_30, col);
        else if (back)
        {
            bool handled = Back(col);
            Flags_14 = (Flags_14 & 0x8FFFFFFFu) | 0x20000000u;
            var parent = Parent_18;
            Next_309D94 = parent;
            if (sounds is not null && (handled || (parent is not null && !ReferenceEquals(parent, this)))) sounds.Play_25BE10(MenuSounds.Back);
        }
        else if (item is not null)
        {
            var next = item.Frame(col, this, input, sounds);
            Next_309D94 = next;
            if (next is not null && !ReferenceEquals(next, this))
            {
                uint mode = ReferenceEquals(next, Parent_18) ? 2u : 1u;
                Flags_14 = (Flags_14 & 0x8FFFFFFFu) | (mode << 28);
                if (sounds is not null && (Flags_14 & 0x70000000u) == 0x20000000u) sounds.Play_25BE10(MenuSounds.Back);
            }
        }
        Frame(col, input, sounds);
    }

    private void Jump(MenuPage to, int link, int col)
    {
        int i = to.IndexOf_257310(link);
        Next_309D94 = to;
        if (i == -1)
        {
            int sel = Selected_0C[col];
            to.Selected_0C[col] = (byte)(sel < to.Items_3C.Count ? sel : to.Items_3C.Count - 1);
        }
        else to.Selected_0C[col] = (byte)i;
    }

    public string TitleString => Title_00 ?? Texts?[TitleText] ?? "";
}

// Amedo
public sealed class MenuDrawer
{
    public const int TitleStyle = 0, SelectedStyle = 1, NormalStyle = 2, DimStyle = 3;

    public byte FewItems_00, ManyItems_01, TitleFlags_02 = 6, ItemFlags_03 = 3;
    public Vector2 TitlePosition_04, TitleScale_0C = Vector2.One, ItemsPosition_14 = new(0f, 0.1f);
    public Vector2 FewScale_1C = new(0.5f, 0.5f);
    public float FewSpacing_24 = 0.05f;
    public Vector2 ManyScale_28 = new(0.5f, 0.5f);
    public float ManySpacing_30 = 0.05f;
    public readonly GameFont?[] Fonts_34 = new GameFont?[4];
    public readonly uint[] Colours_44 = new uint[4];
    public IWidgetScale? Pulse_54, Scale_58;
    public string?[] Styles = new string?[4];

    // Amedo
    public MenuDrawer(GameFont? font)
    {
        Colours_44[0] = Colour.GetColor_11BF10(15);
        Fonts_34[0] = font;
        Colours_44[1] = Colour.GetColor_11BF10(15);
        Fonts_34[1] = font;
        Colours_44[2] = Colour.GetColor_11BF10(0x13);
        Fonts_34[2] = font;
        Colours_44[3] = Colour.GetColor_11BF10(0x15);
        Fonts_34[3] = font;
    }

    // Amedo
    public void Style_257F08(int kind, Vector2 scale, uint[] colours, OverlayQueue pen)
    {
        switch (kind)
        {
            case TitleStyle:
                pen.Colour_10 = colours[0];
                pen.Font_28 = Fonts_34[0];
                pen.Scale_2C = scale;
                pen.Flags_34 = TitleFlags_02;
                break;
            case SelectedStyle:
            {
                uint c = colours[1];
                if (Pulse_54 is { } pulse) c = Colour.Scale_1A0758(c, pulse.Scale_10);
                if (Scale_58 is { } s) scale *= s.Scale_10;
                pen.Colour_10 = c;
                pen.Font_28 = Fonts_34[1];
                pen.Scale_2C = scale;
                pen.Flags_34 = ItemFlags_03;
                break;
            }
            case NormalStyle:
                pen.Colour_10 = colours[2];
                pen.Font_28 = Fonts_34[2];
                pen.Scale_2C = scale;
                pen.Flags_34 = ItemFlags_03;
                break;
            case DimStyle:
                pen.Colour_10 = colours[3];
                pen.Font_28 = Fonts_34[3];
                pen.Scale_2C = scale;
                pen.Flags_34 = ItemFlags_03;
                break;
        }
        pen.TextStyle = Styles[kind];
    }

    private readonly List<MenuItem> _items = new(64);
    private readonly List<string> _strings = new(64);
    private readonly Dictionary<MenuItem, (string Text, string Value, string Line)> _lines = new(ReferenceEqualityComparer.Instance);
    private readonly uint[] _colours = new uint[4];
    private readonly Vector2[] _rows = new Vector2[64];

    private string LineOf(MenuItem item, int col)
    {
        string text = item.Text;
        if (!item.ValueText(col, out string value))
        {
            _lines.Remove(item);
            return text;
        }
        if (_lines.TryGetValue(item, out var known) && ReferenceEquals(known.Text, text) && ReferenceEquals(known.Value, value)) return known.Line;
        string line = text + " : " + value;
        _lines[item] = (text, value, line);
        return line;
    }

    // Amedo
    public void DrawMenuPage_2581A0(MenuPage page, int col, uint tint, Vector2 position, Vector2 size, OverlayQueue overlay)
    {
        var titleScale = TitleScale_0C;
        var selected = page.ItemAgain_25A268(page.Selected_0C[col]);
        float tr = (tint & 0xFF) * BitConverter.Int32BitsToSingle(0x3BAAAAAB), tg = ((tint >> 8) & 0xFF) * BitConverter.Int32BitsToSingle(0x3BAAAAAB);
        float tb = ((tint >> 16) & 0xFF) * BitConverter.Int32BitsToSingle(0x3BAAAAAB), ta = (tint >> 24) * BitConverter.Int32BitsToSingle(0x3C000000);
        for (int i = 0; i < 4; i++)
        {
            uint c = Colours_44[i];
            _colours[i] = Colour.Set_101218(Colour.R(c) * tr, Colour.G(c) * tg, Colour.B(c) * tb, Colour.A(c) * ta);
        }
        titleScale *= size;
        if (!(MathF.Abs(titleScale.X) <= BitConverter.Int32BitsToSingle(0x3851B717) && MathF.Abs(titleScale.Y) <= BitConverter.Int32BitsToSingle(0x3851B717)))
        {
            var at = TitlePosition_04 * size + position;
            Style_257F08(TitleStyle, titleScale, _colours, overlay);
            overlay.TextOwner = page;
            overlay.QueueText_1A03A0(page.TitleString, at);
            overlay.TextOwner = null;
        }
        if (selected is null) return;
        _items.Clear();
        _strings.Clear();
        int selectedIndex = 0;
        foreach (var item in page.Items_3C)
        {
            string line = LineOf(item, col);
            if (ReferenceEquals(item, selected)) selectedIndex = _items.Count;
            if (line.Length != 0 && item.IsSelectable(col))
            {
                _items.Add(item);
                _strings.Add(line);
            }
        }
        int count = _items.Count;
        if (count == 0) return;
        float t;
        if (FewItems_00 == 0 || ManyItems_01 == 0) t = 1f;
        else if (!(FewItems_00 < count)) t = 0f;
        else if (!(count < ManyItems_01)) t = 1f;
        else t = (float)(uint)(count - FewItems_00) / (float)(uint)(ManyItems_01 - FewItems_00);
        float u = 1f - t;
        var scale = new Vector2((ManyScale_28.X * t + FewScale_1C.X * u) * size.X, (ManyScale_28.Y * t + FewScale_1C.Y * u) * size.Y);
        float spacing = (ManySpacing_30 * t + FewSpacing_24 * u) * size.Y;
        var cursor = Vector2.Zero;
        for (int i = 0; i < count && i < _rows.Length; i++)
        {
            _rows[i] = cursor;
            cursor.Y += spacing;
        }
        int first, last;
        if (ManyItems_01 == 0 || !(ManyItems_01 < count))
        {
            last = count - 1;
            first = 0;
        }
        else
        {
            int window = ManyItems_01, half = window >> 1;
            first = half < selectedIndex ? selectedIndex - half : 0;
            cursor.Y = window * spacing;
            if (!(first + window < count)) first = count - window;
            last = first - 1 + window;
        }
        var origin = ItemsPosition_14 * size + position;
        switch (ItemFlags_03)
        {
            case 0x22 or 0x30 or 0x60: origin.Y -= (cursor.Y - spacing) * 0.5f; break;
            case 6 or 0x14 or 0x44: origin.Y -= cursor.Y; break;
        }
        for (int i = first; !(last < i); i++)
        {
            var item = _items[i];
            int kind = ReferenceEquals(item, selected) ? SelectedStyle : item.IsShown(col) ? NormalStyle : DimStyle;
            Style_257F08(kind, scale, _colours, overlay);
            overlay.TextOwner = item;
            overlay.QueueText_1A03A0(_strings[i], _rows[i - first] + origin);
            overlay.TextOwner = null;
        }
    }
}

// Amedo
public sealed class CreditsScroller
{
    public uint Word_00;
    public readonly GameFont? Font_04;
    public readonly List<string> Lines_1C = new();
    public float Speed_20 = 0.1f, Rate_24 = 1f, Spacing_28 = 0.05f, Scale_2C = 0.75f, Offset_30 = 1f;

    // Amedo
    public CreditsScroller(GameFont? font, string text)
    {
        Font_04 = font;
        Parse_258F20(text);
    }

    public int LineCount => (int)(Word_00 & 0xFFF);
    public int FirstLine => (int)((Word_00 >> 12) & 0xFFF);

    // Amedo
    private void Parse_258F20(string text)
    {
        Word_00 &= 0xFF000000u;
        int start = 0, i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            int skip;
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') skip = 2;
            else if (c == '\n') skip = i + 1 < text.Length && text[i + 1] == '\r' ? 2 : 1;
            else if (c == '\r') skip = 1;
            else
            {
                i++;
                continue;
            }
            Lines_1C.Add(text[start..i]);
            Word_00 = (Word_00 & 0xFFFFF000u) | (((Word_00 & 0xFFF) + 1) & 0xFFF);
            i += skip;
            start = i;
        }
    }

    // Amedo
    public bool Update_25C540(int dtTicks)
    {
        uint w = Word_00;
        uint first = (w >> 12) & 0xFFF;
        float offset = Offset_30 - dtTicks * Widget.SecondsPerTick * Speed_20 * Rate_24;
        Offset_30 = offset;
        if (first < (w & 0xFFF))
        {
            if (offset + Spacing_28 < 0f)
            {
                Offset_30 = offset + Spacing_28;
                Word_00 = (w & 0xFF000FFFu) | (((first + 1) & 0xFFF) << 12);
            }
            return true;
        }
        return 0f < offset;
    }

    // Amedo
    public void Draw_25C5F0(OverlayQueue overlay)
    {
        float y = Offset_30;
        int line = FirstLine;
        overlay.Flags_34 = 3;
        overlay.Scale_2C = new Vector2(Scale_2C, Scale_2C);
        overlay.Font_28 = Font_04;
        overlay.Colour_10 = Colour.GetColor_11BF10(15);
        if (!(y < 1f) || !(line < LineCount)) return;
        while (true)
        {
            overlay.QueueText_1A03A0(Lines_1C[line], new Vector2(0.5f, y));
            line++;
            y += Spacing_28;
            if (1f <= y || LineCount <= line) break;
        }
    }
}
