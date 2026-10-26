Shader "Godmist/Dungeon/Silhouette Floor"
{
    Properties { _ForestSurfaceEnabled ("Forest surface", Float) = 0 _ForestFogLitter ("Forest fog litter", Float) = 0 _ForestDifficultTerrainMask ("Difficult terrain cells", 2D) = "black" {} _ForestSurfaceMask ("Forest paths", 2D) = "black" {} _ForestLitter ("Forest litter", 2D) = "white" {} _ForestSoil ("Forest soil", 2D) = "white" {} [PerRendererData] _MainTex ("Tile", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) _ReceiveShadows ("Receive shadows", Float) = 1 _ForestPatchBounds ("Forest patch centre and half size", Vector) = (0,0,0,0) }
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
            TEXTURE2D(_ForestSurfaceMask); SAMPLER(sampler_ForestSurfaceMask);
            TEXTURE2D(_ForestLitter); SAMPLER(sampler_ForestLitter);
            TEXTURE2D(_ForestSoil); SAMPLER(sampler_ForestSoil);
            TEXTURE2D(_ForestDifficultTerrainMask); SAMPLER(sampler_ForestDifficultTerrainMask);
            float _ForestSurfaceEnabled;
            float _ForestFogLitter;
            float4 _ForestSurfaceBounds;
            float4 _Color;
            float4 _ForestPatchBounds;
            float _ForestBackdrop;
            float4 _ForestBackdropBounds;
            float4 _ForestGroundTint;
            float _ForestFogMesh;
            struct A { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; half4 color : COLOR; };
            V vert(A v)
            {
                V o;
                half4 spriteColor = 1;
                // The fog composite is a MeshRenderer, not a SpriteRenderer:
                // do not inherit sprite-only flip/tint inputs for world vertices.
                if (_ForestFogMesh < .5)
                {
                    SetUpSpriteInstanceProperties();
                    v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                    spriteColor = unity_SpriteColor;
                }
                float3 w = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(w); o.world = w.xy;
                o.uv = v.uv; o.color = v.color * _Color * spriteColor;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                if (_ForestBackdrop > .5)
                {
                    float2 edge = min(i.world - _ForestBackdropBounds.xy,
                        _ForestBackdropBounds.xy + _ForestBackdropBounds.zw - i.world);
                    c.a *= smoothstep(0, 4, min(edge.x, edge.y));
                }
                if (_ForestPatchBounds.z > 0)
                {
                    float2 patch = (i.world - _ForestPatchBounds.xy) / _ForestPatchBounds.zw;
                    float2 pixel = floor(i.world * 32);
                    float noise = frac(sin(dot(pixel, float2(12.9898,78.233))) * 43758.5453);
                    // Circular overlapping patches, with pixel-sized edge variation.
                    c.a *= 1 - smoothstep(.74, 1, length(patch) + (noise - .5) * .08);
                }
                // Reject empty underlay pixels before sampling the three forest
                // surface textures. Opt-in props may still grow a rim here.
                if (_ForestPropReadability <= 0) clip(c.a - .0001);
                if (_ForestSurfaceEnabled > .5)
                {
                    // Tile rotations and the large backdrop interpolate the same
                    // world pixel with slightly different float error at edges.
                    // Anchor both to its centre before texture/light sampling.
                    i.world = (floor(i.world * 32 + .001) + .5) / 32;
                    // World UVs keep broad patches continuous across tile rotations.
                    float2 world = floor(i.world * 32) / 32;
                    float2 maskUV = (world - _ForestSurfaceBounds.xy) / _ForestSurfaceBounds.zw;
                    half4 surface = SAMPLE_TEXTURE2D(_ForestSurfaceMask, sampler_ForestSurfaceMask, maskUV);
                    float patch = surface.g;
                    // Woodland apron is decorative litter, never a preview
                    // of routes through still unexplored walkable terrain.
                    bool decorativeSurface = _ForestPatchBounds.z > 0 || _ForestFogLitter > .5;
                    float path = decorativeSurface ? 0
                        : smoothstep(.12 + (surface.b-.5)*.16, .8, surface.r);
                    float wear = lerp(.4, .82, smoothstep(.3, .7, surface.b));
                    // Reuse baked broad/detail noise: damp pockets and moss need
                    // neither extra textures nor a per-frame CPU update.
                    float damp = smoothstep(.54, .72, patch + (surface.b - .5) * .26);
                    float moss = (1 - smoothstep(.28, .48, patch)) * (1 - path);
                    float mud = max(path * wear, damp * .55);
                    half3 litter = SAMPLE_TEXTURE2D(_ForestLitter, sampler_ForestLitter, frac(world * .5)).rgb;
                    half3 soil = SAMPLE_TEXTURE2D(_ForestSoil, sampler_ForestSoil, frac(world * .4)).rgb;
                    half3 foliageTint = lerp(half3(.72,.82,.68), half3(1.05,.99,.8), smoothstep(.25,.75,patch));
                    half3 pathTint = lerp(half3(.42,.48,.38), half3(.68,.59,.43), path);
                    half3 forestFloor = lerp(litter * foliageTint, soil * pathTint, mud);
                    forestFloor *= lerp(half3(1,1,1), half3(.72,.78,.74), damp * .65);
                    forestFloor = lerp(forestFloor, litter * half3(.52,.69,.43), moss * .55);
                    // Decorative woods/fog cannot expose contact imprints of
                    // objects on the hidden side of a discovery frontier.
                    // Native sprite tint and mesh vertex colour are different
                    // input paths. Use one uniform for continuous litter.
                    // Forest knowledge is a separate full-scene composite;
                    // native tint must not dim the path a second time.
                    half3 tint = _ForestBackdrop > .5 || _ForestFogMesh > .5
                        ? _ForestGroundTint.rgb
                        : decorativeSurface ? i.color.rgb : lerp(_ForestGroundTint.rgb, i.color.rgb, path);
                    // Only authored slow cells receive this muddy, broken
                    // surface. Off-map litter never previews hidden hazards.
                    half difficult = decorativeSurface ? 0 : SAMPLE_TEXTURE2D(_ForestDifficultTerrainMask,sampler_ForestDifficultTerrainMask,maskUV).r;
                    half3 churnedMud = soil * half3(.36,.29,.20) * lerp(.82,1.18,surface.b);
                    forestFloor = lerp(forestFloor,churnedMud,difficult*.85);
                    c.rgb = forestFloor * (decorativeSurface ? 1 : surface.a) * tint;
                }
                c = ForestPropSeparation(TEXTURE2D_ARGS(_MainTex,sampler_MainTex),i.uv,c,i.color.a);
                clip(c.a - .0001);
                c.rgb *= TorchLighting(i.world, _ReceiveShadows, 0, 1);
                c.rgb = ForestShadowDetail(c.rgb);
                return c;
            }
            ENDHLSL
        }
    }
}
