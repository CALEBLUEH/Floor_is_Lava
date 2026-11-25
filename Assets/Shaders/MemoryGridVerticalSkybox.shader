Shader "ACG/Skybox Vertical Cubemap"
{
    Properties
    {
        [NoScaleOffset] _Tex ("Cubemap", Cube) = "grey" {}
        _Tint ("Tint", Color) = (.5, .5, .5, .5)
        _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Horizontal Rotation", Range(0, 360)) = 0
        _VerticalRotation ("Vertical Rotation", Range(-80, 80)) = 24
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _Tex;
            half4 _Tint;
            half4 _Tex_HDR;
            half _Exposure;
            float _Rotation;
            float _VerticalRotation;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; };

            float3 RotateX(float3 value, float degrees)
            {
                float angle = radians(degrees);
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float3(value.x, value.y * cosine - value.z * sine,
                    value.y * sine + value.z * cosine);
            }

            float3 RotateY(float3 value, float degrees)
            {
                float angle = radians(degrees);
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float3(value.x * cosine - value.z * sine, value.y,
                    value.x * sine + value.z * cosine);
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.direction = RotateX(RotateY(input.vertex.xyz, _Rotation), _VerticalRotation);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half4 encoded = texCUBE(_Tex, normalize(input.direction));
                half3 color = DecodeHDR(encoded, _Tex_HDR);
                color *= _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                return half4(color, 1.0h);
            }
            ENDCG
        }
    }
    Fallback Off
}
