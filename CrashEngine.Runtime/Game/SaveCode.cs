using CrashEngine.Runtime.Screen;
using CrashEngine.Runtime.Screen.Oleg;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Game;

// Amedo
public abstract partial class SaveCode // Amedo 2026-11-11
{
    public const int Messages = 0x26;
    public const uint Done = 0x10, FullDirectory = 0x20, Success = 0x10000000, AutosaveOn = 0x20000000;

    public static readonly string[] English_2E7C58 =
    [
        "", "Checking media, please wait...", "Formatting, please wait...", "Finding directory, please wait...", "Creating directory, please wait...",
        "Saving header-data, please wait...", "Loading header-data, please wait...", "Finding file, please wait...", "Saving file, please wait...",
        "Loading file, please wait...", "Do you wish to create a save?", "Please replace media...", "Please replace media...", "Media unformatted...",
        "Do you REALLY wish to format?", "Insert media with save...", "Insert media with space...", "Overwrite save?", "Select block to save...",
        "Select block to load...", "Cancel save?", "Format successful!", "Save successful!", "Load successful!", "Format failed!", "Save failed!",
        "Load failed!", "Save cancelled!", "Save-Block", "Empty", "Cancel", "Format", "Continue", "Continue wihout saving", "Create save", "Retry",
        "Yes", "No",
    ];
    public static readonly int[] TextIds_3D1A08 = new int[Messages];
    public static readonly int[] MessageTexts_2E7CF0 = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0x15, 0x16, 0x17, 0x1B, 0, 0xB];
    public static readonly int[] QuestionTexts_2E7D28 = [0, 0xB, 0x10, 0xA, 0xB, 0xD, 0xE, 0xF, 0x10, 0x11, 0x14, 0x12, 0x13, 0x18, 0x19, 0x1A];

    public uint Word_00, Word_04;
    public string Name_08;
    public readonly SaveDevice Device_14;
    public GameTexts? Texts_309770;

    // Amedo
    protected SaveCode(string name, SaveDevice device, GameTexts? texts)
    {
        Name_08 = name;
        Device_14 = device;
        Texts_309770 = texts;
        Word_00 = 0;
        Word_04 = 0;
        Array.Fill(TextIds_3D1A08, -1);
    }

    public int Operation => (int)(Word_00 & 0xF);
    public int Question { get => (int)((Word_00 >> 8) & 0xFF); protected set => Word_00 = (Word_00 & 0xFFFF00FFu) | (((uint)value & 0xFF) << 8); }
    public bool Busy => (Word_00 & 0xF) != 0;
    public bool IsDone => (Word_04 & Done) != 0;
    public bool Succeeded => (Word_00 & Success) != 0;
    public bool Autosave { get => (Word_00 & AutosaveOn) != 0; set => Word_00 = value ? Word_00 | AutosaveOn : Word_00 & ~AutosaveOn; }

    // Amedo
    public static void SetTexts_100600()
    {
        ReadOnlySpan<int> ids =
        [
            -1, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0xB7, 0x32, 0x5F, 0x33, 0x34, 0x35, 0x36, 0x37, 0xB8,
            0xB4, 0xB5, 0xB6, 0x38, 0x39, 0x3A, 0xB9, 0x3B, 0x3C, 0x3D, 0x3E, 5, 0x3F, 0xCC, 0x41, 3, 2,
        ];
        for (int i = 1; i < Messages; i++) TextIds_3D1A08[i] = ids[i];
    }

    // Amedo
    public string Text_2A7730(int message) =>
        TextIds_3D1A08[message] != -1 ? Texts_309770?[TextIds_3D1A08[message]] ?? English_2E7C58[message] : English_2E7C58[message];

    // Amedo
    protected string MessageText_2A1738(int message)
    {
        int needed = Device_14.NeededSize_2A2898();
        string kb = (((uint)needed + 0x3FF) >> 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string text = Text_2A7730(message);
        while (Replace(ref text, "(x)", kb)) { }
        while (Replace(ref text, "(xxx)", Name_08)) { }
        return text;
    }

    private static bool Replace(ref string text, string find, string with)
    {
        int i = text.IndexOf(find, StringComparison.Ordinal);
        if (i < 0) return false;
        text = string.Concat(text.AsSpan(0, i), with, text.AsSpan(i + find.Length));
        return true;
    }

    public abstract void Message(int message, int slot);
    public abstract int Ask(int question);
    public abstract int ShowSlots(int question);

    // Amedo
    public void Request_2A11D0(int op, int minTime)
    {
        Word_04 &= ~Done;
        Word_00 = (Word_00 & 0xEFFFFF00u) | ((uint)op & 0xF) | (((uint)op & 0xF) << 4);
        Device_14.MinTime_0C = minTime;
        if (op < 3)
        {
            if (0 < op)
            {
                Message(1, 0);
                Question = 0;
                return;
            }
            if (op == 0) return;
        }
        else if (op == 6)
        {
            uint slot = Word_04 & 0xF;
            if ((Word_04 & FullDirectory) == 0) Message(8, (int)slot);
            else
            {
                Device_14.Word_00 = (Device_14.Word_00 & 0xFBFFFFFFu) | 0x4000000u;
                Message(5, 0);
            }
            Word_04 = (Word_04 & 0xFFFFFFF0u) | slot;
            return;
        }
        if (!Device_14.IsPresent_2A8510())
        {
            Ask(4);
            return;
        }
        if (!Device_14.IsFormatted_2A8520())
        {
            if (2 < op)
            {
                if (op < 5) Ask(5);
                else if (op == 5) Ask(7);
            }
            return;
        }
        Message(3, 0);
    }

    // Amedo
    public void Frame_2A13E0(int now)
    {
        var device = Device_14;
        int deviceOp = device.Operation;
        if ((Word_00 & 0xF0000) != 0) TakeAnswer_2A0778();
        if (device.Update_2A0250(now)) return;
        int op = Operation;
        int result = device.Result;
        if (deviceOp == 0) deviceOp = device.Operation;
        if (result == 0)
        {
            int q = Question;
            if (op < 6 && 3 <= op)
            {
                if (!device.IsPresent_2A8510())
                {
                    if (!(deviceOp < 0xE && 10 < deviceOp) && q != 10)
                    {
                        if (q < 11 ? q != 4 : 15 < q || q < 13) q = Ask(4);
                    }
                }
                else if (q == 0xD) device.IsFormatted_2A8520();
            }
            Waiting_2A1098(q);
            return;
        }
        if (result != 1) return;
        switch (deviceOp)
        {
            case 1:
                if (op != 1)
                {
                    Finish_2A75E0(false, false);
                    return;
                }
                Ask(1);
                return;
            case 2:
                Ask(0xD);
                return;
            case 3:
                if (op == 5)
                {
                    if (!device.IsPresent_2A8510())
                    {
                        Ask(4);
                        return;
                    }
                    Ask(0xF);
                    return;
                }
                if (5 < op)
                {
                    if (op == 6) Finish_2A75E0(false, false);
                    return;
                }
                if (op < 3) return;
                Ask(device.IsPresent_2A8510() ? 0xE : 4);
                return;
            case 4: case 5: case 7: case 8: case 9: case 10:
                if (op == 5)
                {
                    Ask(0xF);
                    return;
                }
                if (5 < op)
                {
                    if (op == 6) Finish_2A75E0(false, false);
                    return;
                }
                if (op < 3) return;
                Ask(0xE);
                return;
            case 6:
                if (op == 5) Ask(0xF);
                else if (op < 6)
                {
                    if (3 <= op) ShowSlots(0xB);
                }
                else if (op == 6) Finish_2A75E0(false, false);
                Word_04 |= FullDirectory;
                return;
        }
    }

    // Amedo
    private void TakeAnswer_2A0778()
    {
        int q = Question;
        Question = 0;
        int op = Operation;
        uint w = Word_00;
        uint kind = (w >> 16) & 0xF;
        Word_00 = w & 0xFFF0FFFFu;
        var device = Device_14;
        if (kind == 2)
        {
            switch (q)
            {
                case 3: case 4: case 5: case 7: case 8: case 0xB: case 0xC: case 0xD: case 0xE: case 0xF:
                    if (Region.RegionProfile.Current.Region == Region.GameRegion.Pal && CancelAnswer_Pal_2A12B0(op)) return; // Amedo 2026-11-11
                    Finish_2A75E0(false, false);
                    return;
                case 6:
                    Ask(5);
                    return;
                case 9:
                    ShowSlots(0xB);
                    return;
                case 10:
                    if (!device.IsPresent_2A8510()) Ask(4);
                    else if (device.IsFormatted_2A8520()) Message(3, 0);
                    else if (2 < op)
                    {
                        if (op < 5) Ask(5);
                        else if (op == 5) Ask(7);
                    }
                    return;
            }
            return;
        }
        if (2 < kind)
        {
            if (kind != 3 || q == 0) return;
            if (q < 9 || (11 <= q && q <= 15)) Finish_2A75E0(true, false);
            return;
        }
        if (kind != 1) return;
        int id = (int)((w >> 20) & 0xF);
        switch (q)
        {
            case 1: case 2:
                Message(1, 0);
                return;
            case 3:
                Message(4, 0);
                return;
            case 5:
                Ask(6);
                return;
            case 6:
                Message(2, 0);
                return;
            case 9:
                SaveToSlot(Word_00 >> 24 & 0xF);
                return;
            case 10:
                Message(0xD, 0);
                return;
            case 0xB:
            {
                var summary = device.Directory_14.Summaries_30[id]!;
                Word_00 = (Word_00 & 0xF0FFFFFFu) | ((uint)id << 24);
                if (!summary.Used)
                {
                    SaveClock.GetCurrentTime_191C80(summary.Date_00);
                    SaveToSlot(Word_00 >> 24 & 0xF);
                    return;
                }
                Ask(9);
                return;
            }
            case 0xC:
                Word_00 = (Word_00 & 0xF0FFFFFFu) | ((uint)id << 24);
                Message(9, id);
                return;
            case 0xD: case 0xE: case 0xF:
                if (!device.IsPresent_2A8510())
                {
                    Ask(4);
                    return;
                }
                if (device.IsFormatted_2A8520())
                {
                    Message(3, 0);
                    return;
                }
                if (q == 0xE) Ask(5);
                else if (q == 0xD) Message(2, 0);
                else Ask(7);
                return;
        }
    }

    // Amedo
    private void SaveToSlot(uint slot)
    {
        if ((Word_04 & FullDirectory) != 0)
        {
            Device_14.Word_00 = (Device_14.Word_00 & 0xFBFFFFFFu) | 0x4000000u;
            Message(5, 0);
        }
        else Message(8, (int)slot);
        Word_04 = (Word_04 & 0xFFFFFFF0u) | slot;
    }

    // Amedo
    private bool EnoughSpace()
    {
        var device = Device_14;
        uint free = (uint)device.FreeSpace_2A8628(), needed = (uint)device.NeededSize_2A2898();
        bool ok = needed <= free;
        if (device.HasDirectory_2A8530())
        {
            uint dir = (uint)device.DirectorySize_2A8540();
            if (dir == needed) ok = true;
            else ok = needed <= free + dir;
        }
        return ok;
    }

    // Amedo
    public virtual void OperationDone_2A0BB0(int deviceOp)
    {
        var device = Device_14;
        switch (Operation)
        {
            case 1:
                if (!device.IsPresent_2A8510())
                {
                    Ask(1);
                    return;
                }
                if (!device.IsFormatted_2A8520())
                {
                    Finish_2A75E0(true, false);
                    return;
                }
                if (deviceOp != 3)
                {
                    Message(3, 0);
                    return;
                }
                if (!EnoughSpace())
                {
                    Ask(2);
                    return;
                }
                Finish_2A75E0(true, false);
                return;
            case 2:
                Finish_2A75E0(true, Succeeded);
                return;
            case 3: case 4:
                break;
            case 5:
                switch (deviceOp)
                {
                    case 3:
                        if (!device.HasDirectory_2A8530())
                        {
                            Ask(7);
                            return;
                        }
                        Word_04 &= ~FullDirectory;
                        Message(6, 0);
                        return;
                    case 6:
                        ShowSlots(0xC);
                        return;
                    case 9:
                        Word_04 = (Word_04 & 0xFFFFFFF0u) | ((Word_00 >> 24) & 0xF);
                        Message(0xC, 0);
                        return;
                    case 0xC:
                        Finish_2A75E0(true, true);
                        return;
                }
                return;
            case 6:
                if (deviceOp == 8) Finish_2A75E0(true, true);
                return;
            default:
                return;
        }
        switch (deviceOp)
        {
            case 2:
                Message(10, 0);
                return;
            case 3:
                if (!EnoughSpace())
                {
                    Ask(8);
                    return;
                }
                if (!device.HasDirectory_2A8530())
                {
                    Ask(3);
                    return;
                }
                Word_04 &= ~FullDirectory;
                Message(6, 0);
                return;
            case 4: case 6:
                ShowSlots(0xB);
                return;
            case 5:
            {
                uint w = Word_04;
                if ((w & FullDirectory) != 0)
                {
                    Word_04 = w & ~FullDirectory;
                    Message(8, (int)(w & 0xF));
                    Word_04 = (Word_04 & 0xFFFFFFF0u) | (w & 0xF);
                    return;
                }
                ShowSlots(0xB);
                return;
            }
            case 8:
                Message(0xB, 0);
                return;
            case 10:
                Message(4, 0);
                return;
            case 0xB:
                Finish_2A75E0(true, true);
                return;
            case 0xD:
                Finish_2A75E0(false, false);
                return;
        }
    }

    // Amedo
    public virtual bool Waiting_2A1098(int question)
    {
        if (question == 0)
        {
            OperationDone_2A0BB0(Device_14.Operation);
            return true;
        }
        if (question != 4 || !Device_14.IsPresent_2A8510()) return false;
        int op = Operation;
        if (!Device_14.IsFormatted_2A8520())
        {
            if (2 < op)
            {
                if (op < 5) Ask(5);
                else if (op == 5) Ask(7);
            }
            return true;
        }
        Message(3, 0);
        return true;
    }

    // Amedo
    public virtual void Finish_2A75E0(bool done, bool success)
    {
        Word_04 = (Word_04 & ~Done) | (done ? Done : 0u);
        Word_00 = (Word_00 & 0xEFFFFF00u) | ((Word_00 & 0xF) << 4) | (success ? Success : 0u);
    }
}

