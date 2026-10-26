Shader "Godmist/Prototype/City Shadow Overlay"
{
    Properties {_ShadowMask("Shadow mask",2D)="black"{} _Strength("Strength",Float)=.43 _ShadowColor("Shadow",Color)=(.065,.085,.12,1)}
    SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
    Pass {CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #include "UnityCG.cginc"
    sampler2D _ShadowMask;float4 _GroundBounds,_ShadowColor;float _Strength;
    struct V {float4 pos:SV_POSITION;float2 world:TEXCOORD0;};
    V vert(appdata_base v){V o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;return o;}
    fixed4 frag(V i):SV_Target{float2 ground=(floor(i.world*32)+.5)/32;float2 uv=(ground-_GroundBounds.xy)/_GroundBounds.zw;
        float mask=all(uv>=0)&&all(uv<=1)?tex2D(_ShadowMask,uv).r:0;return fixed4(_ShadowColor.rgb,mask*_Strength);}
    ENDCG } }
}
