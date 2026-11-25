// The boost ring under a scooter (BoostRing): unlit and see-through, drawn on a flat quad. The band is the UV distance
// from the quad's centre, and the fill runs clockwise from the scooter's nose (+V) by angle, up to _Fill. The rest of
// the band is the track: dim, but kept above _TrackFloor so an empty ring still reads. _Dashed breaks the filled part
// into dashes (a slowed recharge, carrying a package). The colour is _ColorLow while charging (the horns' colour now)
// and _ColorFull when full, brightened by _Pulse. A spent boost draws a second band at _BurstScale times the radius,
// faded by _BurstAlpha. _Fade hides it all while the scooter is in the air.
// Premultiplied (Blend One OneMinusSrcAlpha): the colour is added, an HDR glow for the bloom, while the alpha darkens a
// band a little wider than the ring (_Backing, _Outline), so it stands out on bright pavement as well as at night.
// No depth write and a normal depth test, but its depth is pulled _DepthBias metres towards the camera (not its place
// on screen): low clutter in front of it (grass, kerbs, bricks) can't hide it, while a wall or a car still does.
// Plain HLSL, nothing platform-specific (D3D11, Metal, Vulkan), and no depth texture needed.
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
        _Thickness ("Ring thickness (UV)", Range(0, 0.25)) = 0.0686
        _Track ("Unfilled track brightness", Range(0, 1)) = 0.6
        _TrackFloor ("Unfilled track's least brightness", Range(0, 1)) = 0.12
        _Backing ("Dark backing's strength", Range(0, 1)) = 0.6
        _Outline ("Dark outline, as a share of the band's half-width", Range(0, 1)) = 0.6
        _DepthBias ("Depth pulled towards the camera (m)", Range(0, 1)) = 0.3
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
            Blend One OneMinusSrcAlpha, Zero One
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
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
                float _TrackFloor;
                float _Backing;
                float _Outline;
                float _DepthBias;
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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);

                // The same point moved towards the camera (view space looks down -z), kept past the near plane: only
                // its depth is used, so the ring stays where it is on screen
                float3 positionVS = TransformWorldToView(positionWS);
                positionVS.z = min(positionVS.z + _DepthBias, -_ProjectionParams.y * 1.05);
                float4 nearerCS = TransformWViewToHClip(positionVS);
                positionCS.z = nearerCS.z / nearerCS.w * positionCS.w;

                output.positionCS = positionCS;
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
                float backing = Band(r, _RingRadius, halfWidth * (1.0 + _Outline), aa) * _Backing;
                float lit = filled * dash;
                float glow = full ? _FullGlow * (1.0 + _Pulse * _PulseGlow) : _ChargeGlow;
                half3 colour = full ? _ColorFull.rgb : _ColorLow.rgb;
                half3 track = max(_ColorLow.rgb * _Track, (half3)_TrackFloor);
                half3 rgb = (colour * glow * lit + track * (1.0 - lit)) * ring;

                // The spent boost: a thinner band flaring out, glow only
                float burst = Band(r, _RingRadius * _BurstScale, halfWidth * 0.7, aa) * _BurstAlpha;
                rgb += _ColorFull.rgb * _FullGlow * burst;

                // Premultiplied: the colour added, the backing darkening what is behind; the view's alpha left alone
                return half4(rgb * _Fade, backing * _Fade);
            }
            ENDHLSL
        }
    }
}
