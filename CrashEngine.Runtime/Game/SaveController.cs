using System.Numerics;
using System.Text;

namespace CrashEngine.Runtime.Game;

// Amedo
public sealed class SaveStream
{
    private readonly MemoryStream _stream;
    private readonly BinaryWriter? _writer;
    private readonly BinaryReader? _reader;

    // Amedo
    public SaveStream(byte[] buffer, bool write)
    {
        _stream = new MemoryStream(buffer, true);
        if (write) _writer = new BinaryWriter(_stream, Encoding.Latin1, true);
        else _reader = new BinaryReader(_stream, Encoding.Latin1, true);
    }

    // Amedo
    public SaveStream(MemoryStream stream, bool write)
    {
        _stream = stream;
        if (write) _writer = new BinaryWriter(_stream, Encoding.Latin1, true);
        else _reader = new BinaryReader(_stream, Encoding.Latin1, true);
    }

    // Amedo
    public SaveStream(byte[] buffer, int length)
    {
        Buffer = buffer;
        _stream = new MemoryStream(buffer, 0, length, true);
        _writer = new BinaryWriter(_stream, Encoding.Latin1, true);
        _reader = new BinaryReader(_stream, Encoding.Latin1, true);
    }

    public byte[]? Buffer { get; }
    public long Position => _stream.Position;
    public void Rewind_54() => _stream.Position = 0;
    public void Seek_6C(int position) => _stream.Position = position;

    public void WriteBytes_24(ReadOnlySpan<byte> data) => _writer!.Write(data);
    public void ReadBytes_14(Span<byte> data) => _reader!.Read(data);
    public void WriteInt_FC(int v) => _writer!.Write(v);
    public int ReadInt_AC() => _reader!.ReadInt32();
    public void WriteUShort_CC(ushort v) => _writer!.Write(v);
    public ushort ReadUShort_7C() => _reader!.ReadUInt16();
    public void WriteFloat_10C(float v) => _writer!.Write(v);
    public float ReadFloat_BC() => _reader!.ReadSingle();
    public void WriteBool_104(bool v) => _writer!.Write((byte)(v ? 1 : 0));
    public bool ReadBool_B4() => _reader!.ReadByte() != 0;
    public void WriteCount_DC(int v) => _writer!.Write(v);
    public int ReadCount_8C() => _reader!.ReadInt32();

    // Amedo
    public void WriteString_204320(string s)
    {
        var bytes = Encoding.Latin1.GetBytes(s);
        _writer!.Write(bytes.Length);
        _writer.Write(bytes);
    }

    // Amedo
    public string ReadString_204260()
    {
        int n = _reader!.ReadInt32();
        return Encoding.Latin1.GetString(_reader.ReadBytes(n));
    }
}

// Amedo
public interface ISaveOptions
{
    bool Vibration_30971C { get; set; }
    Vector2 ScreenOffset_309728 { get; set; }
    float SoundLevel_1E60A8(int group);
    void SetSoundLevel_1E5FE8(float level, int group);
    int OutputMode_309AC8 { get; }
    void SetOutputMode_1E6190(int mode);
    bool Widescreen_30A744 { get; set; }
}

// Amedo
public sealed class SaveController
{
    public const int BufferSize = 0xF000;

    public uint Word_00, Word_04;
    public int TimePlayed_08;
    public string Name_0C = "";
    public ushort Checkpoint_18 = 0xFFFF;
    public Vector2 ScreenOffset_1C;
    public float Effects_24, Music_28;
    public readonly byte[] Buffer_2C = new byte[BufferSize];

    public int Lives { get => (int)(Word_00 & 0x7F); set => Word_00 = (Word_00 & ~0x7Fu) | ((uint)value & 0x7F); }

    // Amedo
    public void CopyFrom(SaveController other)
    {
        Word_00 = other.Word_00;
        Word_04 = other.Word_04;
        TimePlayed_08 = other.TimePlayed_08;
        Name_0C = other.Name_0C;
        Checkpoint_18 = other.Checkpoint_18;
        ScreenOffset_1C = other.ScreenOffset_1C;
        Effects_24 = other.Effects_24;
        Music_28 = other.Music_28;
        Array.Copy(other.Buffer_2C, Buffer_2C, BufferSize);
    }

