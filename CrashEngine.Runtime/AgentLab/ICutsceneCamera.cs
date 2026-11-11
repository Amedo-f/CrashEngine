namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-29
public interface ICutsceneCamera
{
    void ToggleCutsceneCamera_111520(IReadOnlyList<uint> args);
    void CutsceneTargets_1118C8(AgentLabAgent by, IReadOnlyList<uint> args);
    void CutsceneShot_110360(List<uint> args);
    void CameraNode_1201B8(int op, IReadOnlyList<uint> args); // Amedo
    void CameraFocusObject_10F2D8(AgentLabAgent? focus); // Amedo
    void SetCamera_10F1D0(uint flags, int angle, float value); // Amedo
    void CameraNodeSetTarget_120110(AgentLabAgent target); // Amedo
    void Prepare_27DEC0(); // Amedo
    void PlayerCameraReset_15E538(); // Amedo
    void SwitchPlayerCamera_177A90(); // Amedo 2026-11-11
    System.Numerics.Matrix4x4 Pose { get; } // Amedo
}
