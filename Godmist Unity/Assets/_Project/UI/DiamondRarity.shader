Shader "Godmist/UI/Diamond Rarity"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Frame ("Slot outline", Float) = 0
        _Diamond ("Diamond shimmer", Float) = 1
        [HideInInspector] _ShinePhase ("Preview Phase", Float) = -1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 worldPosition:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _ShinePhase;
            float _Frame, _Diamond;
            int _UIVertexColorAlwaysGammaSpace;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb=GammaToLinearSpace(v.color.rgb);
                o.worldPosition=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=i.color; c.a*=tex2D(_MainTex,i.uv).a;
                // One native pixel around a 64px slot; retain the small bottom
                // marker. The transparent centre never tints the item illustration.
                float2 grid=_Frame>.5 ? float2(64,64) : float2(24,3);
                float2 cell=min(floor(i.uv*grid),grid-1);
                if (_Frame>.5)
                {
                    float edge=(cell.x<1 || cell.x>=63 || cell.y<1 || cell.y>=63) ? 1 : 0;
                    float marker=(cell.x>=20 && cell.x<44 && cell.y<3) ? 1 : 0;
                    c.a*=max(edge,marker);
                }
                float2 pixel=cell/grid;
                float phase=_ShinePhase>=0 ? _ShinePhase : frac(_Time.y*.22);
                float distance=abs(pixel.x+.12*pixel.y-(phase*1.6-.3));
                // Saturated cyan is the resting color. White is confined to the
                // travelling highlight instead of washing out the whole accent.
                fixed shine=_Diamond>.5 ? (distance<.04 ? .85 : (distance<.09 ? .28 : 0)) : 0;
                c.rgb=lerp(c.rgb,fixed3(.96,1,1),shine);
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.worldPosition.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
