Shader "Godmist/Prototype/City Household Garden Overlay" {
Properties { _MainTex("Dark earth",2D)="white"{} _PathTex("Packed earth",2D)="white"{} _GrassTex("Grass",2D)="white"{} _StoneTex("Cobble",2D)="white"{} _SlabTex("Slabs",2D)="white"{} _ForgeLight("Light",Vector)=(5,13,1.8,0) _ForgeStrength("Energy",Float)=.32 _BaseY("Ground",Float)=-1000 }
SubShader { Tags {"Queue"="Transparent"} Cull Off ZWrite Off
// Local background garden overlay, compiled independently from the retained terrain.
Pass {
Blend SrcAlpha OneMinusSrcAlpha
CGPROGRAM
#pragma target 3.0
#pragma vertex gardenVert
#pragma fragment gardenFrag
#include "UnityCG.cginc"
#include "../ArungardWorkstationPrototype/ArungardFireResponse.cginc"
sampler2D _MainTex,_PathTex,_SlabTex;
struct gardenV2f {float4 pos:SV_POSITION;float2 world:TEXCOORD0;};
gardenV2f gardenVert(appdata_base v){gardenV2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}
float gardenHash(float2 p){return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);}
float gardenRect(float2 p,float4 b){float2 d=min(p-b.xy,b.zw-p);return min(d.x,d.y);}
float gardenSeg(float2 p,float2 a,float2 b){float2 d=b-a;return length(p-a-d*saturate(dot(p-a,d)/dot(d,d)));}
float gardenEdge(float d,float j){return saturate((d+j+.07)/.14);}
void gardenOver(inout float3 c,inout float a,float3 layer,float opacity){c=c*(1-opacity)+layer*opacity;a=a*(1-opacity)+opacity;}
fixed4 gardenFrag(gardenV2f i):SV_Target {
float2 w=(floor(i.world*32)+.5)/32,uv=w/4;
bool east=w.x>42.5 && w.x<47.5 && w.y>0 && w.y<2.3;
bool southwest=w.x>-24.5 && w.x<-21.4 && w.y>-20.2 && w.y<-16.6;
if(!east && !southwest)discard;
float jitter=(gardenHash(floor(w*8))-.5)*.16+(gardenHash(floor(w*32))-.5)*.05;
float3 c=0;float a=0;
float3 soil=tex2Dlod(_MainTex,float4(uv*1.5,0,0)).rgb*float3(.68,.64,.53);
float3 path=tex2Dlod(_PathTex,float4(uv,0,0)).rgb;
if(east){
 float beds=gardenRect(w,float4(43.03125,.5,47.34375,1.40625));
 gardenOver(c,a,soil,gardenEdge(beds,jitter*.6)*.96);
 float pad=gardenRect(w,float4(43.8125,1.75,45.8125,2.1875));
 gardenOver(c,a,tex2Dlod(_SlabTex,float4(uv,0,0)).rgb*.88,gardenEdge(pad,jitter*.3)*.9);
 float tread=gardenSeg(w,float2(43.25,1.55),float2(47.25,1.55));
 gardenOver(c,a,path*.96,gardenEdge(.11-tread,jitter*.4)*.6);
}else{
 float beds=max(gardenRect(w,float4(-23.96875,-17.8125,-21.65625,-16.90625)),gardenRect(w,float4(-23.96875,-19.21875,-21.65625,-18.3125)));
 gardenOver(c,a,soil,gardenEdge(beds,jitter*.6)*.96);
 float tread=min(gardenSeg(w,float2(-24,-20),float2(-24.1875,-18.0625)),gardenSeg(w,float2(-24.1875,-18.0625),float2(-22.8,-18.0625)));
 gardenOver(c,a,path,gardenEdge(.12-tread,jitter*.45)*.65);
}
clip(a-.001);
return fixed4(ForgeResponse(c/max(a,.001),w),a);
}
ENDCG
}
}}