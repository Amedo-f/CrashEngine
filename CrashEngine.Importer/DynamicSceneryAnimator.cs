using CrashEngine.Core;
using CrashEngine.Renderer;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;

namespace CrashEngine.Importer;

// Amedo 2026-09-26
public sealed class DynamicSceneryAnimator : Component
{
    public const float Fps = DynamicScenerySampler.Fps;

    private readonly TwinDynamicSceneryAnimation _anim;

    public int   FrameCount { get; }
    public float Frame      { get; set; }
    public bool  Playing    { get; set; } = true;
    public TwinDynamicSceneryModel? Model { get; init; } // Amedo 2026-10-06
    public TwinDynamicSceneryAnimation Animation => _anim; // Amedo 2026-10-06
    public bool LogicDriven { get; set; } // Amedo 2026-10-06

    public DynamicSceneryAnimator(TwinDynamicSceneryAnimation anim, int frameCount)
    {
        _anim      = anim;
        FrameCount = frameCount;
    }

    public override void OnUpdate()
    {
        if (LogicDriven) return; // Amedo 2026-10-06
        if (Playing && FrameCount > 0 && RenderPipeline.Instance is { } rp)
            Frame = (float)(rp.TimeSeconds * (double)Fps % FrameCount);
        Apply();
    }

    public void Apply()
    {
        var (pos, rot) = DynamicScenerySampler.Sample(_anim, FrameCount, Frame);
        Transform.Position = pos;
        Transform.Rotation = rot;
    }
}
