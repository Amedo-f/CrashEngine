#include "Includes/ModelLayout.frag"
#include "lygia/lighting/shadingData/shadingData.glsl"
#include "lygia/lighting/specular/gaussian.glsl"
#include "lygia/lighting/sphereMap.glsl"

const float GAME_FOG_Z_SCALE = 1677833.125; // Amedo 2026-09-26
const float GAME_FOG_Z_BIAS = -1117.0; // Amedo 2026-09-26

uniform TwinMaterial twin_material;

// Amedo
uniform int SunShadows;
uniform int SunCascades;
uniform mat4 SunViewProj[5];
uniform vec4 SunTexels;
uniform int SunFocus;
uniform float SunFocusTexel;
uniform vec3 SunDir;
uniform float SunStrength;
uniform float SunPenumbra;
uniform float ShapeSoft; // Amedo: 0..1 how much objects shade like their bounding shape instead of their faces
uniform int SunDebug;
layout (binding = 8) uniform sampler2DArrayShadow SunMap;
const float POINT_SHADOW = 0.55; // Amedo: point light shadows only take away part of the light (soft look)
const float SKIN_WRAP = 0.5; // Amedo: objects/characters: light wraps a little past the side facing it (soft, skin-like falloff)
const float SKIN_PENUMBRA = 0.025; // Amedo: objects/characters: sun shadow edges at least this soft (world units)
const vec3 SKIN_WARM = vec3(1.10, 0.97, 0.90); // Amedo: the light/shade border on objects gets a touch warmer (light under the skin)
uniform int PointCount;
uniform vec4 PointPosRange[4];
uniform vec4 PointColor[4];
layout (binding = 9) uniform samplerCubeArrayShadow PointMap;

// Amedo: the level's point lights, the game's falloff (f = 25 / (d^2 + 25), Radius * f^exp * dot(N, d * f)),
// each one shadowed by its cube map; returns the light that reaches p, `blocked` = the part its shadow takes away
vec3 PointLighting(vec3 p, vec3 n, float wrap, out vec3 blocked)
{
    vec3 lit = vec3(0.0);
    blocked = vec3(0.0);
    for (int i = 0; i < PointCount; i++)
    {
        vec4 pr = PointPosRange[i];
        if (pr.w <= 0.0) continue;
        vec3 d = pr.xyz - p;
        float d2 = dot(d, d);
        if (d2 >= pr.w * pr.w) continue;
        float f = 25.0 / (d2 + 25.0);
        float dist = sqrt(d2);
        float lambert = (dot(n, d) / max(dist, 1e-4) + wrap) / (1.0 + wrap) * dist * f;
        if (lambert <= 0.0) continue;
        vec3 c = PointColor[i].rgb * pow(f, PointColor[i].w) * lambert;
        vec3 q = p + n * (0.02 + dist * 0.01) - pr.xyz;
        float ref = length(q) / pr.w - 0.002;
        float shadowed = texture(PointMap, vec4(q, float(i)), ref);
        if (wrap > 0.0)
        {
            // objects: a few taps around the point, a softer shadow edge
            vec3 side = normalize(cross(q, abs(q.y) < 0.9 * length(q) ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0)));
            vec3 up = normalize(cross(side, q));
            float s = 0.03 + dist * 0.006;
            shadowed = 0.25 * (shadowed + texture(PointMap, vec4(q + side * s, float(i)), ref)
                                        + texture(PointMap, vec4(q - side * s * 0.5 + up * s * 0.87, float(i)), ref)
                                        + texture(PointMap, vec4(q - side * s * 0.5 - up * s * 0.87, float(i)), ref));
        }
        float visible = 1.0 - POINT_SHADOW * (1.0 - shadowed);
        lit += c * visible;
        blocked += c * (1.0 - visible);
    }
    return lit;
}

// Amedo: set per fragment in main (objects get softer edges and skip the sharp layer around the player)
float SunPenumbraNow;
bool SunUseFocus;

