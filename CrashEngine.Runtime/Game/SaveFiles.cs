namespace CrashEngine.Runtime.Game;

// Amedo
public static class SaveClock
{
    public const int DateBytes = 8;
    public const byte UsedFlags = 3;

    // Amedo
    public static void GetCurrentTime_191C80(Span<byte> date)
    {
        var now = DateTime.Now;
        date[0] = (byte)now.Day;
        date[1] = (byte)now.Month;
        BitConverter.TryWriteBytes(date[2..4], (ushort)(now.Year % 100));
        date[4] = (byte)now.Hour;
        date[5] = (byte)now.Minute;
        date[6] = (byte)now.Second;
        date[7] |= UsedFlags;
    }

    public static bool IsUsed(ReadOnlySpan<byte> date) => (date[7] & UsedFlags) == UsedFlags;
}

// Amedo
public class SaveFile
{
    public string Name_00;
    public readonly byte[] Date_0C = new byte[SaveClock.DateBytes];
    public int Version_14;
    public uint Checksum_18;
    public SaveStream? Stream_1C;
    public int Size_20;
    public byte[]? Buffer_24;
    private byte[]? _own;

    // Amedo
    public void CopyStateFrom(SaveFile old)
    {
        Array.Copy(old.Date_0C, Date_0C, SaveClock.DateBytes);
        Version_14 = old.Version_14;
        Checksum_18 = old.Checksum_18;
    }

    // Amedo
    public SaveFile(string name)
    {
        Name_00 = name;
    }

    // Amedo
    public SaveFile(string name, int version, int size, byte[]? buffer)
    {
        Name_00 = name;
        Version_14 = version;
        Size_20 = size;
        Buffer_24 = buffer;
    }

    public virtual bool Delivery => false;

    // Amedo
    public virtual SaveStream CreateStream_2A7A70()
    {
        var buffer = Buffer_24 ?? (_own is { } own && own.Length >= Size_20 ? own : _own = new byte[Size_20]);
        Array.Fill(buffer, (byte)0xA5, 0, Size_20);
        return new SaveStream(buffer, Size_20);
    }

    public virtual void GatherSettings() { }
    public virtual void ReadData(SaveStream stream) { }
    public virtual void WriteData(SaveStream stream) { }

    // Amedo
    public virtual bool BeginRead_2A7C00()
    {
        Stream_1C = CreateStream_2A7A70();
        return Stream_1C is not null;
    }

    // Amedo
    public static uint Checksum(byte[] buffer, int size)
    {
        uint h = (uint)size;
        uint s1 = 0x18, s2 = 0x18;
        uint b = buffer[0];
        uint count = (uint)size - 4;
        for (uint i = 0; i < count; i++)
        {
            uint a = b << (int)(s1 & 31), c = b << (int)(s2 & 31);
            if (!(s2 < 8)) s2 += 0x18;
            if (!(s1 < 6)) s1 += 0x18;
            h = (h + a) ^ c;
            s1 -= 5;
            s2 -= 7;
        }
        return h;
    }

    // Amedo
    public virtual bool Load_2A1D48()
    {
        bool ok = true;
        var stream = Stream_1C;
        if (Version_14 != 0 && stream is not null)
        {
            stream.Seek_6C(Size_20 - 8);
            int version = stream.ReadCount_8C();
            Checksum_18 = (uint)stream.ReadCount_8C();
            ok = false;
            if (version == Version_14)
            {
                ok = true;
                if (Checksum_18 != 0) ok = Checksum(stream.Buffer!, Size_20) == Checksum_18;
            }
            if (ok)
            {
                stream.Rewind_54();
                stream.ReadBytes_14(Date_0C);
                ReadData(stream);
            }
        }
        Stream_1C = null;
        return ok;
    }

    // Amedo
    public virtual bool Save_2A1EF8()
    {
        var stream = Stream_1C = CreateStream_2A7A70();
        GatherSettings();
        SaveClock.GetCurrentTime_191C80(Date_0C);
        if (Version_14 == 0)
        {
            WriteData(stream);
            return true;
        }
        stream.WriteBytes_24(Date_0C);
        WriteData(stream);
        Checksum_18 = Checksum(stream.Buffer!, Size_20);
        stream.Seek_6C(Size_20 - 8);
        stream.WriteCount_DC(Version_14);
        stream.WriteCount_DC((int)Checksum_18);
        return true;
    }

    // Amedo
    public virtual bool EndWrite_2A7C60()
    {
        Stream_1C = null;
        return true;
    }
}

// Amedo
public sealed class SaveSummary
{
    public readonly byte[] Date_00 = new byte[SaveClock.DateBytes];
    public string Name_08 = "";
    public uint Word_18;
    public int TimePlayed_1C;

    public bool Used => SaveClock.IsUsed(Date_00);
    public int Level => (int)(Word_18 & 0x3F);
    public int Character => (int)((Word_18 >> 6) & 0xF);
    public int Crystals => (int)((Word_18 >> 10) & 0x3F);
    public int Lives => (int)((Word_18 >> 16) & 0x7F);
    public int Percent => (int)((Word_18 >> 23) & 0x7F);

