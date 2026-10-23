#include "Includes/ModelLayout.vert"

uniform TwinMaterial twin_material;

vec3 GetVertexOffset(sampler2D offsets, int shapeId, int vertexId)
{
	int shapeCoords = vertexId + ShapeStart + ShapeOffset[shapeId];
	int xCoord = shapeCoords % 256;
	int yCoord = shapeCoords / 256;
	vec4 offset = texelFetch(offsets, ivec2(xCoord, yCoord), 0);
	offset.r = offset.r * 127.0;
	offset.g = offset.g * 127.0;
	offset.b = offset.b * 127.0;
	return vec3(offset.r * BlendShape.x, offset.g * BlendShape.y, offset.b * BlendShape.z);
}

vec3 BlendVertex(vec3 position, sampler2D offsets, int vertexId, float weights[15])
{
	vec3 resultPosition = position;
	for (int i = 0; i < MAX_BLENDS; i += 1)
	{
		resultPosition += GetVertexOffset(offsets, i, vertexId) * weights[i];
	}
	return resultPosition;
}

// Amedo 2026-09-26
uint GameClothHash(float p)
{
    const float K = 0.00037299998803064227;
    precise float m = p * K;
    precise float e = fma(p, K, -m);
    uint b = floatBitsToUint(m);
    if (e != 0.0 && (e < 0.0) != (m < 0.0)) b -= 1u;
    return b & 15u;
}

// Amedo 2026-09-26
float GameClothWave(uint h, int axis)
{
    int kind = int(twin_material.cloth_kind + 0.5);
    if (kind == 2)
    {
        if (h == 0u) return twin_material.cloth_amp[axis];
        h -= 1u;
    }
    float p = twin_material.cloth_p0[h][axis] + twin_material.cloth_phase;
    return (twin_material.cloth_mode > 0.5 || (kind == 1 && axis == 2)) ? cos(p) : sin(p);
}

// Amedo 2026-09-26
vec3 GameClothDeform(vec3 p)
{
    if (twin_material.cloth_kind < 0.5) return p;
    vec3 w = vec3(GameClothWave(GameClothHash(p.x), 0), GameClothWave(GameClothHash(p.y), 1), GameClothWave(GameClothHash(p.z), 2));
    return p + w * twin_material.cloth_amp;
}

// Amedo 2026-10-01
uniform int UseInstances;
uniform int InstanceBase;
layout (std430, binding = 3) readonly buffer InstanceBlock { vec4 InstanceData[]; };

