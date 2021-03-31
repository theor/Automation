using System;
using Unity.Entities;
using UnityEngine;

namespace Automation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    class ItemRenderingSystem : SystemBase
    {
        private RenderedItemPositionComputationSystem _irss;
        private GraphicsBuffer _bufferWithArgs;

        protected override void OnCreate()
        {
            base.OnCreate();
            _irss = World.GetExistingSystem<RenderedItemPositionComputationSystem>();
        }

        protected override void OnDestroy()
        {
            _bufferWithArgs?.Dispose();
        }

        protected override void OnUpdate()
        {
            if (_irss.RenderedItemCount == 0)
                return;
            _irss.SetupDependency.Complete();
            var prefabs = GetSingleton<World.Prefabs>();
            var m = EntityManager.GetSharedComponentData<Unity.Rendering.RenderMesh>(prefabs.ItemPrefab);
            if (_bufferWithArgs == null || _bufferWithArgs.count != _irss.RenderedItemCount)
            {
                _bufferWithArgs?.Dispose();
                _bufferWithArgs = new GraphicsBuffer( GraphicsBuffer.Target.Structured, _irss.RenderedItemCount, 12);
            }
            _bufferWithArgs.SetData(_irss.RenderedItemPositions);
            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetBuffer("_AllInstancesTransformBuffer", _bufferWithArgs);
            Graphics.DrawMeshInstancedProcedural(m.mesh, 0, m.material, new Bounds(Vector3.zero, Vector3.one*10000),_irss.RenderedItemCount, materialPropertyBlock);
        }
    }
}