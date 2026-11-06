using CrashEngine.Runtime.Screen;
using CrashEngine.Runtime.Screen.Oleg;

namespace CrashEngine.Runtime.Game;

// Amedo
public sealed class BankFile : SaveFile
{
    public const int Version = 1, Size = 0xF400;

    public readonly SaveSummary Summary_2C = new();
    public readonly SaveController Controller_594;
    public readonly ISaveOptions Game_4C;

    // Amedo
    public BankFile(string name, byte[] buffer, SaveController controller, ISaveOptions game) : base(name, Version, Size, buffer)
    {
        Controller_594 = controller;
        Game_4C = game;
    }

    // Amedo
    public override void GatherSettings()
    {
        Controller_594.TakeOptions_1798F8(Game_4C);
        Summary_2C.MakeBankSummary_179AD0(Controller_594);
    }

    // Amedo
    public override void ReadData(SaveStream stream) => Controller_594.Read_1683D8(stream);

    // Amedo
    public override void WriteData(SaveStream stream) => Controller_594.Write_1684D0(stream);
}

// Amedo
public sealed class SaveManager : SaveCodeScreens
{
    public const string GameName = "crash twinsanity", Title = "Crash~Twinsanity", IconName = "Crash.ico", IconPath = @"StartUp\Crash.ico";
    public const string DirectoryName = "BASLUS-20909";
    public const int Slots = 4, DirectorySize = 0x800;
    public readonly SaveDirectory Directory_470;
    public readonly BankFile[] Banks_580 = new BankFile[Slots];
    public readonly byte[] Buffer_30BBA8;

    // Amedo
    public SaveManager(Oleg oleg, SaveController controller, ISaveOptions game, GameTexts? texts, string? root, int iconSize, Action<string>? log)
        : this(oleg, controller, game, texts, BuildDevice_169348(root, iconSize, log))
    {
    }

    private SaveManager(Oleg oleg, SaveController controller, ISaveOptions game, GameTexts? texts, (SaveDevice Device, IconSysFile Header, SaveFileSet Set, SaveDirectory Directory, byte[] Buffer) parts)
        : base(GameName, oleg, parts.Device, texts)
    {
        Directory_470 = parts.Directory;
        Buffer_30BBA8 = parts.Buffer;
        for (int i = 0; i < Slots; i++)
        {
            var bank = new BankFile("Bank" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin", Buffer_30BBA8, controller, game);
            Banks_580[i] = bank;
            Directory_470.Summaries_30[i] = bank.Summary_2C;
            Device_14.Slots_18[i] = bank;
        }
    }

    // Amedo
    public void CopyStateFrom(SaveManager old)
    {
        Word_00 = old.Word_00;
        Word_04 = old.Word_04;
        Name_08 = old.Name_08;
        Device_14.CopyStateFrom(old.Device_14);
        Directory_470.CopyStateFrom(old.Directory_470);
        for (int i = 0; i < Slots; i++)
        {
            Banks_580[i].CopyStateFrom(old.Banks_580[i]);
            Banks_580[i].Summary_2C.CopyFrom(old.Banks_580[i].Summary_2C);
        }
    }

    // Amedo
    private static (SaveDevice, IconSysFile, SaveFileSet, SaveDirectory, byte[]) BuildDevice_169348(string? root, int iconSize, Action<string>? log)
    {
        var buffer = new byte[BankFile.Size];
        var header = new IconSysFile(Title, IconName);
        var set = new SaveFileSet(3, header);
        var directory = new SaveDirectory(DirectoryName, Slots, 1, DirectorySize, buffer);
        var device = new SaveDevice(Slots, set, directory, root) { Log = log };
        set.Add_2A80D0(new IconFile(IconName, iconSize));
        return (device, header, set, directory, buffer);
    }
}
