float4 _CityDaylightTint;
float4 _ForgeLight; float _ForgeStrength; float _BaseY;
float3 ForgeResponse(float3 albedo,float2 world)
{
    world=(floor(world*32)+.5)/32;
    if(_BaseY>-999)world.y=_BaseY;
    float attenuation=saturate(1-length(world-_ForgeLight.xy)/_ForgeLight.z);
    attenuation*=attenuation;
    // Gentle multiplicative warmth preserves stone/wood values and texture.
    float3 daylight = dot(_CityDaylightTint.rgb, float3(1,1,1)) > .01 ? _CityDaylightTint.rgb : float3(1,1,1);
    return albedo*daylight*(1+_ForgeStrength*attenuation*float3(1,.42,.10));
}