    // Amedo
    public void Clear_2A73F8() => Array.Clear(Date_00);

    // Amedo
    public void CopyFrom(SaveSummary old)
    {
        Array.Copy(old.Date_00, Date_00, SaveClock.DateBytes);
        Name_08 = old.Name_08;
        Word_18 = old.Word_18;
        TimePlayed_1C = old.TimePlayed_1C;
    }

    // Amedo
    public void Touch_2A7418()
    {
        if (Used) SaveClock.GetCurrentTime_191C80(Date_00);
    }

    // Amedo
    public void MakeBankSummary_179AD0(SaveController controller)
    {
        uint w0 = controller.Word_00;
        uint level = controller.Word_04 & 0x1F;
        uint character = (w0 >> 24) & 0xF;
        uint lives = w0 & 0x7F;
        uint crystals = (w0 & 0x1F80) << 3;
        uint percent = (w0 >> 13) & 0x7F;
        Word_18 = (Word_18 & 0xC0000000u) | level | (character << 6) | (lives << 16) | crystals | (percent << 23);
        TimePlayed_1C = controller.TimePlayed_08;
        Touch_2A7418();
    }

    // Amedo
    public void ReadBase_2A7448(SaveStream stream)
    {
        stream.ReadBytes_14(Date_00);
        Name_08 = stream.ReadString_204260();
    }

    // Amedo
    public void WriteBase_2A74A0(SaveStream stream)
    {
        stream.WriteBytes_24(Date_00);
        stream.WriteString_204320(Name_08);
    }

    // Amedo
    public void Read_179B98(SaveStream stream)
    {
        Span<byte> word = stackalloc byte[4];
        stream.ReadBytes_14(word);
        Word_18 = BitConverter.ToUInt32(word);
        TimePlayed_1C = stream.ReadInt_AC();
        ReadBase_2A7448(stream);
    }

    // Amedo
    public void Write_179C08(SaveStream stream)
    {
        Span<byte> word = stackalloc byte[4];
        BitConverter.TryWriteBytes(word, Word_18);
        stream.WriteBytes_24(word);
        stream.WriteInt_FC(TimePlayed_1C);
        WriteBase_2A74A0(stream);
    }

    // Amedo
    public string Text_29F9B8()
    {
        static string Pad(byte v) => (v < 10 ? ":0" : ":") + v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return Name_08 + " - " + Date_00[4].ToString(inv) + Pad(Date_00[5]) + Pad(Date_00[6]) + " " + Date_00[0].ToString(inv) + "/" +
               Date_00[1].ToString(inv) + "/" + BitConverter.ToUInt16(Date_00, 2).ToString(inv);
    }
}

// Amedo
public sealed class SaveDirectory : SaveFile
{
    public readonly SaveSummary?[] Summaries_30;

    // Amedo
    public SaveDirectory(string name, int count, int version, int size, byte[]? buffer) : base(name, version, size, buffer) =>
        Summaries_30 = new SaveSummary?[count];

    public int Count_2C => Summaries_30.Length;

    // Amedo
    public override void ReadData(SaveStream stream)
    {
        for (int i = 0; i < Count_2C; i++) Summaries_30[i]!.Read_179B98(stream);
    }

    // Amedo
    public override void WriteData(SaveStream stream)
    {
        for (int i = 0; i < Count_2C; i++) Summaries_30[i]!.Write_179C08(stream);
    }

    // Amedo
    public void ClearSummaries_2A7F60()
    {
        for (int i = 0; i < Count_2C; i++) Summaries_30[i]!.Clear_2A73F8();
    }
}

// Amedo
public sealed class SaveFileSet
{
    public byte Count_00;
    public readonly SaveFile? Header_04;
    public readonly SaveFile?[] Files_08;

    // Amedo
    public SaveFileSet(int capacity, SaveFile? header)
    {
        Header_04 = header;
        Files_08 = new SaveFile?[capacity];
    }

    // Amedo
    public void Add_2A80D0(SaveFile file) => Files_08[Count_00++] = file;
}

// Amedo
public sealed class IconSysFile : SaveFile
{
    public const int Size = 0x3C4;

    public readonly string Title_38;
    public readonly string Icon;

    // Amedo
    public IconSysFile(string title, string icon) : base("icon.sys", 0, Size, null)
    {
        Title_38 = title;
        Icon = icon;
    }

    public override bool Delivery => true;
}

// Amedo
public sealed class IconFile : SaveFile
{
    public readonly string DiscPath;

    // Amedo
    public IconFile(string discPath, string name, int size) : base(name, 0, size, null) => DiscPath = discPath;

    public override bool Delivery => true;
    public override bool BeginRead_2A7C00() => false;
    public override bool Load_2A1D48() => false;
    public override bool Save_2A1EF8() => true;
    public override bool EndWrite_2A7C60() => true;
}