// Amedo
public class SaveCodeScreens : SaveCode
{
    public readonly Oleg Oleg_1C;
    public int AllButOverlays_20 = -1, Message_24, Choices_28, Slots_2C;
    public string MessageText_30 = "", QuestionText_3C = "", SlotsText_48 = "";
    public StringLabel? MessageLabel_54, QuestionLabel_58, SlotsLabel_5C;
    public SaveChoicesPage? Choices_60;
    public SaveSlotsPageBase? Slots_64;

    // Amedo
    public SaveCodeScreens(string name, Oleg oleg, SaveDevice device, GameTexts? texts) : base(name, device, texts) => Oleg_1C = oleg;

    private static int Half => GameMath.Trunc(Widget.TicksPerSecond * 0.5f);

    // Amedo
    public override void Message(int message, int slot)
    {
        bool text = true;
        Question = 0;
        switch (Operation)
        {
            case 0: case 2:
                text = false;
                break;
            case 1: case 3: case 4: case 5:
                Oleg_1C.Hide_259BC0(Oleg_1C.Groups_08[AllButOverlays_20], Half, 0);
                Oleg_1C.Show_259B20(Oleg_1C.Groups_08[Message_24], Half, 0);
                break;
        }
        if (text)
        {
            MessageText_30 = MessageText_2A1738(MessageTexts_2E7CF0[message]);
            if (MessageLabel_54 is not null) MessageLabel_54.Text_90 = MessageText_30;
        }
        Device_14.Start_2A00A0(message, slot);
    }

