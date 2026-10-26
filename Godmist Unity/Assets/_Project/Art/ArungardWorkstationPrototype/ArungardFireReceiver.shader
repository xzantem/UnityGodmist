Shader "Godmist/Prototype/ArungardFireReceiver" {
 Properties {
 [PerRendererData] _MainTex("Sprite",2D)="white"{}
 _Color("Tint",Color)=(1,1,1,1) _BaseY("Ground plane Y",Float)=-1000
 _ForgeLight("Ground light XY radius",Vector)=(5,13,1.8,0) _ForgeStrength("Energy",Float)=.3
 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 #include "ArungardFireResponse.cginc"
 sampler2D _MainTex; fixed4 _Color;
 struct a2v {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;fixed4 color:COLOR;};
 v2f vert(a2v v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xy;o.color=v.color*_Color;return o;}
 fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv)*i.color;c.rgb=ForgeResponse(c.rgb,i.world);return c;}
 ENDCG }
 }
}
