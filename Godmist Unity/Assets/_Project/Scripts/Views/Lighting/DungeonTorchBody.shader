Shader "Godmist/Dungeon/Torch Body"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            #include "DungeonTorchLighting.hlsl"
            float4 _FootWorld;
            float _IsActor;
            struct A { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float2 world : TEXCOORD1; };
            V vert(A v) { V o; SetUpSpriteInstanceProperties(); v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy); float3 w = TransformObjectToWorld(v.positionOS); o.positionCS = TransformWorldToHClip(w); o.world = w.xy; o.uv = v.uv; o.color = v.color * unity_SpriteColor; return o; }
            half4 frag(V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                c = ForestPropSeparation(TEXTURE2D_ARGS(_MainTex,sampler_MainTex),i.uv,c,i.color.a);
                c.a *= ForestCanopyOpacity(i.world);
                clip(c.a - .0001);
                c.rgb *= TorchLighting(lerp(i.world, _FootWorld.xy, _IsActor), 0, _IsActor, 1-_IsActor);
                c.rgb = ForestShadowDetail(c.rgb);
                return c;
            }
            ENDHLSL
        }
    }
}
