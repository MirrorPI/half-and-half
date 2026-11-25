Shader "HalfAndHalf/GalaxyHaze"
{
 Properties {
  _DustTint("Diffuse dust",Color)=(.11,.13,.26,1)
  _Slice("Slice in galaxy space",Vector)=(0,0,0,0)
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry-10" "RenderType"="Transparent" "DisableBatching"="True"}
  Pass {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _DustTint;
   float4 _Slice;
   CBUFFER_END
   struct Input {float3 center:POSITION; half4 color:COLOR; float3 uv:TEXCOORD0;};
   struct Pixel {float4 pos:SV_POSITION; half2 uv:TEXCOORD0; half3 light:TEXCOORD1; float slice:TEXCOORD2;};
   Pixel vert(Input i) {
    Pixel o;
    float3 center=TransformObjectToWorld(i.center);
    float3 displacement=mul((float3x3)UNITY_MATRIX_I_V,float3(i.uv.xy*i.uv.z,0));
    float3 world=center+displacement;
    o.pos=TransformWorldToHClip(world);o.uv=i.uv.xy;o.light=SRGBToLinear(i.color.rgb);
    o.slice=(TransformWorldToObject(world).x-_Slice.x)*_Slice.y;
    return o;
   }
   half4 frag(Pixel i):SV_Target {
    clip(i.slice+.00001);
    half d=dot(i.uv,i.uv);
    half wing=saturate(1-d);
    half center=exp2(-10*d);
    half3 pigment=lerp(_DustTint.rgb,i.light,.08h+.67h*center);
    return half4(pigment,.27h*wing*wing);
   }
   ENDHLSL
  }
 }
}
