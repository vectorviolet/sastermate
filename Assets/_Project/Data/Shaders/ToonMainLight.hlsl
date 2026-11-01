#ifndef CLIMATE_GUARDIANS_TOON_MAIN_LIGHT_INCLUDED
#define CLIMATE_GUARDIANS_TOON_MAIN_LIGHT_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

// Legacy direction-only overload for graphs which do not need shadow inputs.
void ToonMainLight_float(out float3 Direction)
{
    #ifdef SHADERGRAPH_PREVIEW
        Direction = normalize(float3(0.5, 0.7, 0.3));
    #else
        // Points from the surface toward the light.
        Direction = GetMainLight().direction;
    #endif
}

void ToonMainLight_half(out half3 Direction)
{
    float3 direction;
    ToonMainLight_float(direction);
    Direction = (half3)direction;
}

// File-mode Custom Function: Position (Absolute World) -> Direction, LightColor, Shadow.
void ToonMainLight_float(float3 Position, out float3 Direction, out float3 LightColor, out float Shadow)
{
    #ifdef SHADERGRAPH_PREVIEW
        Direction = normalize(float3(0.5, 0.7, 0.3));
        LightColor = float3(1.0, 1.0, 1.0);
        Shadow = 1.0;
    #else
        Light mainLight = GetMainLight();
        Direction = mainLight.direction;
        LightColor = mainLight.color;
        #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
            float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(Position));
        #else
            float4 shadowCoord = TransformWorldToShadowCoord(Position);
        #endif
        Shadow = MainLightRealtimeShadow(shadowCoord);
    #endif
}

void ToonMainLight_half(float3 Position, out half3 Direction, out half3 LightColor, out half Shadow)
{
    float3 direction, color;
    float shadow;
    ToonMainLight_float(Position, direction, color, shadow);
    Direction = (half3)direction;
    LightColor = (half3)color;
    Shadow = (half)shadow;
}

#endif
