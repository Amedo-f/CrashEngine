// Add here any uniforms you would like to be accessible from any shader type

uniform float Time;
uniform vec2 Resolution;
uniform vec3 EyePosition;
uniform vec3 EyeDirection;
uniform vec3 FogColor;
uniform float Fov; // In radians
uniform float Aspect;

uniform vec3 AmbientLightColor;
uniform vec3 DirLightColor[4];
uniform vec3 DirLightDir[4];
uniform int DirLightCount;
uniform int LitEnabled;