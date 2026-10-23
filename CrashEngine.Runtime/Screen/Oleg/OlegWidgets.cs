using System.Numerics;
using CrashEngine.Runtime.Game;

namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo
public sealed class HudBarWidget : AnimatedWidget
{
    public const int IconSprite = 3, FirstPieceSprite = 25;

    public Sprite[] Sprites_88;
    public GameProgress Progress_8C;

    // Amedo
    public HudBarWidget(Sprite[] sprites, GameProgress progress, float anchorX) : base(anchorX)
    {
        Sprites_88 = sprites;
        Progress_8C = progress;
    }

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        uint word = Progress_8C.Word_00;
        uint now = (word >> 17) & 7, max = (word >> 14) & 7;
        if ((State_00 & Off) == 0 && Phase >= 2 && now != 0)
        {
            uint colour = Colour_38;
            float iconW = Progress_8C.BossIconWidth_74 * 0.15f;
            int layer = Layer;
            var p = FitPlace_259F38(overlay, Position_48);
            var size = FitSize_259F68(overlay, Vector_68);
            overlay.Colour_10 = colour;
            iconW *= size.X;
            float iconH = size.Y * 0.2f, step = size.X * 0.03f, pieceH = size.Y * 0.04f;
            var m = new Matrix3x2(iconW, 0f, 0f, iconH, p.X, p.Y);
            overlay.QueuePlacedShape_1A0510(m, Sprites_88[IconSprite], layer);
            m = new Matrix3x2(step, 0f, 0f, pieceH, p.X + iconW, p.Y + pieceH);
            overlay.QueuePlacedShape_1A0510(m, Sprites_88[FirstPieceSprite], layer);
            m.M31 += step;
            overlay.QueuePlacedShape_1A0510(m, Sprites_88[FirstPieceSprite + 1], layer);
            m.M31 += step;
            for (uint i = 2; !(now < i) && i < max; )
            {
                overlay.QueuePlacedShape_1A0510(m, Sprites_88[FirstPieceSprite + 2], layer);
                i++;
                m.M31 += step;
                overlay.QueuePlacedShape_1A0510(m, Sprites_88[FirstPieceSprite + 1], layer);
                m.M31 += step;
            }
            if (now == max && !(max < 2))
            {
                overlay.QueuePlacedShape_1A0510(m, Sprites_88[FirstPieceSprite + 2], layer);
                m.M31 += step;
                overlay.QueuePlacedShape_1A0510(m, Sprites_88[FirstPieceSprite + 3], layer);
            }
        }
        base.Draw_25A038(overlay);
    }
}

// Amedo
public sealed class SliderWidget : AnimatedWidget
{
    public float Value_88;
    public Sprite? Bar_8C, Knob_90, Right_94, Left_98;

    // Amedo
    public SliderWidget(float anchorX) : base(anchorX)
    {
    }

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        if ((State_00 & Off) == 0 && Phase >= 2)
        {
            uint colour = Colour_38;
            var p = FitPlace_259F38(overlay, Position_48);
            int layer = Layer;
            var size = FitSize_259F68(overlay, Vector_68);
            overlay.Colour_10 = colour;
            float barH = size.Y * 0.1f;
            var m = new Matrix3x2(size.X, 0f, 0f, barH, p.X - size.X * 0.5f, p.Y - barH * 0.5f);
            overlay.QueuePlacedShape_1A0510(m, Bar_8C, layer);
            m = new Matrix3x2(size.Y, 0f, 0f, size.Y, p.X + size.X * Value_88, p.Y - size.Y * 0.5f);
            overlay.QueuePlacedShape_1A0510(m, Knob_90, layer);
            m.M32 = p.Y - size.Y * 0.5f;
            m.M31 = p.X - (size.X + size.Y) * 0.5f;
            overlay.QueuePlacedShape_1A0510(m, Left_98, layer);
            m.M32 = p.Y - size.Y * 0.5f;
            m.M31 = p.X + (size.X - size.Y) * 0.5f;
            overlay.QueuePlacedShape_1A0510(m, Right_94, layer);
        }
        base.Draw_25A038(overlay);
    }
}

// Amedo
public sealed class LevelWidget : ItemWidget
{
    public const int EmptyGemSprite = 12, FirstGemSprite = 13, UnknownLevelSprite = 0x1D, FirstLevelSprite = 0x1E;

    public readonly Oleg Oleg_90;
    public float Bob_94;
    private static LevelWidget? _sparkleOwner_7264;
    private static float _sparkleAngle_7268;

    // Amedo
    public LevelWidget(MenuPage page, MenuItem item, Oleg oleg) : base(page, item, 0.5f) => Oleg_90 = oleg;

    // Amedo
    public static void ClearSbss()
    {
        _sparkleOwner_7264 = null;
        _sparkleAngle_7268 = 0f;
    }

