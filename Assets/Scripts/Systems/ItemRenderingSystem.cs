using System;
using Unity.Entities;
using UnityEngine;

namespace Automation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    class ItemRenderingSystem : SystemBase
    {
        private RenderedItemPositionComputationSystem _irss;
        private GraphicsBuffer[] _bufferWithArgs;

        protected override void OnCreate()
        {
            base.OnCreate();
            _irss = World.GetExistingSystem<RenderedItemPositionComputationSystem>();
            _bufferWithArgs = new GraphicsBuffer[2];
        }

        protected override void OnDestroy()
        {
            for (int i = 0; i < _bufferWithArgs.Length; i++)
            {
                _bufferWithArgs[i]?.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            _irss.SetupDependency.Complete();
            var renderedItemCount = _irss.RenderedItemPositions;

            var prefabs = GetSingleton<World.Prefabs>();
            
            for (var index = 0; index < renderedItemCount.Length; index++)
            {
                var nativeArray = renderedItemCount[index];
                if (nativeArray.Length == 0)
                {
                    // Debug.Log("Skip " + (EntityType.A + (byte) index));
                    continue;
                }
                // Debug.Log($"Draw {nativeArray.Length} {(EntityType.A + (byte) index)} {nativeArray[0]}");
                if (_bufferWithArgs[index] == null || _bufferWithArgs[index].count != nativeArray.Length)
                {
                    _bufferWithArgs[index]?.Dispose();
                    _bufferWithArgs[index] = new GraphicsBuffer( GraphicsBuffer.Target.Structured, nativeArray.Length, 12);
                }
                _bufferWithArgs[index].SetData(nativeArray);
                var materialPropertyBlock = new MaterialPropertyBlock();
                materialPropertyBlock.SetBuffer("_AllInstancesTransformBuffer", _bufferWithArgs[index]);

                var m = EntityManager.GetSharedComponentData<Unity.Rendering.RenderMesh>(
                    index == 0 ? prefabs.ItemPrefab : prefabs.Item2Prefab); 

                Graphics.DrawMeshInstancedProcedural(m.mesh, 0, m.material, new Bounds(Vector3.zero, Vector3.one*10000),nativeArray.Length, materialPropertyBlock);
            }
           
        }
    }
}