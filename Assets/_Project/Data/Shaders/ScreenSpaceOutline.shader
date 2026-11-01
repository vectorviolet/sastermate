Shader "ClimateGuardians/Screen Space Outline"
{
    Properties
    {
        _OutlineColor ("Outline color", Color) = (0, 0, 0, 1)
        _OutlineParameters ("Width, depth threshold, normal threshold", Vector) = (3, 0.02, 0.2, 0)
        _OutlinePixelSize ("Output pixel size", Vector) = (0.001, 0.001, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "DepthNormalOutline"
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float4 _OutlineParameters;
                float4 _OutlinePixelSize;
            CBUFFER_END

            struct EdgeSample
            {
                float depth;
                float rawDepth;
                float3 normal;
                float background;
                float hasNormal;
            };

            float IsBackground(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth <= 0.000001;
                #else
                    return rawDepth >= 0.999999;
                #endif
            }

            EdgeSample ReadEdgeSample(float2 uv)
            {
                EdgeSample s;
                s.rawDepth = SampleSceneDepth(saturate(uv));
                s.background = IsBackground(s.rawDepth);
                // Perspective LinearEyeDepth is incorrect for an orthographic depth buffer.
                s.depth = unity_OrthoParams.w > 0.5
                    ? LinearDepthToEyeDepth(s.rawDepth)
                    : LinearEyeDepth(s.rawDepth, _ZBufferParams);
                float3 n = SampleSceneNormals(saturate(uv));
                float lengthSquared = dot(n, n);
                s.hasNormal = lengthSquared > 0.25;
                s.normal = n * rsqrt(max(lengthSquared, 0.0001));
                return s;
            }

            float NormalDifference(EdgeSample center, EdgeSample neighbor)
            {
                // Missing normal-buffer data is not a crease. In particular dot(0, 0) = 0
                // previously classified every pixel of an unsupported material as an edge.
                if (center.background > 0.5 || neighbor.background > 0.5 ||
                    center.hasNormal < 0.5 || neighbor.hasNormal < 0.5) return 0;
                return 1.0 - clamp(dot(center.normal, neighbor.normal), -1.0, 1.0);
            }

            float RawDepthThreshold(EdgeSample center, float negativeRaw, float positiveRaw)
            {
                float worldThreshold = max(_OutlineParameters.y, 0.0001);
                float threshold = unity_OrthoParams.w > 0.5
                    ? worldThreshold / max(_ProjectionParams.z - _ProjectionParams.y, 0.0001)
                    : worldThreshold / max(abs(_ZBufferParams.z) * center.depth *
                        (center.depth + worldThreshold), 0.00000001);

                // Tolerate a couple of D24/float-depth quantization steps. Their world-space
                // size grows with distance, so a fixed eye-depth epsilon creates distant noise.
                float maxRaw = max(abs(center.rawDepth), max(abs(negativeRaw), abs(positiveRaw)));
                float precision = max(2.0 / 16777215.0, maxRaw * 0.000000238418579);
                return max(threshold, precision);
            }

            float PairEdge(EdgeSample center, EdgeSample negative, EdgeSample positive,
                float negativeProbe, float positiveProbe)
            {
                float silhouette = max(abs(center.background - negative.background),
                    abs(center.background - positive.background));
                if (silhouette > 0.5) return 1;
                if (center.background > 0.5) return 0;

                // Device depth is affine on a flat projected surface in both projection modes.
                // Linear eye depth is not affine in perspective, even on a perfectly flat plane.
                float threshold = RawDepthThreshold(center, negativeProbe, positiveProbe);
                float curvature = abs((negativeProbe + positiveProbe) * 0.5 - center.rawDepth);
                float support = max(abs(negative.rawDepth - center.rawDepth),
                    abs(positive.rawDepth - center.rawDepth));
                float depthEdge = step(threshold, curvature) * step(threshold, support);
                // A background pixel outside the requested stroke's footprint must not thicken it.
                if (IsBackground(negativeProbe) > 0.5 || IsBackground(positiveProbe) > 0.5)
                    depthEdge = 0;
                float normalChange = max(NormalDifference(center, negative),
                    NormalDifference(center, positive));
                return max(depthEdge, step(_OutlineParameters.z, normalChange));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                // Half the requested width on either side of a discontinuity.
                // UV offsets derive from pixels, never from a world-space mesh extrusion.
                float2 radius = _OutlinePixelSize.xy * (_OutlineParameters.x * 0.5);
                float2 diagonal = radius * 0.70710678;
                EdgeSample center = ReadEdgeSample(uv);
                EdgeSample left = ReadEdgeSample(uv - float2(radius.x, 0));
                EdgeSample right = ReadEdgeSample(uv + float2(radius.x, 0));
                EdgeSample down = ReadEdgeSample(uv - float2(0, radius.y));
                EdgeSample up = ReadEdgeSample(uv + float2(0, radius.y));
                EdgeSample bottomLeft = ReadEdgeSample(uv - diagonal);
                EdgeSample topRight = ReadEdgeSample(uv + diagonal);
                EdgeSample topLeft = ReadEdgeSample(uv + float2(-diagonal.x, diagonal.y));
                EdgeSample bottomRight = ReadEdgeSample(uv + float2(diagonal.x, -diagonal.y));

                // Point-filtered half-texel offsets are asymmetric: one sample may hit the
                // center texel and the other its neighbor. Use whole-texel symmetric probes
                // for slope rejection, and the requested pixel radius above for stroke width.
                float2 probeTexel = _CameraDepthTexture_TexelSize.xy / max(_RTHandleScale.xy, 0.0001);
                float2 probeRadius = max(1.0, ceil(radius / probeTexel)) * probeTexel;
                float2 probeDiagonal = max(1.0, ceil(diagonal / probeTexel)) * probeTexel;
                float leftProbe = SampleSceneDepth(saturate(uv - float2(probeRadius.x, 0)));
                float rightProbe = SampleSceneDepth(saturate(uv + float2(probeRadius.x, 0)));
                float downProbe = SampleSceneDepth(saturate(uv - float2(0, probeRadius.y)));
                float upProbe = SampleSceneDepth(saturate(uv + float2(0, probeRadius.y)));
                float bottomLeftProbe = SampleSceneDepth(saturate(uv - probeDiagonal));
                float topRightProbe = SampleSceneDepth(saturate(uv + probeDiagonal));
                float topLeftProbe = SampleSceneDepth(saturate(uv + float2(-probeDiagonal.x, probeDiagonal.y)));
                float bottomRightProbe = SampleSceneDepth(saturate(uv + float2(probeDiagonal.x, -probeDiagonal.y)));

                float edge = max(max(PairEdge(center, left, right, leftProbe, rightProbe),
                        PairEdge(center, down, up, downProbe, upProbe)),
                    max(PairEdge(center, bottomLeft, topRight, bottomLeftProbe, topRightProbe),
                        PairEdge(center, topLeft, bottomRight, topLeftProbe, bottomRightProbe)));
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, edge * _OutlineColor.a);
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
