Shader "HalfAndHalf/GalaxyVoxel"
{
 Properties { _Starlight("Stellar luminosity",Range(0,1))=0
              [HideInInspector] _CutWave("Cut wave",Vector)=(0,0,0,0) }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Tags {"LightMode"="SRPDefaultUnlit"}
   ZWrite On
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _CutWave;
   half _Starlight;
   CBUFFER_END
   struct Input {float3 pos:POSITION; half4 color:COLOR;};
   struct Pixel {float4 pos:SV_POSITION; float3 world:TEXCOORD0; float localX:TEXCOORD1; half3 color:COLOR;};
   Pixel vert(Input i){
       Pixel o;o.localX=i.pos.x;o.world=TransformObjectToWorld(i.pos);o.pos=TransformWorldToHClip(o.world);o.color=i.color.rgb;return o;
   }
   half4 frag(Pixel i):SV_Target{
       float3 n=normalize(cross(ddy(i.world),ddx(i.world)));
       if(dot(n,GetWorldSpaceViewDir(i.world))<0)n=-n;
       half light=saturate(dot(n,normalize(float3(-.52,.70,-.55))));
       half3 tint=i.color*(.61h+.48h*light)+half3(.018h,.025h,.047h)*(1-light);
       half radiance=smoothstep(.37h,.94h,max(i.color.r,max(i.color.g,i.color.b)));
       half3 luminous=i.color*(.72h+.44h*light)+radiance*half3(.039h,.034h,.038h);
       tint=lerp(tint,luminous,_Starlight*radiance);
       float age=_CutWave.y-abs(i.localX-_CutWave.x)*_CutWave.w;
       half pulse=_CutWave.z*smoothstep(0,.045,age)*(1-smoothstep(.05,.32,age));
       tint+=half3(.20h,.33h,.43h)*pulse*(.65h+.35h*radiance);
       return half4(SRGBToLinear(saturate(tint)),1);
   }
   ENDHLSL
  }
 }
}
