namespace CrashEngine.Runtime.Game;

// Amedo
public sealed class SaveDevice
{
    public const int FreeKb = 8000;

    public uint Word_00, Word_04;
    public int Start_08, MinTime_0C;
    public readonly SaveFileSet Set_10;
    public readonly SaveDirectory Directory_14;
    public readonly SaveFile?[] Slots_18;
    public readonly SaveFile?[] List_1C;
    public int CardType_48, Free_4C, Format_50, DirSize_54;
    public readonly string? Root;
    public Action<string>? Log;
    private int _commandResult;

    // Amedo
    public SaveDevice(int slots, SaveFileSet set, SaveDirectory directory, string? root)
    {
        Set_10 = set;
        Directory_14 = directory;
        Root = root;
        Word_00 = (Word_00 & 0xFC0F00F0u) | 0xE000u | ((uint)slots & 0xF);
        Slots_18 = new SaveFile?[slots];
        List_1C = new SaveFile?[slots * 2];
    }

    // Amedo
    public void CopyStateFrom(SaveDevice old)
    {
        Word_00 = old.Word_00;
        Word_04 = old.Word_04;
        Start_08 = old.Start_08;
        MinTime_0C = old.MinTime_0C;
        CardType_48 = old.CardType_48;
        Free_4C = old.Free_4C;
        Format_50 = old.Format_50;
        DirSize_54 = old.DirSize_54;
    }

    public int State => (int)((Word_00 >> 20) & 0x3F);
    public int Operation => (int)((Word_00 >> 8) & 0xF);
    public int Result => (int)((Word_00 >> 16) & 0xF);
    public int SlotCount => (int)(Word_00 & 0xF);
    private int ListCount => (int)((Word_00 >> 27) & 0xF);

    // Amedo
    public bool Start_2A00A0(int op, int slot)
    {
        if (Operation == op) return true;
        Word_00 = (Word_00 & 0xFFFF0FFFu) | (((uint)op & 0xF) << 12);
        uint state;
        switch (op)
        {
            case 1: state = 2; break;
            case 2: state = 5; break;
            case 3: state = 8; break;
            case 4: state = 0xB; break;
            case 5: state = 0xE; break;
            case 6: state = 0x11; break;
            case 7: state = 0x14; break;
            case 8:
                Word_00 = (Word_00 & 0xFC0FFF0Fu) | (0x17u << 20) | (((uint)slot & 0xF) << 4);
                return true;
            case 9:
                Word_00 = (Word_00 & 0xFC0FFF0Fu) | (0x1Au << 20) | (((uint)slot & 0xF) << 4);
                return true;
            case 10: state = 0x1D; break;
            case 11: state = 0x1E; break;
            case 12: state = 0x1F; break;
            case 13: state = 0x20; break;
            default: return false;
        }
        Word_00 = (Word_00 & 0xFC0FFFFFu) | (state << 20);
        return true;
    }

    // Amedo
    public bool Update_2A0250(int now)
    {
        uint state = (uint)State;
        BeginFrame_2A8558();
        uint requested = (Word_00 >> 12) & 0xF;
        if (requested < 0xE)
        {
            Word_00 = (Word_00 & 0xFFFF00FFu) | (requested << 8) | 0xE000u;
            Start_08 = now;
        }
        Word_00 &= 0xFFF0FFFFu;
        bool timedOut = MinTime_0C <= now - Start_08;
        int next = (int)state;
        switch (state)
        {
            case 1: case 4: case 7: case 10: case 0x13: case 0x16: case 0x19: case 0x1C:
                next = 0;
                break;
            case 2:
                next = 3;
                break;
            case 3:
                if (now - Start_08 < MinTime_0C) next = State;
                else if (IsPresent_2A8510()) next = 4;
                else next = SetResult_2A7500(1);
                break;
            case 5: next = StartOp_2A2548(2, 6); break;
            case 6: next = Poll_2A2430(2, 7, timedOut); break;
            case 8: next = StartOp_2A2548(3, 9); break;
            case 9: next = Poll_2A2430(3, 10, timedOut); break;
            case 0xB:
                Directory_14.ClearSummaries_2A7F60();
                next = StartOp_2A2548(4, 0xC);
                break;
            case 0xC: next = Poll_2A2430(4, 0xD, timedOut); break;
            case 0xD:
                Word_00 |= 0x4000000u;
                next = 0xE;
                break;
            case 0xE: next = BuildWriteList_29FEF8(); break;
            case 0xF: next = WriteNext_29FC98(5, 0x10, now); break;
            case 0x10:
                Word_00 &= 0xFBFFFFFFu;
                next = 0;
                break;
            case 0x11:
                Directory_14.ClearSummaries_2A7F60();
                List_1C[0] = Directory_14;
                next = OpenList(1);
                break;
            case 0x12: next = ReadNext_29FB50(6, 0x13, now); break;
            case 0x14: next = StartOp_2A2548(7, 0x15); break;
            case 0x15: next = OpenThenRead_29FDE0(now); break;
            case 0x17:
            {
                var slot = Slots_18[(Word_00 >> 4) & 0xF]!;
                List_1C[0] = slot;
                List_1C[1] = Directory_14;
                Word_04 &= 0xFFFFFFF0u;
                Word_00 = (Word_00 & 0x87FFFFFFu) | 0x10000000u;
                next = slot.Save_2A1EF8() ? StartOp_2A2548(8, 0x18) : SetResult_2A7500(1);
                break;
            }
            case 0x18: next = WriteNext_29FC98(8, 0x19, now); break;
            case 0x1A:
                List_1C[0] = Slots_18[(Word_00 >> 4) & 0xF];
                next = OpenList(1);
                break;
            case 0x1B: next = ReadNext_29FB50(9, 0x1C, now); break;
            case 0x1D:
                next = IsPresent_2A8510() && now - Start_08 < MinTime_0C ? 0x1D : 0;
                break;
            case 0x1E:
                next = IsPresent_2A8510() && now - Start_08 < MinTime_0C ? 0x1E : 0;
                break;
            case 0x1F:
                next = IsPresent_2A8510() && now - Start_08 < MinTime_0C ? 0x1F : 0;
                break;
            case 0x20:
                next = now - Start_08 < MinTime_0C ? 0x20 : 0;
                break;
        }
        EndFrame_2A8590();
        uint w = Word_00;
        Word_00 = (w & 0xFC0FFFFFu) | (((uint)next & 0x3F) << 20);
        if (next == 0) Word_00 = w & 0xFC0F0FFFu;
        else if (next != 1) return true;
        return false;
    }