    // Amedo
    public override int Ask(int question)
    {
        Question = question;
        if (Choices_60 is not null)
        {
            int mode = question switch
            {
                >= 1 and <= 10 => question - 1,
                13 => 10,
                14 => 11,
                15 => 12,
                _ => -1,
            };
            QuestionText_3C = MessageText_2A1738(QuestionTexts_2E7D28[question]);
            if (QuestionLabel_58 is not null) QuestionLabel_58.Text_90 = QuestionText_3C;
            Choices_60.SetChoices_2A1BE8(mode, Operation == 3);
        }
        Oleg_1C.Hide_259BC0(Oleg_1C.Groups_08[AllButOverlays_20], Half, 0);
        Oleg_1C.Show_259B20(Oleg_1C.Groups_08[Choices_28], Half, 0);
        return question;
    }

    // Amedo
    public override int ShowSlots(int question)
    {
        Question = question;
        if (Slots_64 is not null)
        {
            int mode = question == 0xB ? 1 : question == 0xC ? 0 : -1;
            SlotsText_48 = MessageText_2A1738(QuestionTexts_2E7D28[question]);
            if (SlotsLabel_5C is not null) SlotsLabel_5C.Text_90 = SlotsText_48;
            Slots_64.SetSlots_2A1830(mode, Operation == 3);
        }
        Oleg_1C.Hide_259BC0(Oleg_1C.Groups_08[AllButOverlays_20], Half, 0);
        Oleg_1C.Show_259B20(Oleg_1C.Groups_08[Slots_2C], Half, 0);
        return question;
    }
}

