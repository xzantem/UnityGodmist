Shader "Godmist/Forest/Masked Canopy Silhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Actor",2D)="white" {}
        _Tint ("Occluded contour",Color)=(.56,.62,.5,.65)
        _Canopy0Tex ("Canopy 0",2D)="black" {}
        _Canopy1Tex ("Canopy 1",2D)="black" {}
        _Canopy2Tex ("Canopy 2",2D)="black" {}
        _Canopy3Tex ("Canopy 3",2D)="black" {}
        _CanopyCount ("Count",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            TEXTURE2D(_Canopy0Tex);SAMPLER(sampler_Canopy0Tex);
            TEXTURE2D(_Canopy1Tex);SAMPLER(sampler_Canopy1Tex);
            TEXTURE2D(_Canopy2Tex);SAMPLER(sampler_Canopy2Tex);
            TEXTURE2D(_Canopy3Tex);SAMPLER(sampler_Canopy3Tex);
            float4 _MainTex_TexelSize;
            float4x4 _Canopy0Matrix,_Canopy1Matrix,_Canopy2Matrix,_Canopy3Matrix;
            float4 _Canopy0UV,_Canopy1UV,_Canopy2UV,_Canopy3UV;
            float _CanopyCount;half4 _Tint;
            struct A {float3 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            V vert(A a) {V o;o.positionCS=TransformObjectToHClip(a.positionOS);o.world=TransformObjectToWorld(a.positionOS);o.uv=a.uv;return o;}
            float Inside(float2 uv) {return step(0,uv.x)*step(0,uv.y)*step(uv.x,1)*step(uv.y,1);}
            half4 frag(V i):SV_Target
            {
                half body=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;clip(body-.1);
                float occluded=0;float2 uv;
                if(_CanopyCount>0) {uv=mul(_Canopy0Matrix,float4(i.world,1)).xy;occluded=max(occluded,Inside(uv)*SAMPLE_TEXTURE2D(_Canopy0Tex,sampler_Canopy0Tex,_Canopy0UV.xy+uv*_Canopy0UV.zw).a);}
                if(_CanopyCount>1) {uv=mul(_Canopy1Matrix,float4(i.world,1)).xy;occluded=max(occluded,Inside(uv)*SAMPLE_TEXTURE2D(_Canopy1Tex,sampler_Canopy1Tex,_Canopy1UV.xy+uv*_Canopy1UV.zw).a);}
                if(_CanopyCount>2) {uv=mul(_Canopy2Matrix,float4(i.world,1)).xy;occluded=max(occluded,Inside(uv)*SAMPLE_TEXTURE2D(_Canopy2Tex,sampler_Canopy2Tex,_Canopy2UV.xy+uv*_Canopy2UV.zw).a);}
                if(_CanopyCount>3) {uv=mul(_Canopy3Matrix,float4(i.world,1)).xy;occluded=max(occluded,Inside(uv)*SAMPLE_TEXTURE2D(_Canopy3Tex,sampler_Canopy3Tex,_Canopy3UV.xy+uv*_Canopy3UV.zw).a);}
                clip(occluded-.1);
                float2 px=_MainTex_TexelSize.xy;
                half inner=min(min(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(px.x,0)).a,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(px.x,0)).a),
                    min(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(0,px.y)).a,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(0,px.y)).a));
                half edge=1-step(.1,inner);
                return half4(_Tint.rgb,_Tint.a*body*occluded*lerp(.12,1,edge));
            }
            ENDHLSL
        }
    }
}