// Amedo
float SunSample(int cascade, float texel, vec3 worldPos, vec3 n, out float edge)
{
    float radius = clamp(SunPenumbraNow / texel, 0.5, 3.0);
    vec4 clip = SunViewProj[cascade] * vec4(worldPos + n * texel * (1.0 + radius), 1.0);
    vec3 ndc = clip.xyz / clip.w;
    edge = max(abs(ndc.x), abs(ndc.y));
    if (edge >= 1.0) return -1.0;
    vec3 uvz = ndc * 0.5 + 0.5;
    if (uvz.z >= 1.0) return 1.0;
    vec2 t = vec2(1.0) / vec2(textureSize(SunMap, 0).xy);
    float layer = float(cascade);
    if (radius <= 0.5)
    {
        float lit = 0.25 * (texture(SunMap, vec4(uvz.xy + vec2(-0.5, -0.5) * t, layer, uvz.z))
                     + texture(SunMap, vec4(uvz.xy + vec2( 0.5, -0.5) * t, layer, uvz.z))
                     + texture(SunMap, vec4(uvz.xy + vec2(-0.5,  0.5) * t, layer, uvz.z))
                     + texture(SunMap, vec4(uvz.xy + vec2( 0.5,  0.5) * t, layer, uvz.z)));
        return SunPenumbraNow <= 0.0 ? smoothstep(0.3, 0.7, lit) : lit;
    }
    vec2 stride = t * (radius / 1.5);
    float sum = 0.0;
    for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
            sum += texture(SunMap, vec4(uvz.xy + (vec2(x, y) - 1.5) * stride, layer, uvz.z));
    return sum * (1.0 / 16.0);
}

// Amedo
float SunVisibility(vec3 worldPos, vec3 n, out int cascade)
{
    cascade = SunCascades;
    float focusLit = -1.0, focusBlend = 1.0;
    if (SunFocus != 0 && SunUseFocus)
    {
        float focusEdge;
        focusLit = SunSample(SunCascades, SunFocusTexel, worldPos, n, focusEdge);
        if (focusLit >= 0.0)
        {
            focusBlend = smoothstep(0.7, 1.0, focusEdge);
            cascade = -1;
            if (focusBlend <= 0.0) return focusLit;
        }
    }
    float lit = 1.0;
    for (int i = 0; i < SunCascades; i++)
    {
        float edge;
        float value = SunSample(i, SunTexels[i], worldPos, n, edge);
        if (value < 0.0) continue;
        float blend = smoothstep(0.85, 1.0, edge);
        if (blend > 0.0)
        {
            float nextEdge;
            float next = i + 1 < SunCascades ? SunSample(i + 1, SunTexels[i + 1], worldPos, n, nextEdge) : 1.0;
            value = mix(value, next < 0.0 ? 1.0 : next, blend);
        }
        if (cascade != -1) cascade = i;
        lit = value;
        break;
    }
    return focusLit >= 0.0 ? mix(focusLit, lit, focusBlend) : lit;
}

// Amedo 2026-09-26
vec2 ScrollOffset()
{
    const float TAU = 6.2831853;
    vec2 a = twin_material.uv_scroll_phase + twin_material.uv_scroll_speed * Time;
    int mx = int(twin_material.uv_scroll_mode.x + 0.5);
    int my = int(twin_material.uv_scroll_mode.y + 0.5);
    vec2 o = vec2(0.0);
    if (mx == 3) o.x = sin(TAU * a.x);
    else if (mx == 4) o.y = cos(TAU * a.x);
    else o.x = fract(a.x);
    if (my == 3) o.y = sin(TAU * a.y);
    else if (my == 4) o.y = cos(TAU * a.y);
    else if (my == 2 || twin_material.uv_scroll_speed.y != 0.0) o.y = fract(a.y);
    return o;
}

// Amedo
vec4 GameTexture(vec2 uv)
{
    int mode = int(twin_material.gs_lod.x + 0.5);
    if (mode == 0) return texture(Texture[0], uv, MipBias);
    float lod = mode == 1 ? twin_material.gs_lod.y + log2(1.0 / gl_FragCoord.w) * twin_material.gs_lod.z + MipBias : 0.0;
    return textureLod(Texture[0], uv, max(lod, 0.0));
}

