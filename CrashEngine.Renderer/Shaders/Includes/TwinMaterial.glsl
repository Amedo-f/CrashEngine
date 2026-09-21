const int MAX_BONES = 64;
const int MAX_BLENDS = 15;
const int MAX_TEXTURES = 5;

vec4 texturePanorama(vec3 normal, sampler2D pano)
{
    vec2 st;
    st.x = atan(normal.x, normal.z); // Azimuth
    st.y = acos(normal.y);

    if (st.x < 0.0)
    {
        st.x += 6.2831853; // 2 * PI
    }
    st /= vec2(6.2831853, 3.1415926); // Normalize to [0,1]

    return texture(pano, st);
}

#ifndef TWIN_MATERIAL
#define TWIN_MATERIAL

struct TwinMaterial {
    bool two_sided_lighting;
    float perform_fog; // 0 is off, 1 is on
    float use_texture; // 0 is off, 1 is on
    vec2 deform_speed;
    float billboard_render;
    float double_color;
    vec2 uv_scroll_speed;
    vec2 reflect_dist; // x is 1 or 0 for enabled/disabled, y is for actual distance
    float alpha_test;
    float alpha_blend; // 0 is off, 1 is on
    float metalic_specular;
    float env_map; // 0 is off, 1 is on
    int blend_func;
    int alpha_test_func; // Amedo 2026-09-21
    float select_pulse; // brightness multiplier for the editor's selection highlight; 1.0 = no effect
    // 2026-09-04 -- real bug fix: Material.BaseColor (C#) was set on every DirectCubeRenderer
    // debug marker (Triggers/Cameras/Positions/AI Positions/collision boxes/load-wall zones/
    // particle emitters) AND on a "Color" Inspector field for the plain "+ Cube" tool and a
    // MeshRenderer tint field, but was NEVER actually sent to this shader at all (its own C#
    // doc comment said so: "legacy; unused by twin shader") — every one of those features was
    // silently a no-op, always rendering the mesh's baked-in vertex colour (opaque white for
    // every shared debug-marker mesh) regardless of what colour/alpha was actually set. Confirmed
    // live: trigger/position/AI-position markers rendered solid opaque white no matter what
    // colour or alpha C# assigned. Defaults to (1,1,1,1) — a real decoded game material (which
    // never sets BaseColor) multiplies by this and is completely unaffected.
    vec4 base_color;
};

#endif