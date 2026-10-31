// Połysk jak "zaklęcie" na mieczu w Minecrafcie: po postaci przesuwają się ukośne pasy światła.
// Nakładany jest na kształt sprite'a (alfa), dodawany do tego, co pod spodem. Działa i na postaci w grze, i na obrazku w interfejsie.
Shader "BombGame/EnchantGlint"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _GlintColor ("Glint color", Color) = (0.7, 0.5, 1, 1)
        _Speed ("Speed", Float) = 0.5
        _Scale ("Stripe scale", Float) = 5
        _Rainbow ("Rainbow (0-1)", Float) = 0
        _Strength ("Strength", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _GlintColor;
            float _Speed;
            float _Scale;
            float _Rainbow;
            float _Strength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float t = _Time.y * _Speed;

                // dwie warstwy ukośnych pasów, przesuwane w przeciwne strony
                float a = dot(i.uv, float2(0.8, 0.6)) * _Scale + t;
                float b = dot(i.uv, float2(-0.55, 0.83)) * _Scale * 0.8 - t * 0.7;
                float band1 = smoothstep(0.78, 1.0, 0.5 + 0.5 * sin(a * 6.2831));
                float band2 = smoothstep(0.86, 1.0, 0.5 + 0.5 * sin(b * 6.2831));
                float haze = 0.14 * (0.5 + 0.5 * sin((a + b) * 3.1));
                float intensity = saturate(band1 + band2 * 0.7 + haze) * _Strength;

                float3 rainbow = 0.5 + 0.5 * cos(6.2831 * (float3(0.0, 0.33, 0.67) + i.uv.x * 0.6 + i.uv.y * 0.4 + t * 0.5));
                float3 color = lerp(_GlintColor.rgb, rainbow, _Rainbow);

                return fixed4(color, tex.a * intensity * i.color.a);
            }
            ENDCG
        }
    }
}
