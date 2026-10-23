using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Runtime.Character;
using CrashEngine.Runtime.Physics;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-10-06
public sealed class DynamicSceneryInstance
{
    public const uint InstanceFlags_1FE5E8 = 0x4418;
    public const uint Components_1FE5E8 = 0x11;
    private const float FramesPerSecond = 25f, SecondsPerFrame = 0.04f;

    private readonly DynamicSceneryAnimator _animator;
    private readonly ConvexHull[] _hulls;
    private readonly int _frames;
    private float _time;
    private Matrix4x4 _matrix = Matrix4x4.Identity, _shownFrom = Matrix4x4.Identity;
    private Matrix4x4 _motionPrev, _motionCur;
    private float _motionDt = 0.016666668f;
    private bool _motionValid;

    public Entity Entity { get; }
    public WorldChunk Chunk { get; }
    public TwinDynamicSceneryModel Model { get; }
    public CharacterBody Body { get; }
    public uint AgentFlags = InstanceFlags_1FE5E8 | 0x40000; // Amedo
    public uint ComponentMask => Components_1FE5E8;
    public Matrix4x4 GameMatrix => _matrix;
    private SceneryCell? _cell; // Amedo
    internal SceneryCell Cell => _cell ??= new SceneryCell(this);

    public DynamicSceneryInstance(Entity entity, DynamicSceneryAnimator animator, TwinDynamicSceneryModel model, WorldChunk chunk)
    {
        Entity = entity;
        Chunk = chunk;
        Model = model;
        _animator = animator;
        _frames = animator.FrameCount;
        _hulls = model.BoundingBoxBuilders.Select(ConvexHull.From).ToArray();
        Body = new CharacterBody(this) { StateFlags = uint.MaxValue };
        animator.LogicDriven = true;
        Update_1FDEB0(0f);
        _shownFrom = _matrix;
        Show(1f);
    }

    public void MotionGroup_192270(float dt, bool clockRunning)
    {
        if (!_motionValid || !clockRunning) // Amedo
        {
            _motionDt = 0.016666668f;
            _motionPrev = _motionCur = _matrix;
            _motionValid = true;
            return;
        }
        _motionPrev = _motionCur;
        _motionCur = _matrix;
        _motionDt = dt;
    }

    public Vector3 MotionVelocity_192438()
    {
        var (prev, cur, dt) = MotionFrames_192360();
        return (cur.Translation - prev.Translation) * (1f / dt);
    }

    public (Matrix4x4 Prev, Matrix4x4 Cur, float Dt) MotionFrames_192360()
    {
        if (!_motionValid) MotionGroup_192270(0f, true); // Amedo
        return (_motionPrev, _motionCur, _motionDt);
    }

    public void Update_1FDEB0(float dt)
    {
        _shownFrom = _matrix;
        if (_frames > 0)
        {
            _time += dt;
            float framePos = _time * FramesPerSecond;
            while (_frames <= framePos)
            {
                _time -= _frames * SecondsPerFrame;
                framePos = _time * FramesPerSecond;
            }
            var (pos, rot, w) = DynamicScenerySampler.SampleWithW(_animator.Animation, _frames, framePos); // Amedo
            _matrix = Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos);
            _animator.Frame = framePos;
            if (0.5f < w) AgentFlags |= 0x400; // Amedo
            else if (w < 0.5f) AgentFlags &= ~0x400u;
        }
        Body.Collidable = (AgentFlags & 0x10) != 0;
        Body.Parts.Clear();
        foreach (var hull in _hulls) Body.Parts.Add((hull, _matrix));
    }

    // Amedo
    internal void ChunkAddInstance_1F1940()
    {
        var (min, max) = Box_1EB158();
        Cell.BoxMin_40 = min;
        Cell.BoxMax_50 = max;
        AgentLabWorld.CellsSort_1EB020(Cell, AgentLabWorld.CellsOf_160(Chunk));
    }

    // Amedo
    internal void StepObjectCollision_1F0368()
    {
        var (min, max) = Box_1EB158();
        Cell.BoxMin_40 = min;
        Cell.BoxMax_50 = max;
        if (Cell.Grid_20 is { } grid) AgentLabWorld.CellsSort_1EB020(Cell, grid);
    }

    public (Vector3 Min, Vector3 Max) Box_1EB158()
    {
        if (Body.Parts.Count == 0) return (_matrix.Translation, _matrix.Translation);
        var (min, max) = Body.Parts[0].Hull.Bounds(Body.Parts[0].World);
        for (int i = 1; i < Body.Parts.Count; i++)
        {
            var (hmin, hmax) = Body.Parts[i].Hull.Bounds(Body.Parts[i].World);
            min = Vector3.Min(min, hmin);
            max = Vector3.Max(max, hmax);
        }
        return (min, max);
    }

    // Amedo
    public void Hold() => _shownFrom = _matrix;

    public void Show(float alpha)
    {
        Entity.Transform.LocalMatrix = alpha >= 1f ? _matrix : Matrix4x4.Lerp(_shownFrom, _matrix, alpha);
    }

    public void Release()
    {
        Entity.Transform.LocalMatrix = null;
        _animator.LogicDriven = false;
        _animator.Apply();
    }
}
