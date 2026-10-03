// Unlit vertex-colour overlay used for vision cones, rings, markers and decals.
// _HATCH (the cones' own material): uv2.x weights a world-space diagonal hatch, so dark far ground near Ilse reads as
// lines rather than a fill (SR.5); 0 is a plain fill, 1 is lines only.
Shader "Vespertine/Overlay"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        _HatchPeriod ("Hatch Period (m)", Float) = 0.45
        _HatchWidth ("Hatch Line Width (fraction)", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha [_DstBlend]
        ZWrite Off
        Cull Off
        ZTest [_ZTest]
        Pass
        {
            Name "Overlay"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ _HATCH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0;
            #if defined(_HATCH)
                float2 uv2 : TEXCOORD1;
            #endif
            };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 hatch : TEXCOORD1; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float _HatchPeriod;
                float _HatchWidth;
            CBUFFER_END
            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color * _Color;
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
            #if defined(_HATCH)
                float3 w = TransformObjectToWorld(i.positionOS.xyz);
                o.hatch = float3(w.x, w.z, i.uv2.x);
            #else
                o.hatch = 0;
            #endif
                return o;
            }
            half4 frag (Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
            #if defined(_HATCH)
                // lines across the diagonal, a fixed world spacing, antialiased by the screen-space derivative
                float u = (i.hatch.x + i.hatch.y) * 0.70710678 / max(_HatchPeriod, 0.01);
                float d = abs(frac(u) - 0.5);
                float aa = max(fwidth(u), 1e-4);
                float lines = 1.0 - smoothstep(_HatchWidth * 0.5 - aa, _HatchWidth * 0.5 + aa, d);
                c.a *= lerp(1.0, lines, saturate(i.hatch.z));
            #endif
                return c;
            }
            ENDHLSL
        }
    }
}