    // Amedo
    private int OpenList(int count)
    {
        Word_04 &= 0xFFFFFFF0u;
        Word_00 = (Word_00 & 0x87FFFFFFu) | (((uint)count & 0xF) << 27);
        return StartOp_2A2548(7, 0x15);
    }

    // Amedo
    public int SetResult_2A7500(int result)
    {
        Word_00 = (Word_00 & 0xFFF0FFFFu) | (((uint)result & 0xF) << 16);
        return 1;
    }

    // Amedo
    private int ReadNext_29FB50(int op, int next, int now)
    {
        int r = Poll_2A2430(op, next, MinTime_0C <= now - Start_08);
        if (r != next) return r;
        var file = List_1C[Word_04 & 0xF]!;
        if (file.Load_2A1D48())
        {
            uint i = ((Word_04 & 0xF) + 1) & 0xF;
            Word_04 = (Word_04 & 0xFFFFFFF0u) | i;
            if (ListCount <= (int)i) return r;
            if (List_1C[i]!.BeginRead_2A7C00()) return StartOp_2A2548(op, State);
        }
        return SetResult_2A7500(1);
    }

    // Amedo
    private int WriteNext_29FC98(int op, int next, int now)
    {
        int r = Poll_2A2430(op, next, MinTime_0C <= now - Start_08);
        if (r != next) return r;
        var file = List_1C[Word_04 & 0xF]!;
        if (file.EndWrite_2A7C60())
        {
            uint i = ((Word_04 & 0xF) + 1) & 0xF;
            Word_04 = (Word_04 & 0xFFFFFFF0u) | i;
            if (ListCount <= (int)i) return r;
            if (List_1C[i]!.Save_2A1EF8()) return StartOp_2A2548(op, State);
        }
        return SetResult_2A7500(1);
    }

    // Amedo
    private int OpenThenRead_29FDE0(int now)
    {
        int op = Operation;
        int wanted = op == 6 ? 0x12 : op == 9 ? 0x1B : 0x16;
        int r = Poll_2A2430(7, wanted, MinTime_0C <= now - Start_08);
        if (r is 0x12 or 0x1B)
        {
            if (!List_1C[Word_04 & 0xF]!.BeginRead_2A7C00()) return SetResult_2A7500(1);
            r = StartOp_2A2548(op, r);
        }
        return r;
    }

    // Amedo
    private int BuildWriteList_29FEF8()
    {
        int count;
        if ((Word_00 & 0x4000000u) == 0)
        {
            count = 1;
            List_1C[0] = Directory_14;
        }
        else
        {
            int n = 0;
            if (Set_10.Header_04 is { } header) List_1C[n++] = header;
            for (int i = 0; i < Set_10.Count_00; i++) List_1C[n++] = Set_10.Files_08[i];
            for (int i = 0; i < SlotCount; i++) List_1C[n++] = Slots_18[i];
            List_1C[n] = Directory_14;
            count = n + 1;
        }
        Word_04 &= 0xFFFFFFF0u;
        Word_00 = (Word_00 & 0x87FFFFFFu) | (((uint)count & 0xF) << 27);
        if (!List_1C[0]!.Save_2A1EF8()) return SetResult_2A7500(1);
        return StartOp_2A2548(5, 0xF);
    }