// Amedo
public sealed class SaveChoiceItem : LinkItem
{
    public const int Accept = 0, Cancel = 1, Continue = 2;

    public readonly SaveCode Owner_14;
    public readonly int Kind_18, Message_1C;

    // Amedo
    public SaveChoiceItem(int message, int id, int kind, SaveCode owner) : base("", id, null, 1)
    {
        Owner_14 = owner;
        Kind_18 = kind;
        Message_1C = message;
    }

    // Amedo
    public override MenuPage? Activate(int col, MenuPage page)
    {
        switch (Kind_18)
        {
            case Accept:
                Owner_14.Word_00 = (((Owner_14.Word_00 & 0xFFF0FFFFu) | 0x10000u) & 0xFF0FFFFFu) | ((Word_04 & 0xF) << 20);
                break;
            case Cancel:
                Owner_14.Word_00 = (Owner_14.Word_00 & 0xFFF0FFFFu) | 0x20000u;
                break;
            case Continue:
                Owner_14.Word_00 = (Owner_14.Word_00 & 0xFFF0FFFFu) | 0x30000u;
                break;
        }
        return null;
    }

    // Amedo
    public override void Entered(int col, int mode)
    {
        Name_00 = Owner_14.Text_2A7730(Message_1C);
        Word_04 &= 0xFF000FFFu;
    }
}

