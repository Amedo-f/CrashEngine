#version 450 core
// Amedo 2026-09-26

layout(location = 0) in vec3 in_Position;
layout(location = 1) in vec2 in_UV;
layout(location = 2) in vec4 in_Color;

uniform mat4 ViewProjection;

out vec2 v_UV;
out vec4 v_Color;

void main()
{
    v_UV = in_UV;
    v_Color = in_Color;
    gl_Position = ViewProjection * vec4(in_Position, 1.0);
}
