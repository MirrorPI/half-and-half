Shader "HalfAndHalf/PixelPlanet"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags {"LightMode"="SRPDefaultUnlit"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   struct A {float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR;};
   struct V {float4 positionCS:SV_POSITION; half3 color:TEXCOORD0;};
   V vert(A a){
       V o;
       o.positionCS=TransformObjectToHClip(a.positionOS.xyz);
       half3 n=TransformObjectToWorldNormal(a.normalOS);
       half light=saturate(.52+.5*dot(n,normalize(half3(-.48,.60,-.69))));
       o.color=SRGBToLinear(saturate(a.color.rgb*(.59+.48*light)+half3(.035,.047,.067)*(1-light)));
       // A zero vertex alpha tags only the emissive photosphere, including its cut fragments.
       // Cut caps stay opaque and retain their material-layer colors.
       if(a.color.a<.5h){
           half3 view=GetWorldSpaceNormalizeViewDir(TransformObjectToWorld(a.positionOS.xyz));
           half mu=saturate(dot(n,view));
           half3 limb=lerp(half3(.96,.72,.48),half3(1,1,1),mu);
           o.color=SRGBToLinear(a.color.rgb*limb);
       }
       return o;
   }
   half4 frag(V i):SV_Target{return half4(i.color,1);}
   ENDHLSL
  }
 }
}
