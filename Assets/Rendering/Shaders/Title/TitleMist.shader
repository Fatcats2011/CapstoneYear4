// The title screen's mist puffs and the ghosts' soft shadows: unlit, see-through, premultiplied.
// Its picture is a see-through render texture shown on the title canvas, so it writes premultiplied colour and a true
// alpha (Blend One OneMinusSrcAlpha), which the canvas shows with DoA/UI Premultiplied.
// With _SoftDistance above zero a puff fades out where it nears what is behind it (the road, a ghost), so its flat
// card never shows a hard line where it cuts into the ground. That needs the camera's depth texture.
Shader "DoA/Title Mist"
{
    Properties
    {
        _MainTex ("Puff (alpha)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SoftDistance ("Soft distance (0: off)", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _SoftDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float4 screen : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.screen = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 puff = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = puff.a * input.color.a;

                if (_SoftDistance > 0)
                {
                    float2 uv = input.screen.xy / input.screen.w;
                    float behind = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                    float here = input.screen.w;
                    alpha *= saturate((behind - here) / _SoftDistance);
                }

                return half4(puff.rgb * input.color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
