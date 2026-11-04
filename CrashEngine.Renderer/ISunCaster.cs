using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

// Amedo
public interface ISunCaster
{
    void DrawSunDepth(GL gl, in Matrix4x4 world);
}
