Shader "Godmist/Dungeon/Animated Silhouette Volume Mask"
{
    Properties { [PerRendererData] _MainTex ("Current sprite frame", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend One One BlendOp Max
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize, _UVBounds;
            float4x4 _ShadowVP;
            float4 _Anchor, _FootLocal, _Scale, _HeightBasis, _ContactLine;
            float _VolumeHalfDepth, _DepthPixels;
            struct A { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float depth : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                float2 local = v.positionOS.xy - _FootLocal.xy;
                float baseline = _ContactLine.x * local.x + _ContactLine.y;
                float height = (local.y-baseline)*abs(_Scale.y);
                float2 ground = _Anchor.xy + float2(local.x*_Scale.x,baseline*_Scale.y)
                    + height*_HeightBasis.xy;
                // Depth belongs to the caster, not a screen-space width limit.
                // It vanishes at the contact line, keeping the original soles fixed.
                ground.y += v.positionOS.z * _VolumeHalfDepth * .75 * saturate(height/.2);
                o.positionCS=mul(_ShadowVP,float4(ground,0,1));
                o.uv=v.uv; o.depth=v.positionOS.z; return o;
            }
            float Alpha(float2 uv)
            {
                if(any(uv<_UVBounds.xy) || any(uv>_UVBounds.zw)) return 0;
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a;
            }
            half4 frag(V i) : SV_Target
            {
                clip(Alpha(i.uv)-.5);
                float2 r=_MainTex_TexelSize.xy * (abs(i.depth)*_DepthPixels);
                // Erosion makes thick interiors rounder in depth; thin limbs and
                // weapons get correspondingly little volume from the same frame.
                clip(min(min(Alpha(i.uv+float2(r.x,0)),Alpha(i.uv-float2(r.x,0))),
                    min(Alpha(i.uv+float2(0,r.y)),Alpha(i.uv-float2(0,r.y))))-.5);
                return 1;
            }
            ENDHLSL
        }
    }
}
