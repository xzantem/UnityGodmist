Shader "Godmist/Forest/Ent Arena Fire"
{
    Properties { [PerRendererData] _MainTex("Tree mask", 2D) = "white" {}
        _FireLow("Corrupted ember", Color) = (.13,.19,.025,1)
        _FireCore("Phase II flame", Color) = (.85,.78,.14,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _SpriteBounds; float _Phase;
            half4 _FireLow, _FireCore;
            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 local:TEXCOORD1; half4 color:COLOR; };
            V vert(A v)
            {
                V o; SetUpSpriteInstanceProperties();
                o.local=(v.positionOS.xy-_SpriteBounds.xy)/max(_SpriteBounds.zw,.001);
                v.positionOS=UnityFlipSprite(v.positionOS,unity_SpriteProps.xy);
                o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv;
                o.color=v.color*unity_SpriteColor; return o;
            }
            half4 frag(V i):SV_Target
            {
                // World art stays at 32 PPU. Flame motion advances at the shared 0.2s cadence.
                float2 pixels=floor(i.local*_SpriteBounds.zw*32);
                float2 p=pixels/max(_SpriteBounds.zw*32,1);
                float t=floor(_Time.y*5);
                float mask=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
                // Narrow native-pixel tongues taper as they rise. Keep most
                // fire at roots/trunk, with a few small lower-branch patches;
                // replacing the whole crown made the forest read as neon blobs.
                float sway=sin(pixels.y*.32-t*.5+_Phase)*1.3;
                float tongueX=frac((pixels.x+sway+_Phase*3)/8)*2-1;
                float rise=frac(pixels.y/18-t*.17+sin(floor(pixels.x/8)+_Phase)*.15);
                float width=(1-rise)*.7;
                float tongue=1-smoothstep(width,width+.18,abs(tongueX));
                float vertical=smoothstep(0,.12,rise)*(1-smoothstep(.65,1,rise));
                float roots=1-smoothstep(.2,.48,p.y);
                float2 patch=floor(pixels/float2(6,10));
                float random=frac(sin(dot(patch,float2(12.9898,78.233))+_Phase)*43758.5453);
                float branches=.34*smoothstep(.3,.42,p.y)*(1-smoothstep(.64,.85,p.y))*step(.78,random);
                float fire=tongue*vertical*max(roots,branches);
                float core=(1-smoothstep(.08,.22,abs(tongueX)))*(1-smoothstep(.3,.65,rise));
                half3 green=lerp(_FireLow.rgb,_FireCore.rgb,core);
                // Partial opacity preserves bark/leaf structure beneath the
                // flame. Sprite tint also carries remembered-fog attenuation.
                return half4(green*i.color.rgb,mask*fire*.68*i.color.a);
            }
            ENDHLSL
        }
    }
}
