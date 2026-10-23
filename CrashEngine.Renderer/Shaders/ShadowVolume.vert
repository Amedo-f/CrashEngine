#version 450 core
// Amedo 2026-09-26

layout(location = 0) in vec3 in_Position;

uniform mat4 MVP;

void main()
{
    gl_Position = MVP * vec4(in_Position, 1.0);
}
