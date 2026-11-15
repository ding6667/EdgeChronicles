// ============================================================================
// ECOutlineRendererFeature.cs — URP 14 描边 Renderer Feature 参考实现
// ----------------------------------------------------------------------------
// 归属：美术（art-director）产出规格与参考代码；**工程侧拷贝进工程并落地**。
//   工程侧目标路径：Assets/_Project/Scripts/Rendering/ECOutlineRendererFeature.cs
//   （属 engineering-lead 独占路径，由工程侧自行拷入，本文件不写入该路径）
//
// 【URP 14 专用写法】
//   · 没有 RenderGraph：全部走经典 ScriptableRenderPass
//     （OnCameraSetup / Execute / OnCameraCleanup）
//   · RTHandle 用 RenderingUtils.ReAllocateIfNeeded（URP 12+ 存在）
//   · 全屏拷贝/合成用 Blitter.BlitCameraTexture（URP 13+ 存在）
//   · 自定义 LightMode 用 DrawingSettings.SetShaderPassName 指定
//   · cameraColorTarget / cameraDepthTarget 只能在 render pass 内部访问
//     （OnCameraSetup / Execute），不能在 AddRenderPasses 里取
//   · URP 14 没有 ScriptableRenderPass.requiresIntermediateTexture
//     -> 颜色副本必须自己分配 RT
//
// 【前置条件（工程侧必须配好，否则本 Feature 不工作）】
//   1. URP Asset：
//        Depth Texture          = ON
//        Opaque Texture         = ON
//        MSAA                   = Disabled（理由见 docs/art/outline-urp14.md §7）
//        Intermediate Texture   = Always（相机颜色/深度必须是 RTHandle）
//   2. TagManager 新建层：EC_Outline（Layer 6 或任意空闲层）
//   3. 所有需要描边的角色 Renderer 挂到 EC_Outline 层
//   4. URP Asset -> Always Included Shaders 追加：
//        EC/OutlineMask
//        EC/OutlineDilate
//        EC/OutlineComposite
//      （否则打包时 Shader.Find 失败，运行时材质为 null，本 Feature 直接跳过）
//
// 【不新增材质槽】
//   本 Feature 运行时用 CoreUtils.CreateEngineMaterial 创建 2 个内部材质，
//   工程内**不存在**对应 .mat 资产，也不占用任何模型材质槽。
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EdgeChronicles.Rendering
{
    /// <summary>
    /// 描边渲染底座。4 个 Pass：
    ///   1. ECOutlineMaskPass      全分辨率遮罩（角色覆盖 + 视法线 + 状态编码）
    ///   2. ECOutlineDownsamplePass 全分辨率遮罩 -> 半分辨率 enc
    ///   3. ECOutlineDilatePass    半分辨率 4 档形态学
    ///   4. ECOutlineCompositePass 全分辨率合成 + 写回相机颜色
    /// </summary>
    public class ECOutlineRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("需要描边的 GameObject Layer（EC_Outline 层）")]
            public LayerMask outlineLayerMask = 0;

            [Tooltip("形态学链相对相机分辨率的缩小倍数。2 = 半分辨率（960x540@1080p）")]
            [Range(1, 4)] public int chainDownscale = 2;

            [Tooltip("质量档：0 = 仅轮廓；1 = 轮廓 + 角色内部结构线")]
            [Range(0, 1)] public int quality = 1;

            [Tooltip("全局反相压边（GDD §8.1）")]
            public bool pressEdgeEnabled = true;

            [Tooltip("墨线与背景的最小亮度差。0.60 = 60%（美术规格硬值）")]
            [Range(0.3f, 0.9f)] public float inkMinLumaDelta = 0.6f;

            [Tooltip("墨线/描边宽度随分辨率缩放的基准高度")]
            public int pixelScaleBaseHeight = 1080;
        }

        public Settings settings = new Settings();

        private Material _dilateMaterial;
        private Material _compositeMaterial;

        private ECOutlineMaskPass _maskPass;
        private ECOutlineDilatePass _dilatePass;
        private ECOutlineCompositePass _compositePass;

        /// <summary>4 个 Pass 之间共享的 RT 句柄（由本 Feature 拥有）。</summary>
        private class SharedRT
        {
            public RTHandle Mask;   // 全分辨率遮罩
            public RTHandle Enc;    // 半分辨率 enc
            public RTHandle Dil;    // 半分辨率形态学
            public RTHandle Temp;   // 全分辨率相机颜色副本
        }

        private readonly SharedRT _rt = new SharedRT();

        private static readonly ShaderTagId kMaskTag = new ShaderTagId("ECOutlineMask");

        public override void Create()
        {
            _dilateMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("EC/OutlineDilate"));
            _compositeMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("EC/OutlineComposite"));

            _maskPass = new ECOutlineMaskPass(kMaskTag, settings, _rt);
            _dilatePass = new ECOutlineDilatePass(_dilateMaterial, settings, _rt);
            _compositePass = new ECOutlineCompositePass(_compositeMaterial, settings, _rt);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_dilateMaterial == null || _compositeMaterial == null)
                return;   // Shader 未被 Always Included Shaders 收录 -> 静默跳过，不报错

            CameraData cameraData = renderingData.cameraData;
            if (cameraData.cameraType == CameraType.Preview ||
                cameraData.cameraType == CameraType.Reflection ||
                cameraData.isSceneViewCamera)
                return;

            _maskPass.Setup(settings);
            _dilatePass.Setup(settings);
            _compositePass.Setup(settings);

            renderer.EnqueuePass(_maskPass);
            renderer.EnqueuePass(_dilatePass);
            renderer.EnqueuePass(_compositePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_dilateMaterial);
            CoreUtils.Destroy(_compositeMaterial);
            _dilateMaterial = null;
            _compositeMaterial = null;
        }

        // ====================================================================
        // Pass 1：遮罩
        // ====================================================================
        private class ECOutlineMaskPass : ScriptableRenderPass
        {
            private static readonly ProfilingSampler s_Sampler = new ProfilingSampler("EC Outline Mask");

            private readonly ShaderTagId _shaderTagId;
            private readonly List<ShaderTagId> _shaderTagIdList;
            private readonly SharedRT _rt;
            private Settings _settings;

            public ECOutlineMaskPass(ShaderTagId shaderTagId, Settings settings, SharedRT rt)
            {
                _shaderTagId = shaderTagId;
                _shaderTagIdList = new List<ShaderTagId> { shaderTagId };
                _settings = settings;
                _rt = rt;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            }

            public void Setup(Settings settings)
            {
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;      // 与 URP Asset 的 MSAA = Disabled 对齐
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;

                _rt.Mask = RenderingUtils.ReAllocateIfNeeded(ref _rt.Mask, desc,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ECOutlineMask");

                // 关键：把相机的深度附件挂进来，轮廓才会被墙体正确遮挡
                RTHandle cameraDepth = renderingData.cameraData.renderer.cameraDepthTarget;
                ConfigureTarget(_rt.Mask, cameraDepth);
                ConfigureClear(ClearFlag.Color, Color.clear);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_settings.outlineLayerMask.value == 0)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, s_Sampler))
                {
                    DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIdList,
                        ref renderingData, SortingCriteria.CommonOpaque);
                    drawingSettings.SetShaderPassName(0, _shaderTagId);

                    FilteringSettings filteringSettings = new FilteringSettings(
                        RenderQueueRange.all, _settings.outlineLayerMask);

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        // ====================================================================
        // Pass 2 + 3：下采样 + 形态学链
        // ====================================================================
        private class ECOutlineDilatePass : ScriptableRenderPass
        {
            private static readonly ProfilingSampler s_Sampler = new ProfilingSampler("EC Outline Dilate");

            private readonly Material _material;
            private readonly SharedRT _rt;
            private Settings _settings;

            public ECOutlineDilatePass(Material material, Settings settings, SharedRT rt)
            {
                _material = material;
                _settings = settings;
                _rt = rt;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            }

            public void Setup(Settings settings)
            {
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var cameraDesc = renderingData.cameraData.cameraTargetDescriptor;

                var desc = cameraDesc;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.width = Mathf.Max(1, cameraDesc.width / _settings.chainDownscale);
                desc.height = Mathf.Max(1, cameraDesc.height / _settings.chainDownscale);
                _rt.Dil = RenderingUtils.ReAllocateIfNeeded(ref _rt.Dil, desc,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ECOutlineDil");

                var encDesc = desc;
                encDesc.graphicsFormat = GraphicsFormat.R8_UNorm;
                _rt.Enc = RenderingUtils.ReAllocateIfNeeded(ref _rt.Enc, encDesc,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ECOutlineEnc");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, s_Sampler))
                {
                    CameraData cameraData = renderingData.cameraData;

                    cmd.SetGlobalTexture("_ECOutlineMask", _rt.Mask);
                    cmd.SetGlobalVector("_ECOutlineMaskSize", new Vector4(
                        cameraDesc(cameraData).width, cameraDesc(cameraData).height,
                        1f / cameraDesc(cameraData).width, 1f / cameraDesc(cameraData).height));
                    cmd.SetGlobalVector("_ECOutlineChainSize", new Vector4(
                        _rt.Dil.rt.width, _rt.Dil.rt.height, 1f / _rt.Dil.rt.width, 1f / _rt.Dil.rt.height));
                    cmd.SetGlobalFloat("_ECOutlinePixelScale",
                        (float)cameraDesc(cameraData).height / _settings.pixelScaleBaseHeight);

                    // 下采样：全分辨率遮罩 -> 半分辨率 enc
                    CoreUtils.SetRenderTarget(cmd, _rt.Enc, ClearFlag.Color, Color.clear);
                    _material.SetPass(0);
                    cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3);

                    // 形态学
                    CoreUtils.SetRenderTarget(cmd, _rt.Dil, ClearFlag.Color, Color.clear);
                    _material.SetPass(1);
                    cmd.DrawProcedural(Matrix4x4.identity, _material, 1, MeshTopology.Triangles, 3);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            private static RenderTextureDescriptor cameraDesc(CameraData cameraData)
            {
                return cameraData.cameraTargetDescriptor;
            }
        }

        // ====================================================================
        // Pass 4：合成
        // ====================================================================
        private class ECOutlineCompositePass : ScriptableRenderPass
        {
            private static readonly ProfilingSampler s_Sampler = new ProfilingSampler("EC Outline Composite");

            private readonly Material _material;
            private readonly SharedRT _rt;
            private Settings _settings;

            public ECOutlineCompositePass(Material material, Settings settings, SharedRT rt)
            {
                _material = material;
                _settings = settings;
                _rt = rt;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            }

            public void Setup(Settings settings)
            {
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                _rt.Temp = RenderingUtils.ReAllocateIfNeeded(ref _rt.Temp, desc,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ECTempColor");

                // 注意：**不** ConfigureInput(ScriptableRenderPassInput.Color)。
                // 本 Pass 在 BeforeRenderingTransparents 注入，直接读自己拷出来的
                // _ECTempColor；请求 Color 输入会额外逼 URP 生成一份 _CameraOpaqueTexture，
                // 在集显上是一笔白付的拷贝。
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, s_Sampler))
                {
                    RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTarget;

                    // 1) 相机颜色 -> 副本（URP 14 无 requiresIntermediateTexture，必须自己拷）
                    Blitter.BlitCameraTexture(cmd, cameraColor, _rt.Temp);

                    // 2) 合成：副本 + 遮罩 + 形态学 -> 相机颜色
                    cmd.SetGlobalTexture("_ECTempColor", _rt.Temp);
                    cmd.SetGlobalTexture("_ECOutlineDil", _rt.Dil);
                    cmd.SetGlobalVector("_ECOutlineTempSize", new Vector4(
                        _rt.Temp.rt.width, _rt.Temp.rt.height,
                        1f / _rt.Temp.rt.width, 1f / _rt.Temp.rt.height));
                    cmd.SetGlobalVector("_ECOutlineMaskSize", new Vector4(
                        _rt.Mask.rt.width, _rt.Mask.rt.height,
                        1f / _rt.Mask.rt.width, 1f / _rt.Mask.rt.height));

                    _material.SetFloat("_ECOutlineInkDelta", _settings.inkMinLumaDelta);
                    _material.SetFloat("_ECOutlinePressOn", _settings.pressEdgeEnabled ? 1f : 0f);
                    _material.SetFloat("_ECOutlineQuality", _settings.quality);
                    _material.SetFloat("_ECOutlineUseSemanticColor", 1f);

                    Blitter.BlitCameraTexture(cmd, _rt.Temp, cameraColor, _material, 0);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
