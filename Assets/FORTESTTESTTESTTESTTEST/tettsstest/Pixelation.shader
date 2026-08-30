Shader "Hidden/Pixelation"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size", Range(1, 32)) = 4
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float4 _MainTex_TexelSize;
            float _PixelSize;

            fixed4 frag(v2f_img i) : SV_Target
            {
                // 현재 화면 해상도
                float2 resolution = _MainTex_TexelSize.zw;

                // 화면을 PixelSize 단위의 격자로 나눈다.
                float2 pixelCount = resolution / _PixelSize;

                // UV를 격자 단위로 잘라낸다.
                float2 pixelUV = floor(i.uv * pixelCount) / pixelCount;

                // 각 블록에서 동일한 위치의 픽셀을 가져온다.
                return tex2D(_MainTex, pixelUV);
            }

            ENDCG
        }
    }
}