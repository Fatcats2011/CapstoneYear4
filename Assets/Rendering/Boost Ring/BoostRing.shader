// The boost ring under a scooter (BoostRing): unlit, see-through and added on top of what is behind it, so it glows.
// It's drawn on a flat quad: the band is the UV distance from the quad's centre, and the fill runs clockwise from the
// scooter's nose (+V) by angle, up to _Fill. The rest of the band is a faint track. _Dashed breaks the filled part into
// dashes (a slowed recharge, carrying a package). The colour is _ColorLow while charging (the horns' colour now) and
// _ColorFull when full, brightened by _Pulse. A spent boost draws a second band at _BurstScale times the radius, faded
// by _BurstAlpha. _Fade hides it all while the scooter is in the air.
// No depth write and a normal depth test, lifted a little off the ground by its script: the ground hides it rather than
// it flickering against the ground. Plain HLSL, nothing platform-specific (D3D11, Metal, Vulkan).
Shader "DoA/Boost Ring"
{
    Properties
    {
        _Fill ("Fill (0-1)", Range(0, 1)) = 1
        _Pulse ("Pulse (0-1)", Range(0, 1)) = 0
        _Dashed ("Dashed (0/1)", Float) = 0
        _Fade ("Fade (0-1)", Range(0, 1)) = 1
        _ColorLow ("Charging colour", Color) = (0.23, 0.23, 0.23, 1)
        _ColorFull ("Full colour", Color) = (0.27, 0.07, 1, 1)
        _BurstScale ("Burst scale", Float) = 1
        _BurstAlpha ("Burst alpha", Range(0, 1)) = 0
        _RingRadius ("Ring radius (UV)", Range(0, 0.5)) = 0.2857
        _Thickness ("Ring thickness (UV)", Range(0, 0.25)) = 0.0343
        _Track ("Unfilled track brightness", Range(0, 1)) = 0.35
        _Dashes ("Dashes around the ring", Float) = 24
        _ChargeGlow ("Glow while charging", Float) = 1
        _FullGlow ("Glow when full", Float) = 2.5
        _PulseGlow ("Extra glow at the pulse's peak", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Blend One One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Fill;
                float _Pulse;
                float _Dashed;
                float _Fade;
                half4 _ColorLow;
                half4 _ColorFull;
                float _BurstScale;
                float _BurstAlpha;
                float _RingRadius;
                float _Thickness;
                float _Track;
                float _Dashes;
                float _ChargeGlow;
                float _FullGlow;
                float _PulseGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // 1 inside a band of a half-width around a radius, softened by a pixel at its edges
            float Band(float r, float radius, float halfWidth, float aa)
            {
                return 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, abs(r - radius));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv - 0.5;
                float r = length(p);
                float aa = max(fwidth(r), 1e-5);
                float halfWidth = _Thickness * 0.5;

                // 0 at the nose, rising clockwise seen from above, to 1 all the way round
                float around = frac(atan2(p.x, p.y) / (2.0 * PI) + 1.0);
                bool full = _Fill >= 0.999;
                float filled = full ? 1.0 : step(around, _Fill);
                float dash = _Dashed > 0.5 ? step(frac(around * _Dashes), 0.6) : 1.0;

                float ring = Band(r, _RingRadius, halfWidth, aa);
                float lit = filled * dash;
                float glow = full ? _FullGlow * (1.0 + _Pulse * _PulseGlow) : _ChargeGlow;
                half3 colour = full ? _ColorFull.rgb : _ColorLow.rgb;
                half3 rgb = (colour * glow * lit + _ColorLow.rgb * _Track * (1.0 - lit)) * ring;

                // The spent boost: a thinner band flaring out
                float burst = Band(r, _RingRadius * _BurstScale, halfWidth * 0.7, aa) * _BurstAlpha;
                rgb += _ColorFull.rgb * _FullGlow * burst;

                // Added (Blend One One), leaving the view's alpha alone
                return half4(rgb * _Fade, 0);
            }
            ENDHLSL
        }
    }
}
