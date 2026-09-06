using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FogOfWarRendererFeature : ScriptableRendererFeature
{
    // ============================================================
    // SETTINGS
    // ============================================================

    [Header("Fog")]
    [SerializeField]
    private Shader fogShader;


    [Header("Player")]
    [Tooltip(
        "Player Transform을 찾지 못했을 때 사용하는 Layer입니다. " +
        "일반적으로 비워둬도 됩니다."
    )]
    [SerializeField]
    private LayerMask fallbackPlayerLayer;


    // ============================================================
    // RUNTIME
    // ============================================================

    private Material fogMaterial;

    private FogPass fogPass;


    // ============================================================
    // CREATE
    // ============================================================

    public override void Create()
    {
        if (fogShader == null)
        {
            fogShader =
                Shader.Find(
                    "Hidden/Fog Of War"
                );
        }


        if (fogShader == null)
        {
            Debug.LogError(
                "[FogOfWar] Hidden/Fog Of War Shader를 찾을 수 없습니다."
            );

            return;
        }


        CoreUtils.Destroy(
            fogMaterial
        );


        fogMaterial =
            CoreUtils.CreateEngineMaterial(
                fogShader
            );


        fogPass =
            new FogPass(
                fogMaterial,
                fallbackPlayerLayer
            );


        fogPass.renderPassEvent =
            RenderPassEvent.BeforeRenderingPostProcessing;
    }


    // ============================================================
    // SETUP
    // ============================================================

    public override void SetupRenderPasses(
        ScriptableRenderer renderer,
        in RenderingData renderingData
    )
    {
        if (fogPass == null)
            return;


        if (renderingData.cameraData.cameraType !=
            CameraType.Game)
        {
            return;
        }


        fogPass.SetTarget(
            renderer.cameraColorTargetHandle
        );


        // --------------------------------------------------------
        // Manager가 찾은 Player의 실제 Layer를 자동 사용
        // --------------------------------------------------------

        if (FogOfWarManager.Instance != null &&
            FogOfWarManager.Instance.PlayerTransform != null)
        {
            int playerLayer =
                FogOfWarManager
                    .Instance
                    .PlayerTransform
                    .gameObject
                    .layer;


            fogPass.SetPlayerLayer(
                playerLayer
            );
        }
    }


    // ============================================================
    // ADD RENDER PASSES
    // ============================================================

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData
    )
    {
        if (fogPass == null)
            return;


        if (renderingData.cameraData.cameraType !=
            CameraType.Game)
        {
            return;
        }


        if (!FogOfWarManager.IsReady)
            return;


        renderer.EnqueuePass(
            fogPass
        );
    }


    // ============================================================
    // DISPOSE
    // ============================================================

    protected override void Dispose(
        bool disposing
    )
    {
        if (fogPass != null)
        {
            fogPass.Dispose();

            fogPass = null;
        }


        CoreUtils.Destroy(
            fogMaterial
        );


        fogMaterial = null;
    }


    // ============================================================
    // FOG PASS
    // ============================================================

    private sealed class FogPass :
        ScriptableRenderPass
    {
        private readonly Material material;

        private LayerMask playerLayer;

        private RTHandle cameraColorTarget;

        private RTHandle tempColorTarget;


        // --------------------------------------------------------
        // Shader Tags
        // --------------------------------------------------------

        private readonly List<ShaderTagId>
            shaderTags =
            new List<ShaderTagId>
            {
                new ShaderTagId(
                    "UniversalForward"
                ),

                new ShaderTagId(
                    "UniversalForwardOnly"
                ),

                new ShaderTagId(
                    "SRPDefaultUnlit"
                ),

                new ShaderTagId(
                    "LightweightForward"
                )
            };


        // ========================================================
        // CONSTRUCTOR
        // ========================================================

        public FogPass(
            Material material,
            LayerMask fallbackLayer
        )
        {
            this.material =
                material;

            this.playerLayer =
                fallbackLayer;


            renderPassEvent =
                RenderPassEvent.BeforeRenderingPostProcessing;


            // Shader가 Scene Depth를 사용하므로
            // Depth 입력을 요청합니다.
            ConfigureInput(
                ScriptableRenderPassInput.Depth
            );
        }


        // ========================================================
        // SET TARGET
        // ========================================================

        public void SetTarget(
            RTHandle target
        )
        {
            cameraColorTarget =
                target;
        }


        // ========================================================
        // SET PLAYER LAYER
        // ========================================================

        public void SetPlayerLayer(
            int layer
        )
        {
            if (layer < 0 ||
                layer > 31)
            {
                return;
            }


            playerLayer =
                1 << layer;
        }


        // ========================================================
        // CAMERA SETUP
        // ========================================================

        public override void OnCameraSetup(
            CommandBuffer cmd,
            ref RenderingData renderingData
        )
        {
            if (cameraColorTarget == null)
                return;


            RenderTextureDescriptor descriptor =
                renderingData
                    .cameraData
                    .cameraTargetDescriptor;


            descriptor.depthBufferBits = 0;

            descriptor.msaaSamples = 1;


            RenderingUtils.ReAllocateIfNeeded(
                ref tempColorTarget,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_FogOfWarTemp"
            );


            ConfigureTarget(
                tempColorTarget
            );


            ConfigureClear(
                ClearFlag.None,
                Color.clear
            );
        }


        // ========================================================
        // EXECUTE
        // ========================================================

        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData
        )
        {
            if (material == null)
                return;


            if (cameraColorTarget == null)
                return;


            if (tempColorTarget == null)
                return;


            CommandBuffer cmd =
                CommandBufferPool.Get(
                    "Fog Of War"
                );


            // ====================================================
            // 1. 현재 화면 → Fog Shader → Temp
            // ====================================================

            Blitter.BlitCameraTexture(
                cmd,
                cameraColorTarget,
                tempColorTarget,
                material,
                0
            );


            // ====================================================
            // 2. Temp → Camera
            // ====================================================

            Blitter.BlitCameraTexture(
                cmd,
                tempColorTarget,
                cameraColorTarget
            );


            context.ExecuteCommandBuffer(
                cmd
            );


            cmd.Clear();


            // ====================================================
            // 3. PLAYER 다시 렌더링
            //
            // Fog가 플레이어를 덮은 뒤
            // Player Layer만 다시 그립니다.
            // ====================================================

            if (playerLayer.value != 0)
            {
                // ------------------------------------------------
                // Opaque Player
                // ------------------------------------------------

                DrawingSettings opaqueDrawing =
                    CreateDrawingSettings(
                        shaderTags,
                        ref renderingData,
                        SortingCriteria.CommonOpaque
                    );


                FilteringSettings opaqueFiltering =
                    new FilteringSettings(
                        RenderQueueRange.opaque,
                        playerLayer
                    );


                context.DrawRenderers(
                    renderingData.cullResults,
                    ref opaqueDrawing,
                    ref opaqueFiltering
                );


                // ------------------------------------------------
                // Transparent Player
                // ------------------------------------------------

                DrawingSettings transparentDrawing =
                    CreateDrawingSettings(
                        shaderTags,
                        ref renderingData,
                        SortingCriteria.CommonTransparent
                    );


                FilteringSettings transparentFiltering =
                    new FilteringSettings(
                        RenderQueueRange.transparent,
                        playerLayer
                    );


                context.DrawRenderers(
                    renderingData.cullResults,
                    ref transparentDrawing,
                    ref transparentFiltering
                );
            }


            context.ExecuteCommandBuffer(
                cmd
            );


            CommandBufferPool.Release(
                cmd
            );
        }


        // ========================================================
        // DISPOSE
        // ========================================================

        public void Dispose()
        {
            if (tempColorTarget != null)
            {
                tempColorTarget.Release();

                tempColorTarget = null;
            }
        }
    }
}