void main()
{
	mat4 model = StartModel; // Amedo 2026-10-01
	int inst = 0;
	if (UseInstances != 0)
	{
		inst = InstanceBase + gl_InstanceID * 11;
		model = mat4(InstanceData[inst], InstanceData[inst + 1], InstanceData[inst + 2], InstanceData[inst + 3]);
	}
	mat4 viewModel = StartView * model;
    vec3 processedPosition = GameClothDeform(in_Position); // Amedo 2026-09-26

    // Deform
    if (twin_material.deform_speed != vec2(0.0)) // Amedo 2026-09-30
    {
    float vy = sin(Time + in_Position.y) * twin_material.deform_speed.y * 0.1;
    float vx = sin(Time + in_Position.y) * twin_material.deform_speed.x * 0.1;
    float vz = sin(Time + in_Position.y) * twin_material.deform_speed.x * 0.1;
    processedPosition *= vec3(vx + 1.0, vy + 1.0, vz + 1.0);
    }
    
    // Billboard
	if (twin_material.billboard_render != 0.0) // Amedo 2026-09-30
	{
	mat4 invView = inverse(StartView);
	vec3 toCamera = vec3(invView[3].x - model[3].x, 0.0, invView[3].z - model[3].z); // Amedo 2026-10-01
	vec3 facing = dot(toCamera, toCamera) > 1e-6 ? normalize(toCamera) : normalize(cross(invView[0].xyz, vec3(0.0, 1.0, 0.0)));
	float handed = determinant(mat3(model)) < 0.0 ? -1.0 : 1.0;
	vec3 side = normalize(cross(vec3(0.0, 1.0, 0.0), facing)) * handed;
	viewModel = viewModel * (1.0 - twin_material.billboard_render) + StartView * mat4(vec4(side, 0.0), vec4(0.0, 1.0, 0.0, 0.0), vec4(facing, 0.0), model[3]) * twin_material.billboard_render;
	}

	// Vertex based animation
	if (BlendShapesAmount > 0) processedPosition = BlendVertex(processedPosition, Morphs, gl_VertexID, MorphWeights); // Amedo 2026-09-30
	
	// Skinning
	float boneWeight = in_BoneWeights.x + in_BoneWeights.y + in_BoneWeights.z; // Amedo 2026-09-30
	vec3 skinnedNormal = in_Normal;
	vec3 skinnedSmooth = in_SmoothNormal; // Amedo
	vec3 half2 = max(ShapeHalf * ShapeHalf, vec3(1e-4)); // Amedo
	vec3 skinnedShape = ShapeCenter.w > 0.5 ? (in_Position - ShapeCenter.xyz) / half2 : vec3(0.0);
	if (boneWeight != 0.0)
	{
	vec4 pos1 = (BoneMatrices[uint(in_BoneMatrixIndices.x)] * vec4(processedPosition, 1.0)) * in_BoneWeights.x;
	vec4 pos2 = (BoneMatrices[uint(in_BoneMatrixIndices.y)] * vec4(processedPosition, 1.0)) * in_BoneWeights.y;
	vec4 pos3 = (BoneMatrices[uint(in_BoneMatrixIndices.z)] * vec4(processedPosition, 1.0)) * in_BoneWeights.z;
	processedPosition = mix(processedPosition, pos1.xyz + pos2.xyz + pos3.xyz, in_BoneWeights.x + in_BoneWeights.y + in_BoneWeights.z);

	// Skin the normal the same way as the position (rotation part of each bone only),
	// then carry it into world space via the model matrix — without this, lighting
	// used the raw object-space normal, which only looked right for instances whose
	// placement rotation happened to leave "up" pointing up (e.g. Y-only rotations).
	vec3 n1 = mat3(BoneMatrices[uint(in_BoneMatrixIndices.x)]) * in_Normal * in_BoneWeights.x;
	vec3 n2 = mat3(BoneMatrices[uint(in_BoneMatrixIndices.y)]) * in_Normal * in_BoneWeights.y;
	vec3 n3 = mat3(BoneMatrices[uint(in_BoneMatrixIndices.z)]) * in_Normal * in_BoneWeights.z;
	skinnedNormal = mix(in_Normal, n1 + n2 + n3, in_BoneWeights.x + in_BoneWeights.y + in_BoneWeights.z);
	// Amedo
	skinnedSmooth = mat3(BoneMatrices[uint(in_BoneMatrixIndices.x)]) * in_SmoothNormal * in_BoneWeights.x
	              + mat3(BoneMatrices[uint(in_BoneMatrixIndices.y)]) * in_SmoothNormal * in_BoneWeights.y
	              + mat3(BoneMatrices[uint(in_BoneMatrixIndices.z)]) * in_SmoothNormal * in_BoneWeights.z;
	skinnedShape = mat3(BoneMatrices[uint(in_BoneMatrixIndices.x)]) * skinnedShape * in_BoneWeights.x
	             + mat3(BoneMatrices[uint(in_BoneMatrixIndices.y)]) * skinnedShape * in_BoneWeights.y
	             + mat3(BoneMatrices[uint(in_BoneMatrixIndices.z)]) * skinnedShape * in_BoneWeights.z;
	}

	Position = processedPosition;
	Projection = StartProjection;
	View = StartView;
	Model = model; // Amedo 2026-10-01
	Emit = in_Emit * twin_material.double_color;
	ViewPosition = vec3(model * vec4(processedPosition, 1.0));
	gl_Position = StartProjection * viewModel * vec4(processedPosition, 1.0);
	Texpos = in_Texpos;
	Normal = mat3(model) * skinnedNormal;
	SmoothNormal = mat3(model) * skinnedSmooth; // Amedo
	ShapeNormal = mat3(model) * skinnedShape; // Amedo
	EnvTexpos = twin_material.env_map <= 0.5 ? vec2(0.0) // Amedo 2026-09-30
		: twin_material.env_map > 2.5 // Amedo 2026-09-26
		? GameScreenUv(gl_Position, Normal, twin_material.reflect_dist.y)
		: GameEnvUv(ViewPosition, Normal, twin_material.env_map);
	Color = in_Color * twin_material.double_color * twin_material.base_color;
	Color.a = in_Color.a * twin_material.base_color.a; // Amedo 2026-09-26
	GameLit = 0.0; // Amedo
	// Amedo 2026-10-01
	if (UseInstances != 0)
	{
		vec4 ambient = InstanceData[inst + 4];
		if (ambient.w != 0.0)
		{
			GameLit = 1.0; // Amedo
			vec3 light = ambient.xyz;
			for (int i = 0; i < 3; i++)
				light += InstanceData[inst + 8 + i].xyz * max(0.0, dot(InstanceData[inst + 5 + i].xyz, Normal));
			Color.rgb = min(vec3(255.0), in_Color.rgb * 255.0 * light) / 256.0 * twin_material.base_color.rgb;
		}
	}
	// Amedo 2026-09-26
	else if (ObjLit != 0)
	{
		GameLit = 1.0; // Amedo
		vec3 light = ObjAmbient;
		for (int i = 0; i < ObjLightCount; i++) // Amedo 2026-09-30
			light += ObjLightColor[i] * max(0.0, dot(ObjLightDir[i], Normal));
		Color.rgb = min(vec3(255.0), in_Color.rgb * 255.0 * light) / 256.0 * twin_material.base_color.rgb;
	}
}