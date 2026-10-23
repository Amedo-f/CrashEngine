#include "Includes/ModelLayout.frag"

// Amedo: point light shadow cube faces: linear distance to the light (0..1 over its range)
uniform TwinMaterial twin_material;
uniform vec3 PointLightPos;
uniform float PointLightFar;
uniform int PointCutout;

void main()
{
    if (PointCutout != 0)
    {
        vec2 uv = mix(Texpos, vec2(Texpos.x, 1.0 - Texpos.y), FlipY);
        float alpha = mix(1.0, texture(Texture[0], uv).a, twin_material.use_texture) * Color.a;
        if (alpha < twin_material.alpha_test) discard;
    }
    gl_FragDepth = clamp(length(ViewPosition - PointLightPos) / PointLightFar, 0.0, 1.0);
}
