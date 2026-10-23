namespace CrashEngine.Runtime.Audio;

// Amedo 2026-10-06
public sealed class SoundGroups
{
    private static readonly float[] DuckLevels_2FB228 = [0.25f, 0.25f, 0.25f, 1f];

    private readonly SoundMixer _mixer;
    private readonly int[] _baseL = [0x1000, 0x1000, 0x1000, 0x1000], _baseR = [0x1000, 0x1000, 0x1000, 0x1000];
    private readonly float[] _scale = [1f, 1f, 1f, 1f];
    private int _duckState;
    private float _duckRamp;
    private float _fadeTime_309AC0, _fade_309AC4; // Amedo

    public SoundGroups(SoundMixer mixer) => _mixer = mixer;

    public int DuckState => _duckState;
    public float Scale(int index) => _scale[index];
    public float MasterFade => _fadeTime_309AC0 > 0f ? _fade_309AC4 : 1f; // Amedo

    public static int GroupOf_1E5DF0(int category) => category switch { 1 => 2, 2 => 3, 3 => 4, _ => 1 };

    public void DuckStart_1E58D8()
    {
        _duckState = 1;
        _duckRamp = 1f;
    }

    public void DuckEnd_1E58F0()
    {
        _duckState = 3;
        _duckRamp = 1f;
    }

    public void Update_1DEEE0(float dt)
    {
        if (_duckState is 0 or 2) return;
        if (0f < _duckRamp)
        {
            float ramp = _duckRamp - dt / _duckRamp;
            _duckRamp = ramp < 0f ? 0f : ramp;
        }
        float f = _duckState == 3 ? 1f - _duckRamp : _duckRamp;
        for (int g = 0; g < 4; g++)
        {
            float duck = DuckLevels_2FB228[g];
            _scale[g] = duck + (1f - duck) * f;
            SetLevel_1E5FE8(g, Level_1E60A8(g));
        }
        if (_duckRamp == 0f) _duckState = _duckState == 1 ? 2 : 0;
    }

    // Amedo
    public void FadeOut_1E6118(float duration)
    {
        _fadeTime_309AC0 = duration;
        _fade_309AC4 = 1f;
    }

    // Amedo
    public void UpdateFade_1DFFD8(float dt)
    {
        if (!(0f < _fadeTime_309AC0)) return;
        float fade = _fade_309AC4 - dt / _fadeTime_309AC0;
        _fade_309AC4 = fade < 0f ? 0f : fade;
        for (int g = 0; g < 4; g++)
            _mixer.SetGroup_1E7CB8(g + 1, (int)(_baseL[g] * _fade_309AC4), (int)(_baseR[g] * _fade_309AC4));
    }

    // Amedo
    public void StopFade_1DFDD0()
    {
        _fadeTime_309AC0 = 0f;
        for (int g = 0; g < 4; g++) _mixer.SetGroup_1E7CB8(g + 1, _baseL[g], _baseR[g]);
        for (int g = 0; g < 4; g++)
        {
            _scale[g] = 1f;
            SetLevel_1E5FE8(g, Level_1E60A8(g));
        }
    }

    public float Level_1E60A8(int g) => _baseL[g] * (1f / 8192f) + _baseR[g] * (1f / 8192f);

    public void SetLevel_1E5FE8(int g, float level)
    {
        float l = level * 4096f, r = level * 4096f;
        _baseR[g] = (int)r;
        _baseL[g] = (int)l;
        _mixer.SetGroup_1E7CB8(g + 1, (int)(l * _scale[g]), (int)(r * _scale[g]));
    }

    public void Reset_1E5F18()
    {
        for (int g = 0; g < 4; g++)
        {
            _scale[g] = 1f;
            _mixer.SetGroup_1E7CB8(g + 1, _baseL[g], _baseR[g]);
        }
    }
}
