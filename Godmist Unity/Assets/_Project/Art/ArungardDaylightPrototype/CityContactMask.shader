Shader "Godmist/Prototype/City Contact Mask"
{
    SubShader { Tags {"RenderPipeline"="UniversalPipeline"}
    Pass { Cull Off ZWrite Off ZTest Always Blend One One BlendOp Max
    HLSLPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    float4x4 _ShadowVP;
    struct A {float3 positionOS:POSITION;};struct V {float4 positionCS:SV_POSITION;};
    V vert(A v){V o;o.positionCS=mul(_ShadowVP,mul(unity_ObjectToWorld,float4(v.positionOS,1)));return o;}
    half4 frag(V i):SV_Target{return 1;}
    ENDHLSL } }
}
