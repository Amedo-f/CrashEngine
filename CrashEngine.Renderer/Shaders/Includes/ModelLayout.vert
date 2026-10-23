// Add any changes or additions you want for the model's format

#include "TwinMaterial.glsl"

layout (location = 0) in vec3 in_Position;
layout (location = 1) in vec4 in_Color;
layout (location = 2) in vec2 in_Texpos;
layout (location = 3) in vec3 in_Normal;
layout (location = 4) in vec4 in_Emit;
layout (location = 5) in vec3 in_BoneMatrixIndices;
layout (location = 6) in vec3 in_BoneWeights;
layout (location = 7) in mat4 in_InstanceModelMatrix;
layout (location = 11) in vec3 in_SmoothNormal; // Amedo

out vec3 Position;
out vec2 Texpos;
out vec4 Color;
out vec4 Emit;
out vec3 Normal;
out vec3 SmoothNormal; // Amedo
out float GameLit; // Amedo: 1 = lit by the game's own object lights (vertex)
out vec3 ShapeNormal; // Amedo: direction out of the part's bounding ellipsoid (0 = none)
out vec3 ViewPosition;
out vec2 EnvTexpos; // Amedo 2026-09-26
out mat4 Projection;
out mat4 View;
out mat4 Model;

#include "GlobalUniformsDeclaration.glsl"
// Vertex shader specific uniforms
uniform mat4 StartProjection;
uniform mat4 StartView;
uniform mat4 StartModel;
uniform mat4 BoneMatrices[MAX_BONES];
uniform vec3 BlendShape;
uniform int BlendShapesAmount;
uniform vec4 ShapeCenter; // Amedo: xyz = the mesh's bounds centre (bind pose), w = 1 when set
uniform vec3 ShapeHalf; // Amedo: the bounds' half size
uniform int ShapeOffset[MAX_BLENDS];
uniform int ShapeStart;
uniform float MorphWeights[MAX_BLENDS];
layout (binding = 6) uniform sampler2D Morphs;

// Amedo 2026-09-26
float GameFastInvSqrt(float d2)
{
    return intBitsToFloat(int(1598029824.0 - 0.5 * float(floatBitsToInt(d2))));
}

// Amedo 2026-09-26
vec2 GameEnvUv(vec3 worldPos, vec3 worldNormal, float mode)
{
    const vec3 GAME_PROJ = vec3(1.415, 2.4149, 1.0001);
    vec3 v = EyePosition - worldPos;
    v *= GameFastInvSqrt(dot(v, v));
    if (mode > 1.5)
    {
        vec3 r = mat3(StartView) * (2.0 * dot(v, worldNormal) * worldNormal - v) * GAME_PROJ;
        return clamp(vec2(0.5 + 0.5 * r.x, 0.5 + 0.5 * r.y), 0.0, 1.0);
    }
    vec3 h = mat3(StartView) * ((v + worldNormal) * 0.5) * GAME_PROJ;
    h *= GameFastInvSqrt(dot(h, h));
    return clamp(vec2(0.5 + 0.5 * h.x, 0.5 - 0.5 * h.y), 0.0, 1.0);
}

// Amedo 2026-09-26
vec2 GameScreenUv(vec4 clip, vec3 worldNormal, float dist)
{
    const vec3 GAME_PROJ = vec3(1.415, 2.4149, 1.0001);
    vec2 ndc = clip.xy / clip.w;
    vec3 n = mat3(StartView) * worldNormal * GAME_PROJ;
    return clamp(vec2(0.5 + 0.5 * ndc.x, 0.5 - 0.5 * ndc.y) + n.xy * dist, 0.0, 1.0);
}