Shader "Janus/InstancedURP"
{
    Properties
    {
        _RenderScale ("Render Scale", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Body
            {
                float3 pos;
                float  mass;
                float3 vel;
                int    sign;
            };

            StructuredBuffer<Body> _Bodies;
            float _RenderScale;

            struct Attributes
            {
                float3 positionOS : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                Body b = _Bodies[IN.instanceID];

                float3 worldPos = b.pos + (IN.positionOS * _RenderScale);
                OUT.positionHCS = TransformWorldToHClip(worldPos);

                // Blue (+) / Red (-)
                OUT.color = (b.sign > 0) ? float4(0.2, 0.6, 1.0, 0.9) :  float4(0.6, 0.1, 0.1, 0.25);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return IN.color;
            }
            ENDHLSL
        }
    }
}
