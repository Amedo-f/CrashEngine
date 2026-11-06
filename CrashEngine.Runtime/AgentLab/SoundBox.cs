using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed class SoundBox
{
    public PS2AnyTrigger Source { get; }

    // Amedo
    public SoundBox(PS2AnyTrigger source)
    {
        Source = source;
    }
}
