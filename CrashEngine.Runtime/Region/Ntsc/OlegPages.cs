namespace CrashEngine.Runtime.Screen.Oleg;

// Amedo 2026-11-11
public sealed partial class ExtrasPage
{
    public const int McDonaldsItem = 0x16;
    public bool Unlocked_248;

    private void AddPromoItems_Ntsc_163710(Oleg oleg)
    {
        Unlocked_248 = false;
        Add_25B710(new MovieItem(oleg, "spyro trailer", 0x15, 0x13));
        Add_25B710(new PictureItem(oleg, "mcdonalds extras", McDonaldsItem, @"Extras\McDonalds", 1, 1));
    }

    private void Frame_Ntsc_163458()
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
