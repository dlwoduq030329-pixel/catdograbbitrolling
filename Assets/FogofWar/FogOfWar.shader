Shader "Hidden/Fog Of War"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
        }


        Pass
        {
            Name "Fog Of War"


            ZWrite Off
            ZTest Always
            Cull Off
            Blend One Zero


            HLSLPROGRAM


            #pragma vertex Vert
            #pragma fragment Frag


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"


            // ====================================================
            // Fog Texture
            // ====================================================

            TEXTURE2D(
                _FogTexture
            );

            SAMPLER(
                sampler_FogTexture
            );


            // ====================================================
            // PlayerMap
            //
            // x = World Min X
            // y = World Min Z
            // z = World Size X
            // w = World Size Z
            // ====================================================

            float4
                _MapWorldMinAndSize;


            // ====================================================
            // Current Player
            //
            // x = Player X
            // y = Player Z
            // z = Reveal Radius
            // w = unused
            // ====================================================

            float4
                _RevealWorldPositionAndRadius;


            float
                _FogOpacity;


            // ====================================================
            // FRAGMENT
            // ====================================================

            half4 Frag(
                Varyings input
            ) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(
                    input
                );


                // =================================================
                // 원본 화면
                // =================================================

                half4 sceneColor =
                    SAMPLE_TEXTURE2D_X(
                        _BlitTexture,
                        sampler_LinearClamp,
                        input.texcoord
                    );


                // =================================================
                // Screen UV
                // =================================================

                float2 screenUV =
                    input.positionCS.xy /
                    _ScaledScreenParams.xy;


                // =================================================
                // 실제 Scene Depth
                // =================================================

                float rawDepth =
                    SampleSceneDepth(
                        screenUV
                    );


                #if UNITY_REVERSED_Z

                    bool validDepth =
                        rawDepth > 0.00001;

                    float depth =
                        rawDepth;

                #else

                    bool validDepth =
                        rawDepth < 0.99999;

                    float depth =
                        lerp(
                            UNITY_NEAR_CLIP_VALUE,
                            1.0,
                            rawDepth
                        );

                #endif


                // Depth가 없는 배경은
                // Fog 계산을 하지 않습니다.
                if (!validDepth)
                {
                    return sceneColor;
                }


                // =================================================
                // ★ 실제 화면상의 World Position
                //
                // 카메라가 어디에 있든
                // 실제 렌더링된 물체의 위치를 가져옵니다.
                // =================================================

                float3 worldPosition =
                    ComputeWorldSpacePosition(
                        screenUV,
                        depth,
                        UNITY_MATRIX_I_VP
                    );


                // =================================================
                // PlayerMap UV
                // =================================================

                float2 fogUV =
                    (
                        worldPosition.xz -
                        _MapWorldMinAndSize.xy
                    )
                    /
                    _MapWorldMinAndSize.zw;


                // =================================================
                // 영구 Reveal
                //
                // PlayerMap 내부에서만 Texture 사용
                // =================================================

                float permanentReveal =
                    0.0;


                if (
                    fogUV.x >= 0.0 &&
                    fogUV.x <= 1.0 &&
                    fogUV.y >= 0.0 &&
                    fogUV.y <= 1.0
                )
                {
                    permanentReveal =
                        SAMPLE_TEXTURE2D(
                            _FogTexture,
                            sampler_FogTexture,
                            fogUV
                        ).r;
                }


                permanentReveal =
                    saturate(
                        permanentReveal
                    );


                // =================================================
                // 현재 플레이어 원형 Reveal
                //
                // ★ PlayerMap 경계와 무관
                //
                // ★ EMPTY 영역도 원 안이면 밝아짐
                //
                // ★ 카메라와 무관하게 월드 XZ 거리 사용
                // =================================================

                float2 playerXZ =
                    _RevealWorldPositionAndRadius.xy;


                float revealRadius =
                    max(
                        0.0,
                        _RevealWorldPositionAndRadius.z
                    );


                float currentReveal =
                    0.0;


                if (revealRadius > 0.0)
                {
                    float2 offset =
                        worldPosition.xz -
                        playerXZ;


                    float playerDistance =
                        length(
                            offset
                        );


                    // ------------------------------------------------
                    // 원의 중심부
                    // ------------------------------------------------

                    float innerRadius =
                        revealRadius *
                        0.82;


                    // ------------------------------------------------
                    // 원의 바깥 경계
                    // ------------------------------------------------

                    float outerRadius =
                        revealRadius;


                    currentReveal =
                        1.0 -
                        smoothstep(
                            innerRadius,
                            outerRadius,
                            playerDistance
                        );
                }


                // =================================================
                // Player 안전 영역
                //
                // 캐릭터의 몸통/발 위치가 약간 달라도
                // 플레이어가 Fog에 먹히지 않도록 합니다.
                //
                // revealRadius가 작더라도 최소 1m 정도 확보.
                // =================================================

                float playerSafeRadius =
                    max(
                        revealRadius,
                        1.0
                    );


                float safeDistance =
                    distance(
                        worldPosition.xz,
                        playerXZ
                    );


                float playerSafeReveal =
                    1.0 -
                    smoothstep(
                        playerSafeRadius * 0.65,
                        playerSafeRadius,
                        safeDistance
                    );


                // =================================================
                // 최종 Reveal
                //
                // 영구 Reveal
                // OR
                // 현재 Reveal
                // OR
                // Player 안전 영역
                // =================================================

                float reveal =
                    max(
                        permanentReveal,
                        max(
                            currentReveal,
                            playerSafeReveal
                        )
                    );


                reveal =
                    saturate(
                        reveal
                    );


                // =================================================
                // Fog Amount
                // =================================================

                float fogAmount =
                    (
                        1.0 -
                        reveal
                    )
                    *
                    _FogOpacity;


                fogAmount =
                    saturate(
                        fogAmount
                    );


                // =================================================
                // Fog Color
                // =================================================

            float3 fogColor =
    float3(
        0.15,
        0.15,
        0.15
    );


                // =================================================
                // Final
                // =================================================

                float3 finalColor =
                    lerp(
                        sceneColor.rgb,
                        fogColor,
                        fogAmount
                    );


                return half4(
                    finalColor,
                    sceneColor.a
                );
            }


            ENDHLSL
        }
    }
}