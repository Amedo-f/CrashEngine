#version 450 core
// Amedo 2026-09-26

in vec2 v_UV;

uniform sampler2D Mask;
uniform int UseMask;
uniform float Darkness;

out vec4 out_Color;

void main()
{
    float m = UseMask != 0 ? texture(Mask, v_UV).r : 1.0;
    out_Color = vec4(vec3(Darkness * m), 0.0);
}
