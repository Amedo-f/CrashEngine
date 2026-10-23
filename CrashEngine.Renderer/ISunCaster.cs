using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

// Amedo: something that can draw itself into the sun shadow depth map
public interface ISunCaster
{
    void DrawSunDepth(GL gl, in Matrix4x4 world);
}
