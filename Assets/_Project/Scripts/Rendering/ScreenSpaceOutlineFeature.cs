using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Sastermate.Rendering
{
    /// <summary>Depth/normal outlines for opaque geometry. Unity 6.3 / URP 17.3 Render Graph.</summary>
    public sealed class ScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader outlineShader;
        [SerializeField] private Color outlineColor = Color.black;
        [Tooltip("Nominal full stroke width in output pixels, independent of world scale and distance.")]
        [SerializeField, Range(1f, 8f)] private float thickness = 3f;
        [Tooltip("Depth curvature threshold in world units. Small values suit the miniature city.")]
        [SerializeField, Min(0.0001f)] private float depthThreshold = 0.02f;
        [Tooltip("Normal discontinuity: 1 - dot(N1, N2). 0.2 is about a 37 degree crease.")]
        [SerializeField, Range(0.001f, 2f)] private float normalThreshold = 0.2f;
        [SerializeField] private bool showInSceneView = true;

        private Material _material;
        private OutlinePass _pass;

        public override void Create()
        {
            _pass = new OutlinePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            var cameraType = cameraData.camera.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView) return;
            if (cameraType == CameraType.SceneView && !showInSceneView) return;
            if (cameraData.renderType == CameraRenderType.Overlay) return;
            if (!outlineShader || !outlineShader.isSupported) return;

            if (!_material || _material.shader != outlineShader)
            {
                CoreUtils.Destroy(_material);
                _material = CoreUtils.CreateEngineMaterial(outlineShader);
            }

            _pass.Setup(_material, outlineColor, Mathf.Clamp(thickness, 1f, 8f),
                Mathf.Max(0.0001f, depthThreshold), Mathf.Max(0.001f, normalThreshold));
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int ParametersId = Shader.PropertyToID("_OutlineParameters");
            private static readonly int PixelSizeId = Shader.PropertyToID("_OutlinePixelSize");
            private Material _material;
            private Color _color;
            private Vector4 _parameters;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public Color color;
                public Vector4 parameters;
                public Vector4 pixelSize;
            }

            public OutlinePass()
            {
                // Outline opaque geometry first, then draw water/VFX over it.
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
                requiresIntermediateTexture = true;
            }

            public void Setup(Material material, Color color, float width, float depth, float normal)
            {
                _material = material;
                _color = color;
                _parameters = new Vector4(width, depth, normal, 0f);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid() ||
                    !resources.cameraNormalsTexture.IsValid()) return;

                var camera = frameData.Get<UniversalCameraData>().camera;
                var source = resources.activeColorTexture;
                var descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "Outlined camera color";
                descriptor.clearBuffer = false;
                descriptor.msaaSamples = MSAASamples.None;
                var destination = renderGraph.CreateTexture(descriptor);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           "Screen space depth and normal outlines", out var passData))
                {
                    passData.source = source;
                    passData.material = _material;
                    passData.color = _color;
                    passData.parameters = _parameters;
                    passData.pixelSize = new Vector4(1f / Mathf.Max(1, camera.pixelWidth),
                        1f / Mathf.Max(1, camera.pixelHeight), 0f, 0f);

                    // Declare every sampled texture so Render Graph keeps its producer alive.
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetColor(ColorId, data.color);
                        data.material.SetVector(ParametersId, data.parameters);
                        data.material.SetVector(PixelSizeId, data.pixelSize);
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f),
                            data.material, 0);
                    });
                }

                // Copy back rather than retain a transient cameraColor across a camera stack.
                // Source and destination are always different: no read/write feedback.
                renderGraph.AddBlitPass(destination, source, Vector2.one, Vector2.zero,
                    passName: "Copy outlines to camera color");
            }
        }
    }
}
