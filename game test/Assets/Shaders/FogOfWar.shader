Shader "Custom/FogOfWar"
{
    Properties
    {
        _FogTex   ("Fog Texture", 2D) = "black" {}
        _FogColor ("Fog Color", Color) = (0.05, 0.05, 0.05, 0.95)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // 标准半透明混合：迷雾叠加在世界上方
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // 双面渲染，兼容俯视/斜视摄像机
        Cull Off

        Pass
        {
            Name "FogOfWar"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_FogTex);
            SAMPLER(sampler_FogTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FogTex_ST;
                half4  _FogColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _FogTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // R 通道：0.0 = 迷雾，1.0 = 已揭开
                half revealed = SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, IN.uv).r;

                // smoothstep 在 [0.3, 0.7] 区间柔化揭开边缘
                half fogAlpha = smoothstep(0.3, 0.7, 1.0 - revealed);

                // 完全揭开的像素直接丢弃，避免透明 overdraw
                clip(fogAlpha - 0.01);

                return half4(_FogColor.rgb, fogAlpha * _FogColor.a);
            }
            ENDHLSL
        }
    }
}
