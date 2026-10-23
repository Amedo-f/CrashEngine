#version 450 core
// Amedo 2026-09-26

out vec2 v_UV;

const vec2 P[3] = vec2[3](vec2(-1.0, -1.0), vec2(3.0, -1.0), vec2(-1.0, 3.0));

void main()
{
    v_UV = P[gl_VertexID] * 0.5 + 0.5;
    gl_Position = vec4(P[gl_VertexID], 0.0, 1.0);
}