    // Amedo
    public override void BeginFrame_25A000()
    {
        Bob_94 = 0f;
        if (IsSelected_25A4A0(0))
        {
            if (!ReferenceEquals(_sparkleOwner_7264, this))
            {
                _sparkleOwner_7264 = this;
                _sparkleAngle_7268 = 0f;
                for (int i = 0; i < GameProgress.Gems; i++) Oleg_90.Emitters_85C[i].Pool_00.Clear_17C298(); // Amedo
            }
            Bob_94 = CrashEngine.Core.GameMath.SinCosVu0_2E4C30(_sparkleAngle_7268).Sin * 0.005f;
            float angle = _sparkleAngle_7268 + BitConverter.Int32BitsToSingle(0x3D907162);
            _sparkleAngle_7268 = angle;
            if (BitConverter.Int32BitsToSingle(0x40490FDB) <= angle) _sparkleAngle_7268 = angle - BitConverter.Int32BitsToSingle(0x40490FDB);
            uint gems = Oleg_90.Progress_390.LevelWord(Item_8C.Id) & 0xFF;
            var size = FitSize_259F68(null!, Vector_68);
            var half = size * 0.5f;
            float step = size.X * BitConverter.Int32BitsToSingle(0x3DE38E39);
            for (int gem = 0; gem < GameProgress.Gems; gem++)
            {
                int sprite = (gems & (1u << gem)) != 0 ? FirstGemSprite + gem : EmptyGemSprite;
                if (sprite == EmptyGemSprite) continue;
                Oleg_90.Emitters_85C[gem].Step_1AB778(BitConverter.Int32BitsToSingle(0x3C888889), new Vector2(0f, half.Y), new Vector2(step, 0f));
            }
        }
        base.BeginFrame_25A000();
    }

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        if ((State_00 & Off) == 0 && 1 < Phase)
        {
            uint colour = Colour_38;
            var progress = Oleg_90.Progress_390;
            int level = Item_8C.Id;
            int layer = Layer;
            uint gems = progress.LevelWord(level) & 0xFF;
            int icon = UnknownLevelSprite;
            if (level <= (int)((progress.Word_04 >> 26) & 0x1F)) icon = GameProgress.LevelSlot_2E6C40[level] + FirstLevelSprite;
            var p = FitPlace_259F38(overlay, Position_48);
            var size = FitSize_259F68(overlay, Vector_68);
            float step = size.X * BitConverter.Int32BitsToSingle(0x3DE38E39);
            bool selected = IsSelected_25A4A0(0);
            Vector2 iconSize;
            if (!selected)
            {
                colour = Colour.Scale_1A0758(colour, 0.5f);
                iconSize = size;
            }
            else iconSize = size * CyclingScale.Global_30BB90.Value_10;
            iconSize.X *= 0.2f;
            iconSize.Y *= 1.6f;
            var m = new Matrix3x2(iconSize.X, 0f, 0f, iconSize.Y, p.X - size.X * 0.35f, p.Y);
            overlay.Colour_10 = colour;
            overlay.QueuePlacedShape_1A0510(m, Oleg_90.Sprites_B4C[icon], layer);
            m = new Matrix3x2(step, 0f, 0f, size.Y, p.X - (size.X - step * 5f) * 0.5f, p.Y - size.Y * 0.5f);
            var placed = Matrix3x2.Identity;
            for (int gem = 0; gem < GameProgress.Gems; gem++)
            {
                int sprite = (gems & (1u << gem)) != 0 ? FirstGemSprite + gem : EmptyGemSprite;
                overlay.QueuePlacedShape_1A0510(m, Oleg_90.Sprites_B4C[sprite], layer);
                if (IsSelected_25A4A0(0) && sprite != EmptyGemSprite)
                {
                    placed.M31 = m.M31 + step * 0.5f;
                    placed.M32 = p.Y;
                    Oleg_90.Emitters_85C[gem].Draw(overlay, placed);
                }
                m.M31 += step;
            }
        }
        base.Draw_25A038(overlay);
    }
}

// Amedo
public sealed class SaveSlotWidget : ItemWidget
{
    public const int CrystalSprite = 21, FirstCharacterSprite = 6, EmptyText = 0x3C, FirstLevelName = 0xBC;

    public readonly Oleg Oleg_90;
    public readonly SaveSummary Summary_94;
    private uint _word = uint.MaxValue;
    private int _time = -1;
    private string _crystals = "", _name = "", _lives = "", _clock = "", _percent = "";

    // Amedo
    public SaveSlotWidget(MenuPage page, MenuItem item, Oleg oleg, SaveSummary summary) : base(page, item, 0.5f)
    {
        Oleg_90 = oleg;
        Summary_94 = summary;
    }

