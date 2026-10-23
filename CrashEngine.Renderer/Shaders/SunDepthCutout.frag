#include "Includes/ModelLayout.frag"

// Amedo: sun shadow depth pass, alpha-tested materials (leaves, grass)
uniform TwinMaterial twin_material;

void main()
{
    vec2 uv = mix(Texpos, vec2(Texpos.x, 1.0 - Texpos.y), FlipY);
    float alpha = mix(1.0, texture(Texture[0], uv).a, twin_material.use_texture) * Color.a;
    if (alpha < twin_material.alpha_test) discard;
}
