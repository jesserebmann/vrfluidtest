#if UNITY_2019_2_OR_NEWER && SRP_UNIVERSAL
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Obi
{

    public class ObiFluidRendererFeature : ScriptableRendererFeature
    {
        public ObiFluidRenderingPass[] passes;

        [Range(1, 4)]
        public int thicknessDownsample = 1;

        [Min(0)]
        public float foamFadeDepth = 1.0f;

        private VolumePass m_VolumePass;

        public override void Create()
        {
            if (passes == null)
                return;

            m_VolumePass = new VolumePass();
            m_VolumePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (passes == null)
                {
                    Debug.LogError("passes is null");
                    return;
                }
            
                if (renderer == null)
                {
                    Debug.LogError("renderer is null");
                    return;
                }
            if (passes == null)
                return;

            m_VolumePass.Setup(passes, thicknessDownsample, foamFadeDepth);

            // request camera  depth and color buffers.
            m_VolumePass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);

            renderer.EnqueuePass(m_VolumePass);
        }

        protected override void Dispose(bool disposing)
        {
            m_VolumePass.Dispose();
        }

    }

    public class VolumePass : ScriptableRenderPass
    {
        const string k_ThicknessPassTag = "FluidThicknessPass";
        private ProfilingSampler m_Thickness_Profile = new ProfilingSampler(k_ThicknessPassTag);

        private ObiFluidRenderingPass[] passes;
        private int thicknessDownsample;
        private float foamFadeDepth;

        private Material m_TransmissionMaterial;

        private RTHandle m_TransmissionHandle;
        private RTHandle m_FoamHandle;
        private RTHandle m_SurfHandle;
        private RTHandle m_DepthHandle;

        protected Material CreateMaterial(Shader shader)
        {
            if (!shader || !shader.isSupported)
                return null;
            Material m = new Material(shader);
            m.hideFlags = HideFlags.HideAndDontSave;
            return m;
        }

        public void Setup(ObiFluidRenderingPass[] passes, int thicknessDownsample, float foamFadeDepth)
        {
            // Copy settings;
            this.passes = passes;
            this.thicknessDownsample = thicknessDownsample;
            this.foamFadeDepth = foamFadeDepth;

            m_TransmissionMaterial = CreateMaterial(Shader.Find("Hidden/AccumulateTransmission"));

            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                Debug.LogWarning("Obi Fluid Renderer Feature not supported in this platform.");
                return;
            }
        }

        private Vector2Int ScaleRTs(Vector2Int size)
        {
            return size / thicknessDownsample;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            var camSize = new Vector2Int(desc.width, desc.height);

            if (m_SurfHandle == null || camSize != m_SurfHandle.referenceSize)
            {
                m_TransmissionHandle?.Release();
                m_FoamHandle?.Release();
                m_DepthHandle?.Release();
                m_SurfHandle?.Release();
                m_TransmissionHandle = RTHandles.Alloc(desc.width / thicknessDownsample, desc.height / thicknessDownsample, name: "_FluidThickness", colorFormat:UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);
                m_SurfHandle = RTHandles.Alloc(desc.width, desc.height, name: "_TemporaryBuffer", colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);
                m_DepthHandle = RTHandles.Alloc(desc.width, desc.height, name: "_TemporaryBufferDepth", depthBufferBits: DepthBits.Depth16, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);
                m_FoamHandle = RTHandles.Alloc(desc.width, desc.height, name: "_Foam", colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm);
            }

            ConfigureTarget(m_TransmissionHandle);
            ConfigureClear(ClearFlag.All, FluidRenderingUtils.transmissionBufferClear);

            ConfigureTarget(m_SurfHandle);
            ConfigureClear(ClearFlag.All, FluidRenderingUtils.thicknessBufferClear);
        }

        public void Dispose()
        {
            m_TransmissionHandle?.Release();
            m_SurfHandle?.Release();
            m_DepthHandle?.Release();
            m_FoamHandle?.Release();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_ThicknessPassTag);
            using (new ProfilingScope(cmd, m_Thickness_Profile))
            {
                cmd.SetGlobalTexture("_TemporaryBuffer", m_SurfHandle);

                cmd.SetRenderTarget(m_TransmissionHandle);
                cmd.ClearRenderTarget(false, true, FluidRenderingUtils.transmissionBufferClear);

                // render each pass (there's only one mesh per pass) onto temp buffer to calculate its color and thickness.
                for (int i = 0; i < passes.Length; ++i)
                {
                    if (passes[i] != null && passes[i].renderers.Count > 0)
                    {
                        var fluidMesher = passes[i].renderers[0];
                        if (fluidMesher.actor.isLoaded)
                        {
                            cmd.SetRenderTarget(m_SurfHandle, m_DepthHandle);
                            cmd.ClearRenderTarget(false, true, FluidRenderingUtils.thicknessBufferClear);

                            // fluid mesh renders absorption color and thickness onto temp buffer:
                            var renderSystem = fluidMesher.actor.solver.GetRenderSystem<ObiFluidSurfaceMesher>() as IFluidRenderSystem;
                            if (renderSystem != null)
                                renderSystem.RenderVolume(cmd, passes[i], fluidMesher);

                            // calculate transmission from thickness & absorption and accumulate onto transmission buffer.
                            cmd.SetGlobalFloat("_Thickness", passes[i].thickness);
                            cmd.Blit(m_SurfHandle, m_TransmissionHandle, m_TransmissionMaterial,0);
                        }
                    }
                }

                // get temporary buffer with depth support, render fluid surface depth:
                cmd.SetRenderTarget(m_SurfHandle, m_DepthHandle);
                cmd.ClearRenderTarget(true, true, Color.clear);
                for (int i = 0; i < passes.Length; ++i)
                {
                    if (passes[i] != null && passes[i].renderers.Count > 0)
                    {
                        var fluidMesher = passes[i].renderers[0];
                        if (fluidMesher.actor.isLoaded)
                        {
                            // fluid mesh renders surface onto surface buffer
                            var renderSystem = fluidMesher.actor.solver.GetRenderSystem<ObiFluidSurfaceMesher>() as IFluidRenderSystem;
                            if (renderSystem != null)
                                renderSystem.RenderSurface(cmd, passes[i], fluidMesher);
                        }
                    }
                }

                // render foam, using distance to surface depth to modulate alpha:
                cmd.SetRenderTarget(m_FoamHandle);
                cmd.ClearRenderTarget(false, true, Color.clear);
                for (int i = 0; i < passes.Length; ++i)
                {
                    for (int j = 0; j < passes[i].renderers.Count; ++j)
                    {
                        if (passes[i].renderers[j].TryGetComponent(out ObiFoamGenerator foamGenerator))
                        {
                            var solver = passes[i].renderers[j].actor.solver;
                            var rend = solver.GetRenderSystem<ObiFoamGenerator>() as ObiFoamRenderSystem;

                            if (rend != null)
                            {
                                rend.renderBatch.material.SetFloat("_FadeDepth", foamFadeDepth);
                                rend.renderBatch.material.SetFloat("_VelocityStretching", solver.maxFoamVelocityStretch);
                                rend.renderBatch.material.SetFloat("_FadeIn", solver.foamFade.x);
                                rend.renderBatch.material.SetFloat("_FadeOut", solver.foamFade.y);
                                cmd.DrawMesh(rend.renderBatch.mesh, solver.transform.localToWorldMatrix, rend.renderBatch.material);
                            }
                        }
                    }
                }
            }

            cmd.SetGlobalTexture("_FluidThickness", m_TransmissionHandle);
            cmd.SetGlobalTexture("_Foam", m_FoamHandle);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);

        }
    }


}


#endif