void main()
{
    // Amedo
    vec3 faceNormal = normalize(cross(dFdx(ViewPosition), dFdy(ViewPosition)));
    if (dot(faceNormal, EyePosition - ViewPosition) < 0.0) faceNormal = -faceNormal;
    vec2 screenUvs = gl_FragCoord.xy / Resolution;
    vec2 uvs = (twin_material.env_map > 0.5 ? EnvTexpos : Texpos) + ScrollOffset(); // Amedo 2026-09-26
    uvs.y = mix(uvs.y, 1.0 - uvs.y, FlipY);
    vec4 textureColor = mix(vec4(1.0), GameTexture(uvs), twin_material.use_texture); // Amedo
    vec3 resultColor = textureColor.rgb * Color.rgb;
    float resultAlpha = textureColor.a * Color.a;
    vec4 screenColor = texture(Screen, screenUvs);
    vec2 reflectUv = vec2(screenUvs.x + twin_material.reflect_dist.y, screenUvs.y + twin_material.reflect_dist.y);
    vec4 screenColorReflected = texture(Screen, reflectUv);
    resultColor = mix(resultColor, screenColorReflected.rgb * Color.rgb * resultColor, twin_material.reflect_dist.x);

    vec3 surfaceNormal = normalize(Normal);
    if (!gl_FrontFacing && twin_material.two_sided_lighting)
    {
        surfaceNormal = -surfaceNormal;
    }
    vec3 eyeDirection = normalize(EyePosition - ViewPosition);

    if (resultAlpha < twin_material.alpha_test)
    {
        discard;
        return;
    }

    // 2026-09-06 -- real bug, confirmed against a real UnlitGlossy scenery tile (Ice Hub,
    // labext.rm2) that was showing wrong near-black patches in CrashEngine's own preview: the
    // old line below MULTIPLIED the whole surface by (specular + diffuse), where
    // diffuse = dot(surfaceNormal, eyeDirection) -- a view-angle term that goes to ~0 at any
    // near-grazing angle (exactly what a curved ice cliff face constantly presents to the
    // camera). Multiplying the real base texture by a near-zero factor there turned it solid
    // black -- not a lighting effect, a straight math bug. A highlight should ADD shine on top
    // of the real surface, never multiply it toward black. Keeping the Gaussian specular term
    // itself (no real per-level data contradicts "glossy = has *a* highlight"), dropping the
    // eyeDir "diffuse" term entirely (it had no clear real basis and was the actual cause), and
    // switching to additive.
    // 0.3 intensity scalar -- tames the additive highlight (user-reported "too bright/blown
    // out" after the additive fix above); a plain tunable brightness knob, not derived from any
    // real per-material data (none exists for this).
    float specular = specularGaussian(max(dot(surfaceNormal, eyeDirection), 0.0), 20.0) * twin_material.metalic_specular * 0.3;
    vec4 resultBlend = vec4(resultColor, mix(1.0, resultAlpha, twin_material.alpha_blend));
    resultBlend.rgb += Color.rgb * specular;
    resultBlend.rgb *= Diffuse.rgb;
    resultBlend.a = mix(resultBlend.a, resultBlend.a * Diffuse.a, twin_material.alpha_blend);

    // Amedo
    if (SunShadows != 0)
    {
        SunPenumbraNow = GameLit > 0.5 ? max(SunPenumbra, SKIN_PENUMBRA) : SunPenumbra; // Amedo
        SunUseFocus = GameLit < 0.5;
        int cascade;
        vec3 sunNormal = faceNormal;
        float terminator = 1.0;
        if (dot(SmoothNormal, SmoothNormal) > 1e-8 && GameLit > 0.5 && SunDebug != 5)
        {
            vec3 smoothNormal = normalize(SmoothNormal);
            if (dot(smoothNormal, faceNormal) < 0.0) smoothNormal = -smoothNormal;
            vec3 shapedNormal = smoothNormal;
            if (ShapeSoft > 0.0 && dot(ShapeNormal, ShapeNormal) > 1e-12) shapedNormal = normalize(mix(smoothNormal, normalize(ShapeNormal), ShapeSoft));
            terminator = smoothstep(-0.45, 0.65, dot(shapedNormal, SunDir));
            // the map is read along the computed smooth surface (follows the real triangles), nudged towards the sun
            vec3 readNormal = faceNormal;
            if (dot(SmoothNormal, SmoothNormal) > 1e-8)
            {
                readNormal = normalize(SmoothNormal);
                if (dot(readNormal, faceNormal) < 0.0) readNormal = -readNormal;
            }
            float facing = dot(readNormal, SunDir);
            sunNormal = readNormal * (1.0 + 2.0 * (1.0 - clamp(facing, 0.0, 1.0))) + SunDir * (2.0 * ShapeSoft);
        }
        float shade = 1.0 - SunVisibility(ViewPosition, sunNormal, cascade) * terminator;
        resultBlend.rgb *= 1.0 - SunStrength * shade;
        if (GameLit > 0.5) resultBlend.rgb *= mix(vec3(1.0), SKIN_WARM, 4.0 * terminator * (1.0 - terminator) * SunStrength); // Amedo
        // Amedo
        vec3 pointLit = vec3(0.0), pointBlocked = vec3(0.0);
        if (PointCount > 0)
        {
            vec3 lightNormal = faceNormal;
            if (dot(SmoothNormal, SmoothNormal) > 1e-8)
            {
                lightNormal = normalize(SmoothNormal);
                if (dot(lightNormal, faceNormal) < 0.0) lightNormal = -lightNormal;
            }
            if (GameLit > 0.5 && ShapeSoft > 0.0 && dot(ShapeNormal, ShapeNormal) > 1e-12) lightNormal = normalize(mix(lightNormal, normalize(ShapeNormal), ShapeSoft));
            pointLit = PointLighting(ViewPosition, lightNormal, GameLit > 0.5 ? SKIN_WRAP : 0.0, pointBlocked);
            resultBlend.rgb += textureColor.rgb * pointLit;
        }
        if (SunDebug == 7) resultBlend.rgb = pointLit + vec3(pointBlocked.r + pointBlocked.g + pointBlocked.b, 0.0, 0.0);
        else if (SunDebug == 1) resultBlend.rgb = vec3(1.0 - shade);
        else if (SunDebug == 2) resultBlend.rgb = faceNormal * 0.5 + 0.5;
        else if (SunDebug == 6) resultBlend.rgb = dot(SmoothNormal, SmoothNormal) > 1e-8 ? sunNormal / length(sunNormal) * 0.5 + 0.5 : vec3(0.0);
        else if (SunDebug == 3) resultBlend.rgb = (cascade == -1 ? vec3(1.0, 0.3, 1.0) : cascade == 0 ? vec3(1.0, 0.3, 0.3) : cascade == 1 ? vec3(0.3, 1.0, 0.3) : cascade == 2 ? vec3(0.3, 0.3, 1.0) : cascade == 3 ? vec3(1.0, 1.0, 0.3) : vec3(1.0)) * (1.0 - 0.6 * shade);
    }

    // Fog
    float viewDepth = -(View * vec4(ViewPosition, 1.0)).z; // Amedo 2026-09-26
    float zbuf = GAME_FOG_Z_SCALE / max(viewDepth, 0.0001) + GAME_FOG_Z_BIAS; // Amedo 2026-09-26
    vec4 fogEntry = texelFetch(FogPalette, ivec2(clamp(int(zbuf) >> 8, 0, 255), 0), 0); // Amedo 2026-09-26
    float fogPower = zbuf < 65536.0 ? twin_material.perform_fog * FogAmount * (1.0 - fogEntry.a * 255.0 / 128.0) : 0.0; // Amedo 2026-09-26
    if (twin_material.alpha_blend > 0.5 && twin_material.blend_func != 0) resultBlend.rgb *= 1.0 - fogPower; // Amedo 2026-09-26
    else resultBlend.rgb = mix(resultBlend.rgb, fogEntry.rgb, fogPower); // Amedo 2026-09-26

    // Editor selection highlight — pure brightness scale (same hue, no colour swap), the
    // CPU side pulses this smoothly over time only for currently-selected objects.
    resultBlend.rgb *= twin_material.select_pulse;

    outColor = mix(resultBlend, Diffuse, DiffuseOnly);
}
