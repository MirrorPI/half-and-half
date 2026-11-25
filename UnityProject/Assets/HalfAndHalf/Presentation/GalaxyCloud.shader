Shader "HalfAndHalf/GalaxyCloud"
{
 Properties { _Pose("Frozen or live motion",Vector)=(1,0,0,0.72) }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Pass
  {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Blend SrcAlpha OneMinusSrcAlpha
   Cull Off
   ZWrite Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _Pose;
   CBUFFER_END
   struct Input { float3 pos:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float3 orbit:TEXCOORD1; };
   struct Pixel { float4 pos:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
   Pixel vert(Input i) {
       Pixel o;
       float2 p=i.orbit.xy;
       float2 next=float2(p.x*_Pose.x-p.y*_Pose.y,p.x*_Pose.y+p.y*_Pose.x)*(1+i.orbit.z*_Pose.z);
       i.pos.xy+=(next-p)*float2(1,_Pose.w);
       o.pos=TransformObjectToHClip(i.pos);o.uv=i.uv;
       o.color=half4(SRGBToLinear(i.color.rgb),i.color.a);
       return o;
   }
   half4 frag(Pixel i):SV_Target {
       half depth=1-dot(i.uv,i.uv);
       clip(depth-.001);
       return half4(i.color.rgb,i.color.a*depth*depth);
   }
   ENDHLSL
  }
 }
}
