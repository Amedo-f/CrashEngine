using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;

namespace CrashEngine.Runtime;

// Amedo
public sealed partial class SceneSimulation
{
    private const float ShaderPi = 3.14159274f;
    private readonly Dictionary<PS2AnyTwinsanitySM2, List<MeshDecoder.ClothState>> _shaderLists = new();

    // Amedo
    private void ReadSceneryShaders_1CCF80(PS2AnyTwinsanitySM2? sm2)
    {
        if (sm2 is null || _shaderLists.ContainsKey(sm2)) return;
        var list = MeshDecoder.ClothShadersOf(sm2);
        foreach (var cloth in list)
        {
            if (cloth.Shader.ShaderType == TwinShader.Type.UnlitClothDeformation2) ClothSettings_1D0D20(cloth);
            else ClothSettings_1D18C8(cloth);
            cloth.Clock.Value = 0.0;
        }
        _shaderLists[sm2] = list;
        if (list.Count != 0) _log?.Invoke($"[shaders] read {list.Count} cloth shader(s) ({list.Count * 48} random draws, 0x1D18C8/0x1D0D20), rng state 0x{GameRandom.Shared.State:X8}");
    }

    // Amedo
    private void ReleaseSceneryShaders(PS2AnyTwinsanitySM2? sm2)
    {
        if (sm2 is not null) _shaderLists.Remove(sm2);
    }

    // Amedo
    private static void ClothSettings_1D18C8(MeshDecoder.ClothState c)
    {
        var rng = GameRandom.Shared;
        float scale = c.Shader.IntParam < 2 ? ShaderPi : 1f;
        for (int i = 0; i < 16; i++)
        {
            float x = rng.GetRandFloat__18C130(scale);
            float y = rng.GetRandFloat__18C130(scale);
            float z = rng.GetRandFloat__18C130(scale);
            c.Phases[i] = new System.Numerics.Vector3(x, y, z);
        }
    }

    // Amedo
    private static void ClothSettings_1D0D20(MeshDecoder.ClothState c)
    {
        var rng = GameRandom.Shared;
        bool second = 2 <= c.Shader.IntParam;
        for (int i = 0; i < 16; i++)
        {
            float x = rng.GetRandFloat__18C130(ShaderPi);
            float y = rng.GetRandFloat__18C130(ShaderPi);
            float z = rng.GetRandFloat__18C130(ShaderPi);
            c.Phases[i] = new System.Numerics.Vector3(x, y, z);
            if (!second) continue;
            float x2 = rng.GetRandFloat__18C130(ShaderPi);
            float y2 = rng.GetRandFloat__18C130(ShaderPi);
            float z2 = rng.GetRandFloat__18C130(ShaderPi);
            c.Phases100[i] = new System.Numerics.Vector3(x2, y2, z2);
        }
    }

    // Amedo
    private void StepAnimations_19FE38(int ticks)
    {
        double dt = ticks * (double)(1f / 9000f);
        foreach (var list in _shaderLists.Values)
            foreach (var cloth in list) cloth.Clock.Value += dt;
    }
}
