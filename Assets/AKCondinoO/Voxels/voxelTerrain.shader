Shader"Voxels/voxelTerrain"{
Properties{
_scale("scale",float)=1 _sharpness("triplanar blend sharpness",float)=1
_materials("materials",2DArray)="white"{}_bumps("material bumps",2DArray)="bump"{}_heights("material heights",2DArray)="white"{}_height("Height",Range(0,.125))=.05
}
SubShader{Tags{"Queue"="AlphaTest" "RenderType"="Transparent" "IgnoreProjector"="True"}
LOD 200
Pass{
ZWrite On
ColorMask 0
CGPROGRAM
#pragma   vertex vert
#pragma fragment frag
#pragma require 2darray
#include "UnityCG.cginc"
struct v2f{
float4 pos:SV_POSITION;
};
v2f vert(appdata_base v){
   v2f o;
	   o.pos=UnityObjectToClipPos(v.vertex);
return o;
}
half4 frag(v2f i):COLOR{
return half4(0,0,0,0); 
}
ENDCG  
}
ZWrite On
Blend SrcAlpha OneMinusSrcAlpha
CGPROGRAM
//  Physically based Standard lighting model, and enable shadows on all light types
#pragma surface surf Standard fullforwardshadows keepalpha addshadow finalcolor:applyFixedFog vertex:vert
//  Use shader model 3.0 target, to get nicer looking lighting
#pragma target 3.0
//  Add fog and make it work
#pragma multi_compile_fog
#pragma instancing_options assumeuniformscaling
UNITY_INSTANCING_BUFFER_START(Props)
//  Put more per-instance properties here
UNITY_INSTANCING_BUFFER_END  (Props)
float _scale;
float _sharpness;
float _height;
UNITY_DECLARE_TEX2DARRAY(_materials);UNITY_DECLARE_TEX2DARRAY(_bumps);UNITY_DECLARE_TEX2DARRAY(_heights);
struct Input{
float3 worldPos:POSITION;
float3 worldNormal:NORMAL;
float3 viewDir;
float4 color:COLOR;
float2 uv_MainTex:TEXCOORD0;
float2 uv2_MainTex1:TEXCOORD1;
float2 uv3_MainTex2:TEXCOORD2;
float2 uv4_MainTex3:TEXCOORD3;
INTERNAL_DATA
};
Input vert(inout appdata_full v){
 Input o;
return o;}
void surf(Input input,inout SurfaceOutputStandard o){
half2 uv_x=input.worldPos.yz*_scale;
half2 uv_y=input.worldPos.xz*_scale;
half2 uv_z=input.worldPos.xy*_scale;
half3 blendWeights=pow(abs(WorldNormalVector(input,o.Normal)),_sharpness);blendWeights=blendWeights/(blendWeights.x+blendWeights.y+blendWeights.z);
fixed4 height_axis_x=input.color.r*UNITY_SAMPLE_TEX2DARRAY(_heights,float3(frac(uv_x),0));
fixed4 height_axis_y=input.color.r*UNITY_SAMPLE_TEX2DARRAY(_heights,float3(frac(uv_y),0));
fixed4 height_axis_z=input.color.r*UNITY_SAMPLE_TEX2DARRAY(_heights,float3(frac(uv_z),0));
fixed4 h=(height_axis_x)*blendWeights.x
		+(height_axis_y)*blendWeights.y
		+(height_axis_z)*blendWeights.z;
float2 texOffset=ParallaxOffset(h.r,_height,input.viewDir);
fixed4 tex_axis_x=input.color.r*UNITY_SAMPLE_TEX2DARRAY(_materials,float3(frac(uv_x)+texOffset,0));
fixed4 tex_axis_y=input.color.r*UNITY_SAMPLE_TEX2DARRAY(_materials,float3(frac(uv_y)+texOffset,0));
fixed4 tex_axis_z=input.color.r*UNITY_SAMPLE_TEX2DARRAY(_materials,float3(frac(uv_z)+texOffset,0));
fixed4 bump_axis_x=input.color.r*UNITY_SAMPLE_TEX2DARRAY(_bumps,float3(frac(uv_x)+texOffset,0));
fixed4 bump_axis_y=input.color.r*UNITY_SAMPLE_TEX2DARRAY(_bumps,float3(frac(uv_y)+texOffset,0));
fixed4 bump_axis_z=input.color.r*UNITY_SAMPLE_TEX2DARRAY(_bumps,float3(frac(uv_z)+texOffset,0));
fixed4 c=(tex_axis_x)*blendWeights.x
		+(tex_axis_y)*blendWeights.y
		+(tex_axis_z)*blendWeights.z;
fixed4 b=(bump_axis_x)*blendWeights.x
		+(bump_axis_y)*blendWeights.y
		+(bump_axis_z)*blendWeights.z;
o.Albedo=(c.rgb);o.Normal=UnpackNormal(b);
float alpha=c.a;
o.Alpha=(alpha);
}
void applyFixedFog(Input input,SurfaceOutputStandard o,inout fixed4 color){
}
ENDCG
}
FallBack"Diffuse"}