    // Amedo
    private void MakeTexts()
    {
        if (_word == Summary_94.Word_18 && _time == Summary_94.TimePlayed_1C) return;
        _word = Summary_94.Word_18;
        _time = Summary_94.TimePlayed_1C;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int slot = GameProgress.LevelSlot_2E6C40[Summary_94.Level];
        _crystals = " " + Summary_94.Crystals.ToString(inv);
        _name = (Oleg_90.CodeTexts_309770?[slot + FirstLevelName] ?? "").Replace(' ', '~');
        _lives = "  " + (Summary_94.Lives - 1).ToString(inv);
        int minutes = CrashEngine.Runtime.Character.CharacterController.Cvt(_time * Widget.SecondsPerTick * BitConverter.Int32BitsToSingle(0x3C888889));
        int hours = minutes / 60;
        minutes -= hours * 60;
        _clock = hours.ToString(inv) + (minutes < 10 ? ":0" : ":") + minutes.ToString(inv);
        _percent = Summary_94.Percent.ToString(inv) + "%";
    }

    // Amedo
    public override void Draw_25A038(OverlayQueue overlay)
    {
        if ((State_00 & Off) == 0 && 1 < Phase)
        {
            uint colour = Colour_38;
            var p = FitPlace_259F38(overlay, Position_48);
            var size = FitSize_259F68(overlay, Vector_68);
            int layer = Layer;
            if (Summary_94.Used)
            {
                MakeTexts();
                int slot = GameProgress.LevelSlot_2E6C40[Summary_94.Level];
                int ch = Summary_94.Character;
                int charSprite = ch == 6 ? 6 : ch + FirstCharacterSprite;
                var icon = size;
                if (!IsSelected_25A4A0(0))
                {
                    colour = Colour.Scale_1A0758(colour, 0.75f);
                    icon.X *= 0.09f;
                    icon.Y *= 0.12f;
                }
                else
                {
                    float pulse = CyclingScale.Global_30BB90.Value_10;
                    icon.X = icon.X * 0.09f * pulse;
                    icon.Y = icon.Y * 0.12f * pulse;
                }
                var text = size * 0.5f;
                overlay.Colour_10 = colour;
                overlay.Font_28 = Oleg_90.Font_394;
                var m = new Matrix3x2(icon.X, 0f, 0f, icon.Y, p.X - size.X * BitConverter.Int32BitsToSingle(0x3E051EB8), p.Y - size.Y * 0.04f);
                overlay.QueuePlacedShape_1A0510(m, Oleg_90.Sprites_B4C[slot + LevelWidget.FirstLevelSprite], layer);
                overlay.Flags_34 = 6;
                overlay.Scale_2C = text;
                overlay.QueueText_1A03A0(_name, p);
                overlay.Flags_34 = 0x44;
                var at = new Vector2(p.X, p.Y + size.Y * 0.05f);
                overlay.Scale_2C = size * 0.5f;
                overlay.QueueText_1A03A0(_clock, at);
                at.X = p.X + size.X * 0.1f;
                overlay.QueueText_1A03A0(_percent, at);
                m = new Matrix3x2(size.X * 0.075f, 0f, 0f, size.Y * 0.075f, p.X + size.X * 0.15f, p.Y - size.Y * 0.05f);
                overlay.QueuePlacedShape_1A0510(m, Oleg_90.Sprites_B4C[charSprite], layer);
                overlay.Flags_34 = 0x11;
                overlay.Scale_2C = size * 0.625f;
                overlay.QueueText_1A03A0(_lives, new Vector2(m.M31, m.M32));
                m = new Matrix3x2(size.X * 0.04f, 0f, 0f, size.Y * 0.075f, p.X + size.X * 0.125f, p.Y + size.Y * 0.05f);
                overlay.QueuePlacedShape_1A0510(m, Oleg_90.Sprites_B4C[CrystalSprite], layer);
                overlay.Flags_34 = 0x30;
                overlay.Scale_2C = size * 0.625f;
                overlay.QueueText_1A03A0(_crystals, new Vector2(m.M31, m.M32));
            }
            else
            {
                string empty = Oleg_90.CodeTexts_309770?[EmptyText] ?? "";
                if (IsSelected_25A4A0(0)) size *= CyclingScale.Global_30BB90.Value_10;
                else colour = Colour.Scale_1A0758(colour, (Item_8C.Shown_08 & 1) != 0 ? 0.75f : 0.5f);
                overlay.Font_28 = Oleg_90.Font_394;
                overlay.Colour_10 = colour;
                overlay.Scale_2C = size;
                overlay.Flags_34 = 0x22;
                overlay.QueueText_1A03A0(empty, p);
            }
        }
        base.Draw_25A038(overlay);
    }
}
