Shader "Godmist/Prototype/ArungardCourtyard" {
 Properties { _MainTex("Earth",2D)="white"{} _Size("World size",Vector)=(8,4,0,0) _ForgeLight("Ground light",Vector)=(5,13,1.8,0) _ForgeStrength("Energy",Float)=.32 _BaseY("Ground Y",Float)=-1000 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 #include "../ArungardWorkstationPrototype/ArungardFireResponse.cginc"
 sampler2D _MainTex; float4 _Size;
 struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}
 fixed4 frag(v2f i):SV_Target {
   float2 pixel=floor(i.world*32);
   float2 local=abs((i.uv-.5)*_Size.xy);
   float edge=min(_Size.x*.5-local.x,_Size.y*.5-local.y);
   float noise=frac(sin(dot(pixel,float2(12.9898,78.233)))*43758.5453);
   fixed4 c=tex2D(_MainTex,(pixel+.5)/128);
   c.a=saturate((edge-.12+(noise-.5)*.25)/.5);
   c.rgb=ForgeResponse(c.rgb,i.world);
   return c;
 }
 ENDCG }
 }
}
