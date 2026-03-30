Shader "Custom/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                float2 size = _MainTex_TexelSize.xy * _OutlineWidth;
                
                // 주변 4방향 알파값 체크
                float alpha = col.a;
                alpha += tex2D(_MainTex, i.uv + float2(size.x, 0)).a;
                alpha += tex2D(_MainTex, i.uv - float2(size.x, 0)).a;
                alpha += tex2D(_MainTex, i.uv + float2(0, size.y)).a;
                alpha += tex2D(_MainTex, i.uv - float2(0, size.y)).a;

                if (col.a <= 0.1 && alpha > 0.1) return _OutlineColor;
                return col;
            }
            ENDCG
        }
    }
}