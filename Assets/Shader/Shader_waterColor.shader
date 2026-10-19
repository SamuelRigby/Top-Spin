Shader "Custom/WaterColor Moveable"
{
    Properties
    {
        [MainColor] _Color ("Color", Color) = (1, 1, 1, 1)

        [Header(Noise)]
        [MainTexture] _Noise ("Noise", 2D) = "white" {}
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 1
        _NoiseContrast ("Noise Contrast", Range(0, 4)) = 1

        [Header(Random Tiling)]
        [ToggleOff(_RANDOM_TILING_OFF)] _RandomTiling ("Random Tiling", Float) = 1
        _PatchSize ("Patch Size", Range(0.05, 2)) = 0.35
        _RandomRotation ("Random Rotation", Range(0, 1)) = 0.15
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 6
        _Seed ("Random Seed", Float) = 0

        [Header(Triplanar)]
        [Toggle(_TRIPLANAR)] _Triplanar ("Triplanar (ignore UVs)", Float) = 0
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 32)) = 8

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionNoise ("Emission Noise", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local_fragment _RANDOM_TILING_OFF
            #pragma shader_feature_local_fragment _TRIPLANAR

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #define WATERCOLOR_MOVEABLE
            #include "WaterColorInput.hlsl"
            #include "WaterColorForwardPass.hlsl"
            ENDHLSL
        }

        UsePass "Custom/WaterColor/SHADOWCASTER"
        UsePass "Custom/WaterColor/DEPTHONLY"
        UsePass "Custom/WaterColor/DEPTHNORMALS"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
