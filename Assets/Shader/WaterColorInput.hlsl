#ifndef WATERCOLOR_INPUT_INCLUDED
#define WATERCOLOR_INPUT_INCLUDED

// Shared by Custom/WaterColor and Custom/WaterColor Moveable.
// Define WATERCOLOR_MOVEABLE before including to use the material seed instead of the world position.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

// Both shaders share this layout so the moveable shader can reuse the depth and shadow passes.
CBUFFER_START(UnityPerMaterial)
    float4 _Noise_ST;
    half4 _Color;
    half4 _EmissionColor;
    half _NoiseStrength;
    half _NoiseContrast;
    half _EmissionNoise;
    float _PatchSize;
    float _RandomRotation;
    float _BlendSharpness;
    float _ObjectRandom;
    float _Seed;
    float _TriplanarSharpness;
CBUFFER_END

float2 Hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float2 NoiseOffset()
{
#if defined(WATERCOLOR_MOVEABLE)
    // The pattern has to stay glued to the mesh while it moves, so the offset comes from the material.
    return Hash22(float2(_Seed, _Seed * 1.618 + 0.5));
#else
    // Shifted by a random amount per object so neighbours don't line up.
    float3 objectPos = UNITY_MATRIX_M._m03_m13_m23;
    return Hash22(objectPos.xz + objectPos.y * 17.0) * _ObjectRandom;
#endif
}

float2 NoiseUV(float2 uv)
{
    return TRANSFORM_TEX(uv, _Noise) + NoiseOffset();
}

// Splits the plane into triangles and returns the three corners with their barycentric weights.
void TriangleGrid(float2 uv, out float3 weights, out float2 v1, out float2 v2, out float2 v3)
{
    const float2x2 gridToSkewed = float2x2(1.0, -0.57735027, 0.0, 1.15470054);
    float2 skewed = mul(gridToSkewed, uv);
    float2 baseId = floor(skewed);
    float3 temp = float3(frac(skewed), 0.0);
    temp.z = 1.0 - temp.x - temp.y;

    float s = step(0.0, -temp.z);
    float s2 = 2.0 * s - 1.0;
    weights = float3(-temp.z * s2, s - temp.y * s2, s - temp.x * s2);
    v1 = baseId + float2(s, s);
    v2 = baseId + float2(s, 1.0 - s);
    v3 = baseId + float2(1.0 - s, s);
}

half SamplePatch(float2 uv, float2 dx, float2 dy, float2 id)
{
    float2 rnd = Hash22(id);
    float angle = (Hash22(id + 71.3).x - 0.5) * TWO_PI * _RandomRotation;
    float sn, cs;
    sincos(angle, sn, cs);
    float2x2 rot = float2x2(cs, -sn, sn, cs);
    return SAMPLE_TEXTURE2D_GRAD(_Noise, sampler_Noise, mul(rot, uv) + rnd, mul(rot, dx), mul(rot, dy)).r;
}

// Every patch of the surface samples the noise with its own random offset and rotation,
// blended across patch borders, so the texture never visibly repeats.
half SampleNoise(float2 uv)
{
#if defined(_RANDOM_TILING_OFF)
    half n = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, uv).r;
#else
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);

    float3 w;
    float2 v1, v2, v3;
    TriangleGrid(uv / _PatchSize, w, v1, v2, v3);

    w = pow(saturate(w), _BlendSharpness);
    w /= w.x + w.y + w.z;

    half n = SamplePatch(uv, dx, dy, v1) * w.x
           + SamplePatch(uv, dx, dy, v2) * w.y
           + SamplePatch(uv, dx, dy, v3) * w.z;
#endif
    return saturate((n - 0.5) * _NoiseContrast + 0.5);
}

// Projects the noise along the three axes and blends by the surface normal, so mesh UVs are not needed.
half SampleNoiseTriplanar(float3 positionWS, float3 normalWS)
{
#if defined(WATERCOLOR_MOVEABLE)
    // Object space, corrected for scale: the pattern moves with the mesh but keeps its world size.
    float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
    float3 pos = TransformWorldToObject(positionWS) * scale;
    float3 normal = normalize(mul(normalWS, (float3x3)UNITY_MATRIX_M) / scale);
#else
    float3 pos = positionWS;
    float3 normal = normalWS;
#endif

    float3 w = pow(abs(normal), _TriplanarSharpness);
    w /= w.x + w.y + w.z;

    // Each axis gets its own shift so the three projections don't mirror each other.
    float2 offset = _Noise_ST.zw + NoiseOffset();
    return SampleNoise(pos.zy * _Noise_ST.xy + offset) * w.x
         + SampleNoise(pos.xz * _Noise_ST.xy + offset + 0.37) * w.y
         + SampleNoise(pos.xy * _Noise_ST.xy + offset + 0.71) * w.z;
}

#endif
