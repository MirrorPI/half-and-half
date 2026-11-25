Shader "HalfAndHalf/VoxelPalette"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Tags {"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   struct A{float4 positionOS:POSITION;half4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;half4 color:COLOR;};
   V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.color=half4(SRGBToLinear(i.color.rgb),1);return o;}
   half4 frag(V i):SV_Target{return i.color;}
   ENDHLSL
  }
 }
}
