// Wzór chromy na postaci: półprzezroczyste ikonki (listki, płomyki, serduszka...) zsuwają się w dół po sylwetce.
// Rysowany jest jako druga warstwa z tym samym sprite'em: kształt bierze z alfy sprite'a, a obrazek z kafelka wzoru
// (_PatternTex, powtarzalny, z pomnożoną alfą), który składa ChromaFx.Pattern. Dwie warstwy wzoru o różnej wielkości
// i prędkości dają wrażenie głębi. Działa i na postaci w grze, i na obrazku w interfejsie.
Shader "BombGame/EnchantGlint"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _PatternTex ("Pattern tile", 2D) = "black" {}
        _UvRect ("Sprite UV rect (x, y, w, h)", Vector) = (0, 0, 1, 1)
        _Aspect ("Sprite width / height", Float) = 1
        _Tiles ("Pattern tiles per sprite height", Float) = 1.8
        _Speed ("Fall speed (tiles per second)", Float) = 0.22
        _Rainbow ("Rainbow (0-1)", Float) = 0
        _Strength ("Opacity", Float) = 0.62
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float2 puv : TEXCOORD1;
                float4 localPosition : TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _PatternTex;
            float4 _UvRect;
            float _Aspect;
            float _Tiles;
            float _Speed;
            float _Rainbow;
            float _Strength;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPosition = v.vertex;
                o.uv = v.uv;
                o.color = v.color;

                // położenie na sprite'cie (0..1), przeliczone na kafelki wzoru; wzór jest przyklejony do postaci
                float2 local = (v.uv - _UvRect.xy) / max(_UvRect.zw, 0.0001);
                o.puv = float2(local.x * _Aspect, local.y) * _Tiles;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float t = _Time.y;

                // bliższa warstwa: większe ikonki, spadają szybciej i lekko kołyszą się na boki
                float2 uv1 = i.puv;
                uv1.y += t * _Speed;
                uv1.x += sin(t * 1.7 + i.puv.y * 3.0) * 0.035;

                // dalsza warstwa: mniejsze, bledsze, wolniejsze
                float2 uv2 = i.puv * 1.7 + float2(0.37, 0.61);
                uv2.y += t * _Speed * 0.6;
                uv2.x -= sin(t * 1.3 + i.puv.y * 2.2) * 0.03;

                fixed4 frontLayer = tex2D(_PatternTex, uv1);
                fixed4 backLayer = tex2D(_PatternTex, uv2) * 0.55;
                fixed4 pattern = frontLayer + backLayer * (1.0 - frontLayer.a);

                float3 rainbow = 0.5 + 0.5 * cos(6.2831 * (float3(0.0, 0.33, 0.67) + i.puv.x * 0.35 + i.puv.y * 0.25 + t * 0.25));
                pattern.rgb *= lerp(float3(1.0, 1.0, 1.0), 0.55 + 0.45 * rainbow, _Rainbow);

                // tylko tam, gdzie jest postać
                float mask = tex.a * i.color.a * _Strength;

                // w przewijanej liście (interfejs) wzór jest przycinany razem z kartą
                #ifdef UNITY_UI_CLIP_RECT
                mask *= UnityGet2DClipping(i.localPosition.xy, _ClipRect);
                #endif

                return pattern * mask;
            }
            ENDCG
        }
    }
}
