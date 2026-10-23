namespace CrashEngine.Runtime.Game;

// Amedo
public sealed class GameProgress
{
    public const int LevelWords = 16, Gems = 6;
    public const uint CrystalBit = 0x100;

    public static readonly int[] LevelSlot_2E6C40 = [0, 1, 1, 2, 3, 0, 4, 5, 5, 6, 7, 7, 4, 8, -1, 9, 9, 10, 11, 8, 12, 13, 14, 15, 12, 0];
    public static readonly byte[] LevelPercent_2E6CA8 = [0, 2, 6, 8, 10, 16, 16, 18, 22, 24, 26, 30, 36, 36, 36, 38, 42, 44, 46, 52, 52, 55, 58, 61, 68, 0, 0, 0, 0, 0, 0, 0];

    public uint Word_00, Word_04 = 1u << 4 | 6u << 8 | 6u << 12;
    public int TimePlayed_08, TimeStamp_0C;
    public uint Field_28, Field_2C, Field_30;
    public string StartChunk_10 = "", Chunk_1C = "", Chunk_28 = ""; // Amedo
    public readonly uint[] LevelWords_34 = new uint[LevelWords];
    public float BossIconWidth_74;
    public int Field_78, WormTicks_7C;

    public GameProgress() => Word_00 = 5u << 7;

    public int Wumpa { get => (int)(Word_00 & 0x7F); set => Word_00 = (Word_00 & ~0x7Fu) | ((uint)value & 0x7F); }
    public int Lives { get => (int)((Word_00 >> 7) & 0x7F); set => Word_00 = (Word_00 & ~(0x7Fu << 7)) | (((uint)value & 0x7F) << 7); }
    public int BossHealthMax { get => (int)((Word_00 >> 14) & 7); set => Word_00 = (Word_00 & ~(7u << 14)) | (((uint)value & 7) << 14); }
    public int BossHealthNow { get => (int)((Word_00 >> 17) & 7); set => Word_00 = (Word_00 & ~(7u << 17)) | (((uint)value & 7) << 17); }
    public int WormTotal { get => (int)((Word_00 >> 20) & 0x3F); set => Word_00 = (Word_00 & ~(0x3Fu << 20)) | (((uint)value & 0x3F) << 20); }
    public int WormCount { get => (int)(Word_00 >> 26); set => Word_00 = (Word_00 & 0x03FFFFFFu) | ((uint)value << 26); }
    public int HudMode { get => (int)(Word_04 & 0xF); set => Word_04 = (Word_04 & ~0xFu) | ((uint)value & 0xF); }
    public int Character => (int)((Word_04 >> 8) & 0xF);
    public int Level => (int)((Word_04 >> 16) & 0x1F);

    // Amedo
    public ref uint LevelWord(int level)
    {
        int slot = LevelSlot_2E6C40[level];
        if (slot < 0) return ref Field_30;
        return ref LevelWords_34[slot];
    }

    // Amedo
    public static uint ReadLevelWord_17A128(SaveStream stream)
    {
        Span<byte> word = stackalloc byte[4];
        stream.ReadBytes_14(word);
        return BitConverter.ToUInt32(word);
    }

    // Amedo
    public static void WriteLevelWord_17A160(uint value, SaveStream stream)
    {
        Span<byte> word = stackalloc byte[4];
        BitConverter.TryWriteBytes(word, value);
        stream.WriteBytes_24(word);
    }

    // Amedo
    public void SetTimePlayed_179680(int time, int nowTicks)
    {
        TimePlayed_08 = time;
        TimeStamp_0C = nowTicks;
    }

    // Amedo
    public void CopyFrom(GameProgress other) // Amedo
    {
        Word_00 = other.Word_00;
        Word_04 = other.Word_04;
        TimePlayed_08 = other.TimePlayed_08;
        TimeStamp_0C = other.TimeStamp_0C;
        Field_28 = other.Field_28;
        Field_2C = other.Field_2C;
        Field_30 = other.Field_30;
        StartChunk_10 = other.StartChunk_10;
        Chunk_1C = other.Chunk_1C;
        Chunk_28 = other.Chunk_28;
        Array.Copy(other.LevelWords_34, LevelWords_34, LevelWords);
        BossIconWidth_74 = other.BossIconWidth_74;
        Field_78 = other.Field_78;
        WormTicks_7C = other.WormTicks_7C;
    }

    public void Reset_167D38(int kind, int nowTicks)
    {
        Field_78 = 0;
        WormTicks_7C = 0;
        Word_04 &= 0xFFFFFFF0u;
        Word_00 &= 0xFFF03FFFu;
        if (kind == 2)
        {
            Word_00 &= 0xFFFFFF80u;
            return;
        }
        if (kind >= 2) return;
        Word_00 = (Word_00 & 0xFFFFC000u) | 0x280;
        Word_04 = (Word_04 & 0x8000000Fu) | 0x6610;
        SetTimePlayed_179680(0, nowTicks);
        for (int i = 0; i < LevelWords; i++) LevelWords_34[i] = 0;
    }

    // Amedo
    public static bool MarkCrystal_17A198(ref uint word)
    {
        if (((word >> 8) & 1) != 0) return false;
        word |= CrystalBit;
        return true;
    }

    // Amedo
    public static bool MarkGem_17A1C0(ref uint word, int gem)
    {
        uint bit = 1u << gem;
        if ((word & 0xFF & bit) != 0) return false;
        word = (word & ~0xFFu) | ((word & 0xFF) | bit) & 0xFF;
        return true;
    }

    // Amedo
    public int Percent_1795E8()
    {
        int gems = 0;
        for (int gem = 0; gem < Gems; gem++)
            for (int level = 0; level < LevelWords; level++)
                if ((LevelWords_34[level] & 0xFF & (1u << gem)) != 0) gems++;
        return LevelPercent_2E6CA8[(Word_04 >> 21) & 0x1F] + gems / 3;
    }

    // Amedo
    public int GemsFound_1795A8(int gem)
    {
        uint bit = 1u << gem;
        int found = 0;
        for (int i = 0; i < LevelWords; i++)
            if ((LevelWords_34[i] & 0xFF & bit) != 0) found++;
        return found;
    }

    // Amedo
    public bool AddHealth_1796B8(int n)
    {
        int health = BossHealthNow + n;
        if (health < 0)
        {
            BossHealthNow = 0;
            return true;
        }
        int max = BossHealthMax;
        if (health < max)
        {
            BossHealthNow = health & 7;
            return false;
        }
        BossHealthNow = max;
        return true;
    }

    // Amedo
    public bool AddToCount_179748(int n)
    {
        int count = WormCount + n;
        if (count < 0)
        {
            WormCount = 0;
            return true;
        }
        int total = WormTotal;
        if (count < total)
        {
            WormCount = count;
            return false;
        }
        WormCount = total;
        return true;
    }
}
