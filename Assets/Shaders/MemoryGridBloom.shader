Shader "Hidden/ACG/MemoryGridBloom"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
        struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
        sampler2D _MainTex;
        sampler2D _BloomTex;
        float4 _MainTex_TexelSize;
        float _Threshold;
        float _Intensity;
        float2 _BlurDirection;

        v2f vert(appdata input)
        {
            v2f output;
            output.vertex = UnityObjectToClipPos(input.vertex);
            output.uv = input.uv;
            return output;
        }

        fixed4 fragPrefilter(v2f input) : SV_Target
        {
            half4 color = tex2D(_MainTex, input.uv);
            half brightness = max(color.r, max(color.g, color.b));
            half contribution = max(0.0h, brightness - _Threshold) / max(brightness, 0.0001h);
            return half4(color.rgb * contribution, 1.0h);
        }

        fixed4 fragBlur(v2f input) : SV_Target
        {
            float2 offset = _MainTex_TexelSize.xy * _BlurDirection;
            half3 color = tex2D(_MainTex, input.uv).rgb * .227027h;
            color += tex2D(_MainTex, input.uv + offset * 1.384615).rgb * .316216h;
            color += tex2D(_MainTex, input.uv - offset * 1.384615).rgb * .316216h;
            color += tex2D(_MainTex, input.uv + offset * 3.230769).rgb * .070270h;
            color += tex2D(_MainTex, input.uv - offset * 3.230769).rgb * .070270h;
            return half4(color, 1.0h);
        }

        fixed4 fragComposite(v2f input) : SV_Target
        {
            half4 source = tex2D(_MainTex, input.uv);
            half3 bloom = tex2D(_BloomTex, input.uv).rgb;
            return half4(source.rgb + bloom * _Intensity, source.a);
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDCG
        }
    }
    Fallback Off
}
