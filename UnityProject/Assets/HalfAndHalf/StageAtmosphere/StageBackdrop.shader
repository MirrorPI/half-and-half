Shader "HalfAndHalf/StageBackdrop"
{
    Properties { [PerRendererData] _MainTex ("Painted backdrop", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Background" "Queue"="Background" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            struct Vertex { float3 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Vertex input)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(input.pos);
                o.uv = input.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb, 1);
            }
            ENDHLSL
        }
    }
}