    // Amedo
    public void Take_168810(GameProgress progress, string? chunkName, Action<SaveStream> writeChunks, ushort? checkpoint, int nowTicks, ISaveOptions options)
    {
        var stream = new SaveStream(Buffer_2C, true);
        Name_0C = chunkName ?? progress.StartChunk_10;
        Checkpoint_18 = checkpoint ?? 0xFFFF;
        uint w0 = (Word_00 & 0xFFFFFF80u) | ((progress.Word_00 >> 7) & 0x7F);
        uint crystals = 0;
        for (int i = 0; i < GameProgress.LevelWords; i++) crystals += (progress.LevelWords_34[i] >> 8) & 1;
        w0 = (w0 & 0xFFFFE07Fu) | ((crystals & 0x3F) << 7);
        uint gems = 0;
        for (int gem = 0; gem < GameProgress.Gems; gem++)
            for (int i = 0; i < GameProgress.LevelWords; i++)
                if ((progress.LevelWords_34[i] & 0xFF & (1u << gem)) != 0) gems++;
        uint percent = ((uint)GameProgress.LevelPercent_2E6CA8[(progress.Word_04 >> 21) & 0x1F] + gems / 3) & 0x7F;
        w0 = (w0 & 0xFFF0007Fu) | ((crystals & 0x3F) << 7) | (percent << 13);
        uint high = progress.Word_04 >> 16;
        Word_04 = (Word_04 & 0xFFFF8000u) | (high & 0x1F) | (high & 0x3E0) | (high & 0x7C00);
        w0 = (w0 & 0x0000007Fu) | ((crystals & 0x3F) << 7) | (percent << 13) | (((progress.Word_04 >> 4) & 0xF) << 20) |
             (((progress.Word_04 >> 8) & 0xF) << 24) | ((progress.Word_04 >> 12) << 28);
        Word_00 = w0;
        int running = progress.TimeStamp_0C != 0 ? nowTicks - progress.TimeStamp_0C : 0;
        TimePlayed_08 = running + progress.TimePlayed_08;
        for (int i = 0; i < GameProgress.LevelWords; i++) GameProgress.WriteLevelWord_17A160(progress.LevelWords_34[i], stream);
        writeChunks(stream);
        TakeOptions_1798F8(options);
    }

    // Amedo
    public void TakeOptions_1798F8(ISaveOptions options)
    {
        Word_04 = (Word_04 & 0xFFFF7FFFu) | (options.Vibration_30971C ? 0x8000u : 0u);
        Effects_24 = options.SoundLevel_1E60A8(0);
        Music_28 = options.SoundLevel_1E60A8(2);
        uint output = ((uint)options.OutputMode_309AC8 & 0xF) << 17;
        Word_04 = (Word_04 & 0xFFE0FFFFu) | output | (options.Widescreen_30A744 ? 0x10000u : 0u);
        ScreenOffset_1C = options.ScreenOffset_309728;
    }

    // Amedo
    public void Restore_1685C0(bool full, GameProgress progress, Action<SaveStream> readChunks, int nowTicks, ISaveOptions options)
    {
        progress.Word_00 = (progress.Word_00 & 0xFFFFC07Fu) | ((Word_00 & 0x7F) << 7);
        uint w4 = progress.Word_04 & 0x8000000Fu;
        w4 |= (Word_04 & 0x1F) << 16;
        w4 |= ((Word_04 >> 5) & 0x1F) << 21;
        w4 |= ((Word_04 >> 10) & 0x1F) << 26;
        w4 |= (Word_00 >> 16) & 0xF0;
        w4 |= ((Word_00 >> 24) & 0xF) << 8;
        w4 |= (Word_00 >> 16) & 0xF000;
        progress.Word_04 = w4;
        progress.TimePlayed_08 = TimePlayed_08;
        progress.TimeStamp_0C = nowTicks;
        if (full)
        {
            var stream = new SaveStream(Buffer_2C, false);
            for (int i = 0; i < GameProgress.LevelWords; i++) progress.LevelWords_34[i] = GameProgress.ReadLevelWord_17A128(stream);
            readChunks(stream);
        }
        options.Vibration_30971C = (Word_04 & 0x8000) != 0;
        options.SetSoundLevel_1E5FE8(Effects_24, 0);
        options.SetSoundLevel_1E5FE8(Effects_24, 1);
        options.SetSoundLevel_1E5FE8(Effects_24, 3);
        options.SetSoundLevel_1E5FE8(Music_28, 2);
        options.SetOutputMode_1E6190((int)((Word_04 >> 17) & 0xF));
        options.Widescreen_30A744 = ((Word_04 >> 16) & 1) != 0;
        options.ScreenOffset_309728 = ScreenOffset_1C;
    }

    // Amedo
    public void Write_1684D0(SaveStream stream)
    {
        Span<byte> head = stackalloc byte[8];
        BitConverter.TryWriteBytes(head, Word_00);
        BitConverter.TryWriteBytes(head[4..], Word_04);
        stream.WriteBytes_24(head);
        stream.WriteInt_FC(TimePlayed_08);
        stream.WriteString_204320(Name_0C);
        stream.WriteUShort_CC(Checkpoint_18);
        Span<byte> offset = stackalloc byte[8];
        BitConverter.TryWriteBytes(offset, ScreenOffset_1C.X);
        BitConverter.TryWriteBytes(offset[4..], ScreenOffset_1C.Y);
        stream.WriteBytes_24(offset);
        stream.WriteFloat_10C(Effects_24);
        stream.WriteFloat_10C(Music_28);
        stream.WriteBytes_24(Buffer_2C);
    }

    // Amedo
    public void Read_1683D8(SaveStream stream)
    {
        Span<byte> head = stackalloc byte[8];
        stream.ReadBytes_14(head);
        Word_00 = BitConverter.ToUInt32(head);
        Word_04 = BitConverter.ToUInt32(head[4..]);
        TimePlayed_08 = stream.ReadInt_AC();
        Name_0C = stream.ReadString_204260();
        Checkpoint_18 = stream.ReadUShort_7C();
        Span<byte> offset = stackalloc byte[8];
        stream.ReadBytes_14(offset);
        ScreenOffset_1C = new Vector2(BitConverter.ToSingle(offset), BitConverter.ToSingle(offset[4..]));
        Effects_24 = stream.ReadFloat_BC();
        Music_28 = stream.ReadFloat_BC();
        stream.ReadBytes_14(Buffer_2C);
    }
}
