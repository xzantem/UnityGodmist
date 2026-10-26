Shader "Godmist/Prototype/City Public Ground Overlay" {
Properties { _MainTex("Dark earth",2D)="white"{} _PathTex("Packed earth",2D)="white"{} _GrassTex("Grass",2D)="white"{} _StoneTex("Cobble",2D)="white"{} _SlabTex("Slabs",2D)="white"{} _ForgeLight("Light",Vector)=(5,13,1.8,0) _ForgeStrength("Energy",Float)=.32 _BaseY("Ground",Float)=-1000 }
SubShader { Tags {"Queue"="Transparent"} Cull Off ZWrite Off
// Local public ground overlay, independent from the retained terrain shader.
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
bool arrival=w.x>9.5 && w.x<20.5 && w.y>7 && w.y<13;
bool board=w.x>9.8 && w.x<13 && w.y>19 && w.y<21;
if(!arrival && !board)discard;
float jitter=(gardenHash(floor(w*8))-.5)*.16+(gardenHash(floor(w*32))-.5)*.05;
float3 c=0;float a=0;
float3 path=tex2Dlod(_PathTex,float4(uv,0,0)).rgb;
if(arrival){
 // A gathering place belongs between the plaza and the southern public walk.
 float court=1-length((w-float2(14.5,10))/float2(4.5,.82));
 float join=min(gardenSeg(w,float2(16,10.3),float2(16,12.7)),gardenSeg(w,float2(16,9.8),float2(17,8)));
 float cover=max(gardenEdge(court,jitter*.8)*.78,gardenEdge(.56-join,jitter*.8)*.78);
 gardenOver(c,a,path,cover);
}else{
 // The reader stands south of the board, off the gate circulation lane.
 float tread=min(gardenSeg(w,float2(12,20),float2(11.25,19.6)),gardenSeg(w,float2(11.25,19.6),float2(10.8,19.95)));
 float standing=1-length((w-float2(10.8,19.9))/float2(.7,.35));
 gardenOver(c,a,path,gardenEdge(max(.22-tread,standing),jitter*.65)*.78);
 float feet=gardenRect(w,float4(10.25,20.34375,11.34375,20.5625));
 gardenOver(c,a,tex2Dlod(_SlabTex,float4(uv,0,0)).rgb*.84,gardenEdge(feet,jitter*.25)*.85);
}
clip(a-.001);
return fixed4(ForgeResponse(c/max(a,.001),w),a);
}
ENDCG
}
}}