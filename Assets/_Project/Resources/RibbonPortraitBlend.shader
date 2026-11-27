Shader "Happi/UI/RibbonPortraitBlend"
{
    Properties
    {
        [PerRendererData] _MainTex ("First frame", 2D) = "white" {}
        _NextTex ("Next frame", 2D) = "white" {}
        _FirstRect ("First frame bounds", Vector) = (0, 0, 1, 1)
        _NextRect ("Next frame bounds", Vector) = (0, 0, 1, 1)
        _Weights ("Frame weights", Vector) = (1, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NextTex;
            float4 _FirstRect;
            float4 _NextRect;
            half2 _Weights;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : COLOR0;
            };

            Varyings vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.alpha = input.color.a;
                return output;
            }

            half4 WeightedFrame(sampler2D source, float2 uv, float4 bounds, half weight)
            {
                float2 at = (uv - bounds.xy) / bounds.zw;
                float2 inside = step(0, at) * step(at, 1);
                half4 color = tex2D(source, saturate(at));
                half alpha = weight * color.a * inside.x * inside.y;
                return half4(color.rgb * alpha, alpha);
            }

            half4 frag(Varyings input) : SV_Target
            {
                return (WeightedFrame(_MainTex, input.uv, _FirstRect, _Weights.x)
                    + WeightedFrame(_NextTex, input.uv, _NextRect, _Weights.y)) * input.alpha;
            }
            ENDCG
        }
    }
}
