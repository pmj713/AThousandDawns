using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace AThousandDawns.Art
{
    // 화면 전체를 한 장의 수묵화처럼: 한지 결·종이 섬유, 가장자리가 한지색으로 바래는 번짐, 색 바램(붉은색은 남김).
    // 이 컴포넌트가 붙은 카메라에만 후처리를 끼워 넣어요 (프로젝트 공용 URP 렌더러 설정은 건드리지 않음).
    // 값은 재질(InkPaperScreen 셰이더)에서 조절해요. UI(HUD)는 영향 없음.
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class InkPaperPostFX : MonoBehaviour
    {
        [SerializeField] Material material;

        Camera _camera;
        InkPaperPass _pass;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            _pass ??= new InkPaperPass();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera || material == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;
            _pass.Setup(material);
            data.scriptableRenderer.EnqueuePass(_pass);
        }

        sealed class InkPaperPass : ScriptableRenderPass
        {
            Material _material;

            public InkPaperPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public void Setup(Material material) => _material = material;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || _material == null) return;

                TextureHandle source = resources.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "InkPaperColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0), "Ink Paper");
                resources.cameraColor = destination;
            }
        }
    }
}
