Shader "Godmist/Dungeon/Pixel Torch"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} _FlameMask ("Flame Mask", 2D) = "black" {} _FlameBase ("Flame Base UV", Float) = .70 _FogBrightness ("Fog brightness", Float) = 1 _Phase ("Phase", Float) = 0 _ForestFixture ("Forest Fixture", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DungeonTorchLighting.hlsl"
            float _FogBrightness; float _Phase; float _FlameBase; float _ForestFixture; float4 _MainTex_TexelSize;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_FlameMask); SAMPLER(sampler_FlameMask);
            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; half4 color:COLOR; };
            float FlameSourceMask(float2 sampleUV)
            {
                float authored = SAMPLE_TEXTURE2D(_FlameMask,sampler_FlameMask,sampleUV).r;
                half4 art = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,sampleUV);
                float forest = step(.01,art.a)*step(.35,art.r)*step(art.b+.06,art.g)*step(_FlameBase,sampleUV.y);
                return lerp(authored,forest,step(.5,_ForestFixture));
            }
            V vert(A v)
            {
                V o;
                o.positionCS=TransformObjectToHClip(v.positionOS);
                o.uv=v.uv;
                o.world=TransformObjectToWorld(v.positionOS).xy;
                o.color=v.color;
                return o;
            }
            half4 frag(V i):SV_Target
            {
                // Quantised movement keeps the source pixel-art crisp while
                // using two incommensurate rates for a less mechanical flame.
                float frame=floor(_Time.y*12);
                float wave=sin(frame*.25*6.283185+_Phase);
                float2 texel=_MainTex_TexelSize.xy;
                // All fragments belonging to one authored pixel deform alike,
                // including the contact row when the sprite is magnified.
                float2 sourceUV=(floor(i.uv/texel)+.5)*texel;
                half4 source=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,sourceUV);
                source*=i.color;
                float sourceFlame=FlameSourceMask(sourceUV);
                float flameProgress=saturate((sourceUV.y-_FlameBase)/max(.001,1-_FlameBase));
                float motionProgress=saturate((flameProgress-.5*_MainTex_TexelSize.y)/max(.001,1-_FlameBase));
                float2 uv=sourceUV;
                float bendPixels=round(wave*(.75+.9*flameProgress))*step(.001,motionProgress);
                float breathPixels=round(sin(frame*.31*6.283185+_Phase*1.7)*(.6+.25*flameProgress))*step(.001,motionProgress);
                uv.x+=bendPixels*texel.x;
                uv.y+=breathPixels*texel.y;
                half4 animated=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
                animated*=i.color;
                float animatedFlame=FlameSourceMask(uv);
                float staticBody=step(.05,source.a)*(1-sourceFlame);
                half4 c=animated*animatedFlame;
                c=lerp(c,source,staticBody);
                float3 metalLight=TorchLighting(i.world,0,0,1);
                c.rgb=lerp(c.rgb,ForestShadowDetail(source.rgb*metalLight),staticBody);
                float fireLayer=animatedFlame*(1-staticBody);
                c.rgb*=lerp(1,.93+.07*wave,fireLayer);

                // A single rare ember gets a short, pixel-snapped lifetime.
                // The seed is stable for the lifetime, so it rises smoothly
                // instead of teleporting as screen-space noise.
                float lifetime=5.0;
                float sparkTick=floor((frame+_Phase*1.7)/lifetime);
                float sparkSeed=frac(sin(sparkTick*91.37+_Phase*17.13)*43758.5453);
                float sparkActive=step(.985,sparkSeed);
                float sparkProgress=frac((frame+_Phase*1.7)/lifetime);
                float sparkX=.38+frac(sparkSeed*23.71)*.24;
                sparkX+=round(sin(sparkProgress*6.283185+_Phase)*.5)*_MainTex_TexelSize.x;
                float sparkY=.70+sparkProgress*.25;
                float2 sparkPixel=_MainTex_TexelSize.xy;
                sparkX=floor(sparkX/sparkPixel.x+.5)*sparkPixel.x;
                sparkY=floor(sparkY/sparkPixel.y+.5)*sparkPixel.y;
                float spark=step(abs(i.uv.x-sparkX),sparkPixel.x*.65)
                    *step(abs(i.uv.y-sparkY),sparkPixel.y*.65)
                    *sparkActive*(1-staticBody);
                c.rgb=lerp(c.rgb,float3(1,.73,.24),spark);
                c.a=max(c.a,spark*.78);
                c.rgb *= saturate(_FogBrightness);
                return c;
            }
            ENDHLSL
        }
    }
}
