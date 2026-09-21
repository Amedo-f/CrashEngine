#include "Includes/ModelLayout.frag"
#include "lygia/lighting/shadingData/shadingData.glsl"
#include "lygia/lighting/specular/gaussian.glsl"
#include "lygia/lighting/sphereMap.glsl"

const float FOG_CAMERA_MAX_DIST = 300.0;

uniform TwinMaterial twin_material;

void main()
{
    vec2 screenUvs = gl_FragCoord.xy / Resolution;
    vec2 uvs = twin_material.uv_scroll_speed * vec2(Time) + Texpos;
    uvs.y = mix(uvs.y, 1.0 - uvs.y, FlipY);
    vec4 textureColor = mix(vec4(1.0), texture(Texture[0], uvs), twin_material.use_texture);
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

    // Real per-level world lighting (ambient + up to 4 directional lights, decoded from this
    // level's own SM2 scenery data - see project_crashengine_worldlighting memory). Applied
    // BEFORE env-map mixing below so mirror/chrome surfaces stay unaffected by scene light.
    // Gated behind LitEnabled (off by default, matching TT Lab's own real, verified-accurate
    // unlit look) so it's a one-uniform global toggle, not a silent always-on change.
    if (LitEnabled != 0)
    {
        vec3 worldLighting = AmbientLightColor;
        for (int i = 0; i < DirLightCount; i++)
        {
            worldLighting += DirLightColor[i] * max(0.0, dot(surfaceNormal, -DirLightDir[i]));
        }
        resultColor *= worldLighting;
    }

    // We have 2 ways either sphere mapping or doing the function BetaM wrote. I personally like doing sphere mapping :^)
    vec4 panoramaTexture = texture(Texture[0], sphereMap(surfaceNormal, EyePosition)); //texturePanorama(normalize(eyeDirection * vec3(-1, -1, 1)), Texture[0]);
    vec3 envMapColor = panoramaTexture.rgb * Color.rgb;
    float envMapAlpha = mix(1.0, panoramaTexture.a * Color.a, twin_material.alpha_blend);
    resultColor = mix(resultColor, envMapColor, twin_material.env_map);
    resultAlpha = mix(resultAlpha, envMapAlpha, twin_material.env_map);

    // Amedo 2026-09-21 -- honor the PS2 GS alpha-test compare method, not a fixed "< ref" discard
    {
        int   f = twin_material.alpha_test_func;
        float a = resultAlpha;
        float r = twin_material.alpha_test;
        bool passed;
        if      (f == 1) passed = true;              // ALWAYS
        else if (f == 0) passed = false;             // NEVER
        else if (f == 2) passed = (a <  r);          // LESS
        else if (f == 3) passed = (a <= r);          // LEQUAL
        else if (f == 4) passed = (abs(a - r) < 0.004);   // EQUAL
        else if (f == 6) passed = (a >  r);          // GREATER
        else if (f == 7) passed = (abs(a - r) >= 0.004);  // NOTEQUAL
        else             passed = (a >= r);          // GEQUAL (default)
        if (!passed)
        {
            discard;
            return;
        }
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

    // Fog
    float cameraDistance = distance(EyePosition, ViewPosition);
    float fogPower = 0.4 * (1.0 - exp(-(cameraDistance / FOG_CAMERA_MAX_DIST)));
    resultBlend.rgb = mix(resultBlend.rgb, FogColor, fogPower);

    // Editor selection highlight — pure brightness scale (same hue, no colour swap), the
    // CPU side pulses this smoothly over time only for currently-selected objects.
    resultBlend.rgb *= twin_material.select_pulse;

    outColor = mix(resultBlend, Diffuse, DiffuseOnly);
}
