#version 450 core
// Amedo 2026-09-26

in vec2 v_UV;
in vec4 v_Color;

uniform sampler2D Tex;
uniform int Opaque;
uniform float AlphaRef; // Amedo

out vec4 outColor;

void main()
{
    vec4 t = texture(Tex, v_UV);
    vec3 col = t.rgb * v_Color.rgb;
    float a = t.a * v_Color.a;
    if (a * 128.0 < AlphaRef) discard;
    if (Opaque != 0)
        outColor = vec4(col, 1.0);
    else
        outColor = vec4(col * a, min(a, 1.0));
}
