Shader "ACG/Neon Glow Shell"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.25, 8)) = 2
        _Alpha ("Glow Alpha", Range(0, 1)) = 0.32
    }

    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
            };

            float4 _GlowColor;
            float _RimPower;
            float _Alpha;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 normal = normalize(input.worldNormal);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);
                float rim = pow(1.0 - saturate(dot(normal, viewDirection)), _RimPower);
                return fixed4(_GlowColor.rgb * (0.35 + rim), saturate(_Alpha * (0.35 + rim)));
            }
            ENDCG
        }
    }

    Fallback Off
}
