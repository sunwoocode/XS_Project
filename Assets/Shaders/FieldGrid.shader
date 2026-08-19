Shader "XS Project/Field Grid"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.22, 0.50, 0.27, 1)
        _LineColor ("Grid Line Color", Color) = (0.68, 0.92, 0.70, 1)
        _HighlightColor ("Selected Cell Color", Color) = (1.0, 0.72, 0.18, 1)
        _MovementColor ("Movement Range Color", Color) = (0.18, 0.62, 0.95, 1)
        _GridSize ("Grid Size", Vector) = (10, 10, 0, 0)
        _HighlightCell ("Highlighted Cell", Vector) = (-1, -1, 0, 0)
        _ReachabilityMap ("Reachability Map", 2D) = "black" {}
        _LineWidth ("Line Width", Range(0.005, 0.15)) = 0.035
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;
                half4 _HighlightColor;
                half4 _MovementColor;
                float4 _GridSize;
                float4 _HighlightCell;
                float _LineWidth;
            CBUFFER_END

            TEXTURE2D(_ReachabilityMap);
            SAMPLER(sampler_ReachabilityMap);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 gridSize = max(_GridSize.xy, float2(1.0, 1.0));
                float2 uv = saturate(input.positionOS.xz + 0.5);
                float2 cellPosition = frac(uv * gridSize);
                float2 distanceToEdge = min(cellPosition, 1.0 - cellPosition);
                float edgeDistance = min(distanceToEdge.x, distanceToEdge.y);
                float antialiasing = max(fwidth(edgeDistance), 0.0001);
                float gridLine = 1.0 - smoothstep(_LineWidth, _LineWidth + antialiasing, edgeDistance);
                float topFace = step(0.99, input.normalOS.y);
                float2 cellIndex = min(floor(uv * gridSize), gridSize - 1.0);
                float cellDelta = max(
                    abs(cellIndex.x - _HighlightCell.x),
                    abs(cellIndex.y - _HighlightCell.y));
                float selectedCell = (1.0 - step(0.5, cellDelta)) * _HighlightCell.z * topFace;
                float2 reachabilityUv = (cellIndex + 0.5) / gridSize;
                float reachableCell =
                    SAMPLE_TEXTURE2D(_ReachabilityMap, sampler_ReachabilityMap, reachabilityUv).r *
                    _HighlightCell.z * topFace;
                half4 surfaceColor = lerp(_BaseColor, _MovementColor, reachableCell);
                surfaceColor = lerp(surfaceColor, _HighlightColor, selectedCell);

                return lerp(surfaceColor, _LineColor, gridLine * topFace);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