// Amedo
public sealed class SaveChoicesPage : MenuPage
{
    public const int Choices = 8;

    // Amedo
    public SaveChoicesPage(SaveCode owner) : base("", 1)
    {
        Add_25B710(new SaveChoiceItem(0x1F, 0, SaveChoiceItem.Accept, owner));
        Add_25B710(new SaveChoiceItem(0x20, 1, SaveChoiceItem.Continue, owner));
        Add_25B710(new SaveChoiceItem(0x21, 2, SaveChoiceItem.Continue, owner));
        Add_25B710(new SaveChoiceItem(0x22, 3, SaveChoiceItem.Accept, owner));
        Add_25B710(new SaveChoiceItem(0x23, 4, SaveChoiceItem.Accept, owner));
        Add_25B710(new SaveChoiceItem(0x1E, 5, SaveChoiceItem.Cancel, owner));
        Add_25B710(new SaveChoiceItem(0x24, 6, SaveChoiceItem.Accept, owner));
        Add_25B710(new SaveChoiceItem(0x25, 7, SaveChoiceItem.Cancel, owner));
    }

    // Amedo
    public void SetChoices_2A1BE8(int mode, bool canContinue)
    {
        Span<byte> on = stackalloc byte[Choices];
        byte flag = (byte)(canContinue ? 0xFF : 0);
        switch (mode)
        {
            case 0: case 1:
                on[4] = on[1] = 0xFF;
                break;
            case 2:
                on[2] = flag;
                on[5] = on[3] = 0xFF;
                break;
            case 3: case 6: case 7:
                on[2] = flag;
                on[5] = 0xFF;
                break;
            case 4:
                on[0] = 0xFF;
                on[2] = flag;
                on[5] = 0xFF;
                break;
            case 5: case 8: case 9:
                on[7] = on[6] = 0xFF;
                break;
            case 10: case 12:
                on[5] = on[4] = 0xFF;
                break;
            case 11:
                on[2] = flag;
                on[5] = on[4] = 0xFF;
                break;
            case 13:
                on[5] = 0xFF;
                break;
        }
        for (int i = 0; i < Choices; i++)
        {
            var item = Find_2573E0(i)!;
            item.Selectable_09 = on[i];
            if (on[i] != 0) Select_25B970(0, item.Id);
        }
    }
}

// Amedo
public class SaveSlotsPageBase : MenuPage
{
    public readonly SaveCode Owner_50;

    // Amedo
    public SaveSlotsPageBase(SaveCode owner) : base("", 1) => Owner_50 = owner;

    // Amedo
    protected void AddCancelItems_2A77A0()
    {
        Add_25B710(new SaveChoiceItem(0x21, 0, SaveChoiceItem.Continue, Owner_50));
        Add_25B710(new SaveChoiceItem(0x1E, 1, SaveChoiceItem.Cancel, Owner_50));
    }

    // Amedo
    public void SetSlots_2A1830(int mode, bool canContinue)
    {
        var device = Owner_50.Device_14;
        int count = device.SlotCount;
        Span<byte> on = stackalloc byte[2];
        if (mode == 0)
        {
            for (int i = 0; i < count; i++)
            {
                var item = Item_25A238(i)!;
                item.Shown_08 = (byte)(device.Directory_14.Summaries_30[i]!.Used ? 0xFF : 0);
            }
        }
        else
        {
            if (mode != 1) goto apply;
            for (int i = 0; i < count; i++) Item_25A238(i)!.Shown_08 = 0xFF;
            on[0] = (byte)(canContinue ? 0xFF : 0);
        }
        on[1] = 0xFF;
    apply:
        for (int i = 0; i < 2; i++)
        {
            var item = Find_2573E0(i)!;
            item.Selectable_09 = on[i];
            if (on[i] != 0) SetFirstItem_25B9C0(item.Id);
        }
    }
}
