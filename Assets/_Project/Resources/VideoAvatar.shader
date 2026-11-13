Shader "AICompanion/VideoAvatar"
{
    // Draws the 2D video avatar on a uGUI RawImage. The picture comes from _MainTex.
    // Transparency comes from the clip's own alpha, from a separate mask texture, or
    // from the right half of a packed clip. Output is premultiplied so the desktop
    // shows through the overlay window without a dark fringe.
    Properties
    {
        _MainTex ("Video", 2D) = "black" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _ColorRect ("Picture UV scale and offset", Vector) = (1, 1, 0, 0)
        _MaskRect ("Mask UV scale and offset", Vector) = (1, 1, 0, 0)
        _MaskSource ("0 clip alpha, 1 mask texture, 2 packed half", Float) = 0
        _MaskDecode ("1 undoes sRGB decoding of the mask", Float) = 0
        _MaskLow ("Mask value treated as fully clear", Float) = 0.06
        _MaskHigh ("Mask value treated as fully solid", Float) = 0.94
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        Pass
        {
            Name "VideoAvatar"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ColorRect;
                float4 _MaskRect;
                float _MaskSource;
                float _MaskDecode;
                float _MaskLow;
                float _MaskHigh;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 colorUv = input.uv * _ColorRect.xy + _ColorRect.zw;
                float2 maskUv = input.uv * _MaskRect.xy + _MaskRect.zw;

                half4 video = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, colorUv);
                half3 separate = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUv).rgb;
                half3 packed = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, maskUv).rgb;

                half3 matte = _MaskSource > 1.5 ? packed : separate;
                half luma = dot(matte, half3(0.299, 0.587, 0.114));
                if (_MaskDecode > 0.5)
                    luma = pow(max(luma, 0.0001), 1.0 / 2.2);
                // Video compression leaves black a little above 0 and white a little below 1.
                half fromMask = saturate((luma - _MaskLow) / max(_MaskHigh - _MaskLow, 0.0001));

                half alpha = _MaskSource > 0.5 ? fromMask : video.a;
                alpha = saturate(alpha * input.color.a);
                return half4(video.rgb * input.color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
