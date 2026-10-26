#ifndef GODMIST_TORCH_LIGHTING
#define GODMIST_TORCH_LIGHTING
TEXTURE2D_ARRAY(_ShadowMasks); SAMPLER(sampler_ShadowMasks);
float4 _GroundBounds, _Torches[8], _TorchColors[8];
float _Ambient, _ReceiveShadows; int _TorchCount;
float _ForestShadowDetail;
half3 ForestShadowDetail(half3 color)
{
    if (_ForestShadowDetail <= 0) return color;
    float peak = max(color.r, max(color.g, color.b));
    // Lift the shadow toe with one RGB gain: hue, black and bright light remain
    // intact. Apply before blending, so fog tint and canopy alpha still win.
    float lifted = pow(saturate(peak), .8);
    float gain = peak > 0 && peak < 1 ? lifted / max(peak, .00001) : 1;
    return color * lerp(1, gain, _ForestShadowDetail);
}
float _ForestPropReadability;
float _ForestReadabilityProfile;
float4 _ForestPropUV, _ForestPropTexel;
float _ForestCanopy, _ForestCanopyFade, _ForestCanopyRootTopY;
float _ForestCanopyCellSize;
float4 _ForestCanopyRootBounds;
half ForestCanopyOpacity(float2 world)
{
    if (_ForestCanopy < .5 || _ForestCanopyFade <= 0) return 1;
    float side = _ForestCanopyRootBounds.z > .5
        ? max(_ForestCanopyRootBounds.x-world.x, world.x-_ForestCanopyRootBounds.y) : -1;
    // Keep the physical root rectangle solid. Every other leaf on this tree
    // shares one smoothly animated alpha, with no actor-shaped cutout.
    float crown = max(world.y-_ForestCanopyRootTopY, side);
    float feather = max(_ForestCanopyCellSize, .03125) * .125;
    return 1 - saturate(_ForestCanopyFade) * saturate(crown/feather);
}
half ForestPropAlpha(TEXTURE2D_PARAM(tex, texSampler), float2 uv)
{
    if (any(uv < _ForestPropUV.xy) || any(uv > _ForestPropUV.zw)) return 0;
    return SAMPLE_TEXTURE2D(tex, texSampler, uv).a;
}
half4 ForestPropSeparation(TEXTURE2D_PARAM(tex, texSampler), float2 uv, half4 color, half tintAlpha)
{
    // Opt-in only: floor textures and Catacombs keep their old path.
    if (_ForestPropReadability <= 0) return color;
    // Preserve authored opaque edge pixels. A constant bright inner contour
    // makes dark sprites look selected and erases the original edge shading.
    bool actor = _ForestReadabilityProfile > .5 && _ForestReadabilityProfile < 1.5;
    // Actors keep their authored silhouette, including partially transparent
    // edge pixels. Selection/hover owns its separate intentional outline.
    if (actor) return half4(color.rgb * 1.16, color.a);
    half gain = 1.18;
    if (color.a >= 1) return half4(color.rgb * gain, color.a);
    float2 step = _ForestPropTexel.xy;
    half edge = max(max(ForestPropAlpha(TEXTURE2D_ARGS(tex,texSampler),uv+float2(step.x,0)),
                        ForestPropAlpha(TEXTURE2D_ARGS(tex,texSampler),uv-float2(step.x,0))),
                    max(ForestPropAlpha(TEXTURE2D_ARGS(tex,texSampler),uv+float2(0,step.y)),
                        ForestPropAlpha(TEXTURE2D_ARGS(tex,texSampler),uv-float2(0,step.y))));
    // Only dense foliage retains the wider falloff. Small props get a
    // restrained one-pixel dark rim outside their alpha shape.
    half outer = 0;
    if (_ForestReadabilityProfile < .5)
        outer = max(max(ForestPropAlpha(TEXTURE2D_ARGS(tex,texSampler),uv+float2(2*step.x,0)),
                         ForestPropAlpha(TEXTURE2D_ARGS(tex,texSampler),uv-float2(2*step.x,0))),
                     max(ForestPropAlpha(TEXTURE2D_ARGS(tex,texSampler),uv+float2(0,2*step.y)),
                         ForestPropAlpha(TEXTURE2D_ARGS(tex,texSampler),uv-float2(0,2*step.y))));
    half strength = _ForestPropReadability * (_ForestReadabilityProfile > 1.5 ? .8 : 1);
    half rim = max(edge,outer*.38) * (1-color.a) * strength * tintAlpha;
    half alpha = color.a + rim;
    half3 rimColor = half3(.012,.016,.011);
    half3 rgb = (color.rgb*gain*color.a + rimColor*rim) / max(alpha,.0001);
    return half4(rgb,alpha);
}
// One stone response shared by floor, walls and architectural props.
float3 StoneTorchColor(float distanceSquared, float3 flameColor)
{
    float nearFlame = saturate(1-distanceSquared/4);
    nearFlame *= nearFlame;
    // Keep the same warm spectrum throughout the radius; only energy falls off.
    return lerp(float3(.95,.95,.95), flameColor, .65) * (1+.65*nearFlame);
}
float3 PixelLightTone(float3 light)
{
    light *= 1.35 / max(1.35, max(light.r,max(light.g,light.b)));
    return floor(max(light,0)*24+.5)/24;
}
float3 TorchLighting(float2 world, float receiveShadows, float facingSurface, float stoneSurface)
{
    float2 ground = (floor(world * 32) + .5) / 32;
    float2 uv = (ground - _GroundBounds.xy) / _GroundBounds.zw;
    float3 light = _Ambient * float3(.83,.91,1);
    for (int index=0; index<min(_TorchCount,8); index++)
    {
        float2 delta = ground - _Torches[index].xy;
        float distanceSquared = dot(delta,delta);
        float attenuation = saturate(1-distanceSquared/(_Torches[index].z*_Torches[index].z));
        // Do not fetch a shadow-array layer for pixels this source cannot light.
        if (attenuation <= 0) continue;
        float shadow = 0;
        if (receiveShadows > .5 && all(uv>=0) && all(uv<=1))
            shadow = SAMPLE_TEXTURE2D_ARRAY(_ShadowMasks,sampler_ShadowMasks,uv,index).r;
        float facing = lerp(1, lerp(.35,1,smoothstep(-.8,.8,delta.y)), facingSurface);
        float3 source = lerp(_TorchColors[index].rgb,
            StoneTorchColor(distanceSquared, _TorchColors[index].rgb), stoneSurface);
        // Preserve some diffuse light under the silhouette so overlapping shadows remain readable.
        light += source * (_Torches[index].w * attenuation * (1-shadow*.8) * facing);
    }
    // Preserve hue when several warm lights overlap instead of clipping R/G independently.
    return PixelLightTone(light);
}
#endif
