// JanusParticles.shader
// Rendu des corps Janus : points à taille fixe en pixels, mélange additif.
// Couleurs : étoiles anciennes (jaune pâle), gaz (rose discret), étoiles jeunes (bleu vif,
// s'éteignant avec l'âge), masses négatives (rouge), halo de matière noire (violet).
// À utiliser avec le mesh "Quad" de Unity.
Shader "Janus/Particles"
{
    Properties
    {
        _PointSize     ("Taille (pixels)", Float) = 3
        _Intensity     ("Intensité", Float) = 0.15
        _PosColor      ("Couleur étoiles anciennes", Color) = (1, 0.85, 0.6, 1)
        _GasColor      ("Couleur gaz", Color) = (1, 0.35, 0.55, 1)
        _GasIntensity  ("Intensité relative gaz", Float) = 0.6
        _YoungColor    ("Couleur étoiles jeunes", Color) = (0.45, 0.65, 1, 1)
        _YoungBoost    ("Éclat des étoiles jeunes", Float) = 5
        _NegColor      ("Couleur m-", Color) = (1, 0.25, 0.15, 1)
        _NegIntensity  ("Intensité relative m- / halo", Float) = 0.3
        _HaloColor     ("Couleur halo matière noire", Color) = (0.7, 0.4, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct Body
            {
                float3 pos;
                float  mass;   // étoile jeune : date de naissance
                float3 vel;
                int    sign;   // 1 étoile, -1 négative, 2 halo, 3 gaz, 4 étoile jeune
            };

            StructuredBuffer<Body> _Bodies;
            float  _PointSize, _Intensity, _NegIntensity, _GasIntensity, _YoungBoost;
            float  _SimTime, _YoungLifetime;
            float4 _PosColor, _NegColor, _HaloColor, _GasColor, _YoungColor;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 col : COLOR;
            };

            v2f vert(appdata v, uint iid : SV_InstanceID)
            {
                Body b = _Bodies[iid];
                v2f o;

                float4 clip   = mul(UNITY_MATRIX_VP, float4(b.pos, 1));
                float2 corner = v.vertex.xy * 2.0;            // quad Unity : ±0.5 -> ±1
                clip.xy += corner * _PointSize / _ScreenParams.xy * clip.w;

                float4 c = _PosColor;                                       // étoile ancienne
                if (b.sign < 0)  c = _NegColor  * _NegIntensity;            // masse négative
                if (b.sign == 2) c = _HaloColor * _NegIntensity;            // halo matière noire
                if (b.sign == 3) c = _GasColor  * _GasIntensity;            // gaz
                if (b.sign == 4)                                            // étoile jeune
                {
                    float age = saturate((_SimTime - b.mass) / max(_YoungLifetime, 1e-3));
                    float glow = 1.0 - age;
                    c = lerp(_PosColor, _YoungColor * _YoungBoost, glow * glow);
                }

                o.pos = clip;
                o.uv  = corner;
                o.col = c * _Intensity;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float a = saturate(1.0 - dot(i.uv, i.uv));    // disque doux
                return i.col * (a * a);
            }
            ENDCG
        }
    }
}
