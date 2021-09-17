Shader"Voxels/voxelTerrainAtlas"{
Properties{
_Color("Color",Color)=(1,1,1,1) _MainTex("Albedo (RGB)",2D)="white"{} _MainTex1("Albedo (RGB) 1",2D)="white"{} _MainTex2("Albedo (RGB) 2",2D)="white"{} _MainTex3("Albedo (RGB) 3",2D)="white"{} _Glossiness("Smoothness",Range(0,1))=0.5 _Metallic("Metallic",Range(0,1))=0.0
_BumpMap("Normal Map",2D)="bump"{} _BumpMap1("Normal Map 1",2D)="bump"{} _BumpMap2("Normal Map 2",2D)="bump"{} _BumpMap3("Normal Map 3",2D)="bump"{}
_HeightMap("Height Map",2D)="white"{} _HeightMap1("Height Map 1",2D)="white"{} _HeightMap2("Height Map 2",2D)="white"{} _HeightMap3("Height Map 3",2D)="white"{}
_Height("Height",Range(0,.125))=.125
_TilesResolution("Atlas Tiles Resolution",float)=3 _Scale("Scale",float)=1 _Sharpness("Triplanar Blend Sharpness",float)=1
_CameraPosition("Camera Position",Vector)=(0,0,0,1)
_FogIntensity("Fog Intensity",float)=1 _FogColor("Fog Color",Color)=(1,1,1,1) _FogQuadrangularStart("Quadrangular Fog Start",Vector)=(4,112,4,1) _FogQuadrangularEnd("Quadrangular Fog End",Vector)=(8,120,8,1) _FadeQuadrangularStart("Quadrangular Fade Start",Vector)=(8,120,8,1) _FadeQuadrangularEnd("Quadrangular Fade End",Vector)=(16,128,16,1)
}
SubShader{Tags{"Queue"="AlphaTest" "RenderType"="Transparent" "IgnoreProjector"="True"}
LOD 200
Pass{
    ZWrite On
    ColorMask 0
    CGPROGRAM
    #pragma   vertex vert
    #pragma fragment frag
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
fixed4 _Color;sampler2D _MainTex;sampler2D _MainTex1;sampler2D _MainTex2;sampler2D _MainTex3;half _Glossiness;half _Metallic;
sampler2D _BumpMap;sampler2D _BumpMap1;sampler2D _BumpMap2;sampler2D _BumpMap3;
sampler2D _HeightMap;sampler2D _HeightMap1;sampler2D _HeightMap2;sampler2D _HeightMap3;
float _Height;
float _TilesResolution;float _Scale;float _Sharpness;
float4 _CameraPosition;
float _FogIntensity;fixed4 _FogColor;fixed4 _FogQuadrangularStart;fixed4 _FogQuadrangularEnd;fixed4 _FadeQuadrangularStart;fixed4 _FadeQuadrangularEnd;
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
//  Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
#pragma instancing_options assumeuniformscaling
UNITY_INSTANCING_BUFFER_START(Props)
//  Put more per-instance properties here
UNITY_INSTANCING_BUFFER_END  (Props)
Input vert(inout appdata_full v){
Input o;
return o;}
void surf(Input input,inout SurfaceOutputStandard o){
//  Find our UVs for each axis based on world position of the fragment.
half2 xUV=input.worldPos.yz*_Scale;
half2 yUV=input.worldPos.xz*_Scale;
half2 zUV=input.worldPos.xy*_Scale;
//  The worldNormal is the world-space normal of the fragment
//  Get the absolute value of the world normal.
//  Put the blend weights to the power of BlendSharpness, the higher the value, 
// the sharper the transition between the planar maps will be.
half3 blendWeights=pow(abs(WorldNormalVector(input,o.Normal)),_Sharpness);
//  Divide our blend mask by the sum of it's components, this will make x + y + z = 1
blendWeights=blendWeights/(blendWeights.x+blendWeights.y+blendWeights.z);
//  Now do texture samples from our diffuse map with each of the 3 UV sets.
float offsetUVSize=(1/_TilesResolution);
fixed4 xAxis;fixed4 bumpxAxis;
fixed4 yAxis;fixed4 bumpyAxis;
fixed4 zAxis;fixed4 bumpzAxis;
float2 offsetUV=input.uv_MainTex;
	   offsetUV=float2(clamp(offsetUV.x,0,1),clamp(offsetUV.y,0,1));
fixed4 heightxAxis=input.color.r*tex2D(_HeightMap,(frac(xUV)*offsetUVSize+offsetUV));
fixed4 heightyAxis=input.color.r*tex2D(_HeightMap,(frac(yUV)*offsetUVSize+offsetUV));
fixed4 heightzAxis=input.color.r*tex2D(_HeightMap,(frac(zUV)*offsetUVSize+offsetUV));
fixed4 h=(heightxAxis)*blendWeights.x+(heightyAxis)*blendWeights.y+(heightzAxis)*blendWeights.z;
float2 texOffset=ParallaxOffset(h.r,_Height,input.viewDir);
xAxis=input.color.r*tex2D(_MainTex,(frac(xUV)*offsetUVSize+offsetUV)+texOffset);bumpxAxis=input.color.r*tex2D(_BumpMap,(frac(xUV)*offsetUVSize+offsetUV)+texOffset);
yAxis=input.color.r*tex2D(_MainTex,(frac(yUV)*offsetUVSize+offsetUV)+texOffset);bumpyAxis=input.color.r*tex2D(_BumpMap,(frac(yUV)*offsetUVSize+offsetUV)+texOffset);
zAxis=input.color.r*tex2D(_MainTex,(frac(zUV)*offsetUVSize+offsetUV)+texOffset);bumpzAxis=input.color.r*tex2D(_BumpMap,(frac(zUV)*offsetUVSize+offsetUV)+texOffset);
fixed4 xAxis1=0;fixed4 bumpxAxis1=0;
fixed4 yAxis1=0;fixed4 bumpyAxis1=0;
fixed4 zAxis1=0;fixed4 bumpzAxis1=0;
if(input.uv2_MainTex1.x>=0){
float2 offsetUV1=input.uv2_MainTex1;
       offsetUV1=float2(clamp(offsetUV1.x,0,1),clamp(offsetUV1.y,0,1));
fixed4 heightxAxis1=input.color.g*tex2D(_HeightMap1,(frac(xUV)*offsetUVSize+offsetUV1));
fixed4 heightyAxis1=input.color.g*tex2D(_HeightMap1,(frac(yUV)*offsetUVSize+offsetUV1));
fixed4 heightzAxis1=input.color.g*tex2D(_HeightMap1,(frac(zUV)*offsetUVSize+offsetUV1));
fixed4 h1=(heightxAxis1)*blendWeights.x+(heightyAxis1)*blendWeights.y+(heightzAxis1)*blendWeights.z;
float2 texOffset1=ParallaxOffset(h1.r,_Height,input.viewDir);
xAxis1=input.color.g*tex2D(_MainTex1,(frac(xUV)*offsetUVSize+offsetUV1)+texOffset1);bumpxAxis1=input.color.g*tex2D(_BumpMap1,(frac(xUV)*offsetUVSize+offsetUV1)+texOffset1);
yAxis1=input.color.g*tex2D(_MainTex1,(frac(yUV)*offsetUVSize+offsetUV1)+texOffset1);bumpyAxis1=input.color.g*tex2D(_BumpMap1,(frac(yUV)*offsetUVSize+offsetUV1)+texOffset1);
zAxis1=input.color.g*tex2D(_MainTex1,(frac(zUV)*offsetUVSize+offsetUV1)+texOffset1);bumpzAxis1=input.color.g*tex2D(_BumpMap1,(frac(zUV)*offsetUVSize+offsetUV1)+texOffset1);
}
fixed4 xAxis2=0;fixed4 bumpxAxis2=0;
fixed4 yAxis2=0;fixed4 bumpyAxis2=0;
fixed4 zAxis2=0;fixed4 bumpzAxis2=0;
if(input.uv3_MainTex2.x>=0){
float2 offsetUV2=input.uv3_MainTex2;
       offsetUV2=float2(clamp(offsetUV2.x,0,1),clamp(offsetUV2.y,0,1));
fixed4 heightxAxis2=input.color.b*tex2D(_HeightMap2,(frac(xUV)*offsetUVSize+offsetUV2));
fixed4 heightyAxis2=input.color.b*tex2D(_HeightMap2,(frac(yUV)*offsetUVSize+offsetUV2));
fixed4 heightzAxis2=input.color.b*tex2D(_HeightMap2,(frac(zUV)*offsetUVSize+offsetUV2));
fixed4 h2=(heightxAxis2)*blendWeights.x+(heightyAxis2)*blendWeights.y+(heightzAxis2)*blendWeights.z;
float2 texOffset2=ParallaxOffset(h2.r,_Height,input.viewDir);
xAxis2=input.color.b*tex2D(_MainTex2,(frac(xUV)*offsetUVSize+offsetUV2)+texOffset2);bumpxAxis2=input.color.b*tex2D(_BumpMap2,(frac(xUV)*offsetUVSize+offsetUV2)+texOffset2);
yAxis2=input.color.b*tex2D(_MainTex2,(frac(yUV)*offsetUVSize+offsetUV2)+texOffset2);bumpyAxis2=input.color.b*tex2D(_BumpMap2,(frac(yUV)*offsetUVSize+offsetUV2)+texOffset2);
zAxis2=input.color.b*tex2D(_MainTex2,(frac(zUV)*offsetUVSize+offsetUV2)+texOffset2);bumpzAxis2=input.color.b*tex2D(_BumpMap2,(frac(zUV)*offsetUVSize+offsetUV2)+texOffset2);
}
fixed4 xAxis3=0;fixed4 bumpxAxis3=0;
fixed4 yAxis3=0;fixed4 bumpyAxis3=0;
fixed4 zAxis3=0;fixed4 bumpzAxis3=0;
if(input.uv4_MainTex3.x>=0){
float2 offsetUV3=input.uv4_MainTex3;
       offsetUV3=float2(clamp(offsetUV3.x,0,1),clamp(offsetUV3.y,0,1));
fixed4 heightxAxis3=input.color.a*tex2D(_HeightMap3,(frac(xUV)*offsetUVSize+offsetUV3));
fixed4 heightyAxis3=input.color.a*tex2D(_HeightMap3,(frac(yUV)*offsetUVSize+offsetUV3));
fixed4 heightzAxis3=input.color.a*tex2D(_HeightMap3,(frac(zUV)*offsetUVSize+offsetUV3));
fixed4 h3=(heightxAxis3)*blendWeights.x+(heightyAxis3)*blendWeights.y+(heightzAxis3)*blendWeights.z;
float2 texOffset3=ParallaxOffset(h3.r,_Height,input.viewDir);
xAxis3=input.color.a*tex2D(_MainTex3,(frac(xUV)*offsetUVSize+offsetUV3)+texOffset3);bumpxAxis3=input.color.a*tex2D(_BumpMap3,(frac(xUV)*offsetUVSize+offsetUV3)+texOffset3);
yAxis3=input.color.a*tex2D(_MainTex3,(frac(yUV)*offsetUVSize+offsetUV3)+texOffset3);bumpyAxis3=input.color.a*tex2D(_BumpMap3,(frac(yUV)*offsetUVSize+offsetUV3)+texOffset3);
zAxis3=input.color.a*tex2D(_MainTex3,(frac(zUV)*offsetUVSize+offsetUV3)+texOffset3);bumpzAxis3=input.color.a*tex2D(_BumpMap3,(frac(zUV)*offsetUVSize+offsetUV3)+texOffset3);
}
//  Finally, blend together all three samples based on the blend mask.
fixed4 c=(xAxis+xAxis1+xAxis2+xAxis3)*blendWeights.x+(yAxis+yAxis1+yAxis2+yAxis3)*blendWeights.y+(zAxis+zAxis1+zAxis2+zAxis3)*blendWeights.z;fixed4 b=(bumpxAxis+bumpxAxis1+bumpxAxis2+bumpxAxis3)*blendWeights.x+(bumpyAxis+bumpyAxis1+bumpyAxis2+bumpyAxis3)*blendWeights.y+(bumpzAxis+bumpzAxis1+bumpzAxis2+bumpzAxis3)*blendWeights.z;
//  Albedo comes from a texture tinted by color
o.Albedo=(c.rgb);o.Normal=UnpackNormal(b);float alpha=c.a;
fixed3 quadrangularViewDistance=fixed3(abs(_CameraPosition.x-input.worldPos.x),
								       abs(_CameraPosition.y-input.worldPos.y),
								       abs(_CameraPosition.z-input.worldPos.z));
float3 quadrangularTransparencyFactor=float3((_FadeQuadrangularEnd.x-quadrangularViewDistance.x)/(_FadeQuadrangularEnd.x-_FadeQuadrangularStart.x),
									         (_FadeQuadrangularEnd.y-quadrangularViewDistance.y)/(_FadeQuadrangularEnd.y-_FadeQuadrangularStart.y),
									         (_FadeQuadrangularEnd.z-quadrangularViewDistance.z)/(_FadeQuadrangularEnd.z-_FadeQuadrangularStart.z));
float transparencyFactor=quadrangularTransparencyFactor.x;
                      if(quadrangularTransparencyFactor.y<transparencyFactor){
	  transparencyFactor=quadrangularTransparencyFactor.y;}
                      if(quadrangularTransparencyFactor.z<transparencyFactor){
	  transparencyFactor=quadrangularTransparencyFactor.z;}
 clip(transparencyFactor);
				       alpha=alpha*saturate(transparencyFactor);
o.Alpha=(alpha);
//  Metallic and smoothness come from slider variables
o.Metallic  =(_Metallic  );
o.Smoothness=(_Glossiness);
}
void applyFixedFog(Input input,SurfaceOutputStandard o,inout fixed4 color){
            
//...

}
ENDCG
}
FallBack"Diffuse"}