    // Amedo
    public int NeededSize_2A2898()
    {
        uint kb = 0, files = 0;
        if (Set_10.Header_04 is { } header)
        {
            files = 1;
            kb = ((uint)header.Size_20 + 0x3FF) >> 10;
        }
        for (int i = 0; i < Set_10.Count_00; i++)
        {
            files++;
            kb += ((uint)Set_10.Files_08[i]!.Size_20 + 0x3FF) >> 10;
        }
        files++;
        kb += ((uint)Directory_14.Size_20 + 0x3FF) >> 10;
        for (int i = 0; i < SlotCount; i++)
        {
            files++;
            kb += ((uint)Slots_18[i]!.Size_20 + 0x3FF) >> 10;
        }
        kb += (files + 1) >> 1;
        kb += 2;
        return (int)(kb << 10);
    }

    // Amedo
    private void BeginFrame_2A8558()
    {
        CardType_48 = Root is null ? 0 : 2;
        Format_50 = 1;
        Free_4C = FreeKb;
    }

    // Amedo
    private void EndFrame_2A8590() { }

    public bool IsPresent_2A8510() => CardType_48 == 2;
    public bool IsFormatted_2A8520() => Format_50 == 1;
    public bool HasDirectory_2A8530() => DirSize_54 >= 0;
    public int DirectorySize_2A8540() => DirSize_54 << 10;
    public int FreeSpace_2A8628() => Free_4C << 10;

    // Amedo
    private int Poll_2A2430(int op, int next, bool timedOut)
    {
        if (!IsPresent_2A8510()) return SetResult_2A7500(1);
        if (!timedOut) return State;
        if (_commandResult == 0) return SetResult_2A7500(1);
        if (_commandResult == 1) return next;
        return State;
    }

    // Amedo
    private int StartOp_2A2548(int op, int state)
    {
        bool started = true;
        switch (op)
        {
            case 2: _commandResult = 1; break;
            case 3: _commandResult = FindDirectory(); break;
            case 4: _commandResult = MakeDirectory(); break;
            case 5: case 8: _commandResult = WriteFile(List_1C[Word_04 & 0xF]!); break;
            case 6: case 9: _commandResult = ReadFile(List_1C[Word_04 & 0xF]!); break;
            case 7: _commandResult = FileInfo(List_1C[Word_04 & 0xF]!); break;
            default: started = false; break;
        }
        return started ? state : SetResult_2A7500(1);
    }

    private string DirectoryPath => Path.Combine(Root!, Directory_14.Name_00);

    // Amedo
    private int FindDirectory()
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
            {
                DirSize_54 = -1;
                return 1;
            }
            int kb = 0;
            foreach (var f in new DirectoryInfo(DirectoryPath).GetFiles()) kb += (int)((f.Length + 0x3FF) >> 10);
            DirSize_54 = kb;
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log?.Invoke($"[save] find directory failed: {ex.Message}");
            return 0;
        }
    }

    // Amedo
    private int MakeDirectory()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log?.Invoke($"[save] create directory failed: {ex.Message}");
            return 0;
        }
    }

    // Amedo
    private int WriteFile(SaveFile file)
    {
        if (file.Delivery) return 1;
        if (file.Stream_1C?.Buffer is not { } buffer) return 0;
        string path = Path.Combine(DirectoryPath, file.Name_00), temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            using (var fs = File.Create(temp)) fs.Write(buffer, 0, file.Size_20);
            File.Move(temp, path, true);
            Log?.Invoke($"[save] wrote {path} ({file.Size_20} bytes)");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log?.Invoke($"[save] write {path} failed: {ex.Message}");
            return 0;
        }
    }

    // Amedo
    private int ReadFile(SaveFile file)
    {
        if (file.Delivery || file.Stream_1C?.Buffer is not { } buffer) return 0;
        string path = Path.Combine(DirectoryPath, file.Name_00);
        try
        {
            if (!File.Exists(path)) return 0;
            using var fs = File.OpenRead(path);
            int total = 0, size = file.Size_20;
            while (total < size)
            {
                int n = fs.Read(buffer, total, size - total);
                if (n <= 0) break;
                total += n;
            }
            Log?.Invoke($"[save] read {path} ({total} bytes)");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log?.Invoke($"[save] read {path} failed: {ex.Message}");
            return 0;
        }
    }

    // Amedo
    private int FileInfo(SaveFile file)
    {
        if (file.Delivery) return 1;
        try
        {
            return File.Exists(Path.Combine(DirectoryPath, file.Name_00)) ? 1 : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
