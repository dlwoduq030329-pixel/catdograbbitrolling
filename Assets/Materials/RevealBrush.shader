Shader "Hidden/RevealBrush"
{
    Properties
    {
        _MainTex ("Previous Reveal", 2D) = "black" {}

        _RevealCenter ("Reveal Center", Vector) =
            (0.5, 0.5, 0, 0)

        _RevealRadius ("Reveal Radius", Float) =
            5.0

        _FadeDistance ("Fade Distance", Float) =
            2.0

        _WorldSize ("World Size", Vector) =
            (180, 180, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Overlay"
        }

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"


            // =====================================================
            // 기존 Reveal Map
            // =====================================================

            sampler2D _MainTex;


            // =====================================================
            // 현재 Player의 Reveal 위치
            // =====================================================

            float4 _RevealCenter;


            // =====================================================
            // 실제 월드 단위
            // =====================================================

            float _RevealRadius;

            float _FadeDistance;


            // =====================================================
            // Reveal Map이 나타내는 실제 월드 크기
            //
            // X = World X 크기
            // Y = World Z 크기
            // =====================================================

            float4 _WorldSize;


            // =====================================================
            // Vertex
            // =====================================================

            struct appdata
            {
                float4 vertex : POSITION;

                float2 uv : TEXCOORD0;
            };


            struct v2f
            {
                float4 vertex : SV_POSITION;

                float2 uv : TEXCOORD0;
            };


            v2f vert(
                appdata v
            )
            {
                v2f o;


                o.vertex =
                    UnityObjectToClipPos(
                        v.vertex
                    );


                o.uv =
                    v.uv;


                return o;
            }


            // =====================================================
            // Fragment
            // =====================================================

            float4 frag(
                v2f i
            ) : SV_Target
            {
                // -------------------------------------------------
                // 기존에 밝혀진 값
                //
                // 0 = 안 밝혀짐
                // 1 = 밝혀짐
                // -------------------------------------------------

                float previousReveal =
                    tex2D(
                        _MainTex,
                        i.uv
                    ).r;


                // -------------------------------------------------
                // 현재 위치와의 UV 차이
                // -------------------------------------------------

                float2 uvDelta =
                    i.uv
                    -
                    _RevealCenter.xy;


                // -------------------------------------------------
                // UV 차이를 실제 월드 거리로 변환
                //
                // X → World X
                // Y → World Z
                // -------------------------------------------------

                float2 worldDelta =
                    uvDelta
                    *
                    _WorldSize.xy;


                // -------------------------------------------------
                // 실제 월드 거리
                // -------------------------------------------------

                float distanceFromCenter =
                    length(
                        worldDelta
                    );


                // -------------------------------------------------
                // 현재 Player 주변 Reveal
                //
                // Radius 안쪽 = 1
                //
                // Radius ~ Radius+Fade
                // = 부드럽게 1 → 0
                //
                // 그 바깥 = 0
                // -------------------------------------------------

                float currentReveal =
                    1.0
                    -
                    smoothstep(
                        _RevealRadius,
                        _RevealRadius + _FadeDistance,
                        distanceFromCenter
                    );


                // -------------------------------------------------
                // 핵심
                //
                // 기존에 밝혀진 값과 현재 Reveal 중
                // 더 큰 값을 사용
                //
                // 따라서:
                //
                // 한번 밝혀진 영역
                // → 다시 어두워지지 않음
                // -------------------------------------------------

                float finalReveal =
                    max(
                        previousReveal,
                        currentReveal
                    );


                return float4(
                    finalReveal,
                    finalReveal,
                    finalReveal,
                    1.0
                );
            }

            ENDHLSL
        }
    }
}