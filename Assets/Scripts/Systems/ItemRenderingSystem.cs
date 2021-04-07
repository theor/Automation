using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Automation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    class ItemRenderingSystem : SystemBase
    {
        private RenderedItemPositionComputationSystem _renderedItemPositionComputationSystem;
        private GraphicsBuffer[] _graphicsBuffers;

        protected override void OnCreate()
        {
            base.OnCreate();
            _renderedItemPositionComputationSystem = World.GetExistingSystem<RenderedItemPositionComputationSystem>();
            _graphicsBuffers = new GraphicsBuffer[2];
        }

        protected override void OnDestroy()
        {
            for (int i = 0; i < _graphicsBuffers.Length; i++)
                _graphicsBuffers[i]?.Dispose();
        }

        protected override void OnUpdate()
        {
            _renderedItemPositionComputationSystem.SetupDependency.Complete();
            var renderedItemPositions = _renderedItemPositionComputationSystem.RenderedItemPositions;

            var prefabs = GetSingleton<World.Prefabs>();
            
            for (var index = 0; index < renderedItemPositions.Length; index++)
            {
                NativeArray<float3> itemPositions = renderedItemPositions[index];
                if (itemPositions.Length == 0)
                    continue;
                if (_graphicsBuffers[index] == null || _graphicsBuffers[index].count != itemPositions.Length)
                {
                    _graphicsBuffers[index]?.Dispose();
                    _graphicsBuffers[index] = new GraphicsBuffer( GraphicsBuffer.Target.Structured, itemPositions.Length, 12);
                }
                _graphicsBuffers[index].SetData(itemPositions);
                var materialPropertyBlock = new MaterialPropertyBlock();
                materialPropertyBlock.SetBuffer("_AllInstancesTransformBuffer", _graphicsBuffers[index]);

                var m = EntityManager.GetSharedComponentData<Unity.Rendering.RenderMesh>(
                    index == 0 ? prefabs.ItemPrefab : prefabs.Item2Prefab); 

                Graphics.DrawMeshInstancedProcedural(m.mesh, 0, m.material, new Bounds(Vector3.zero, Vector3.one*10000),itemPositions.Length, materialPropertyBlock);
            }
           
        }
    }
}