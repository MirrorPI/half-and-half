Shader "HalfAndHalf/GalaxyDust"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   float4 _Center0,_Center1,_Motion0,_Motion1,_Slow0,_Slow1,_Fast0,_Fast1;
   struct A{float4 positionOS:POSITION;half4 color:COLOR;float2 offset:TEXCOORD0;float2 group:TEXCOORD1;};
   struct V{float4 positionCS:SV_POSITION;half4 color:COLOR;};
   V vert(A i)
   {
    float4 c=i.group.x<.5?_Center0:_Center1,m=i.group.x<.5?_Motion0:_Motion1;
    if(i.group.y>.5)m=i.group.y<1.5?(i.group.x<.5?_Slow0:_Slow1):(i.group.x<.5?_Fast0:_Fast1);
    float3 p=i.positionOS.xyz;
    float3 moved=float3((p.x*m.x-p.y*m.y)*m.z,(p.x*m.y+p.y*m.x)*m.w,p.z)+c.xyz;
    moved.xy+=i.offset; // Pixel squares stay aligned with the vertical cutting plane.
    V o;o.positionCS=TransformObjectToHClip(moved);o.color=half4(SRGBToLinear(i.color.rgb),1);return o;
   }
   half4 frag(V i):SV_Target{return i.color;}
   ENDHLSL
  }
 }
}
