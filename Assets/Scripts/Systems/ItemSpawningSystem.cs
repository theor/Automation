using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Automation
{
    // class ItemRenderingSystem : SystemBase
    // {
    //     private InstancedRenderMeshBatchGroup _group;
    //
    //     protected override void OnCreate()
    //     {
    //         base.OnCreate();
    //         _group = new Unity.Rendering.InstancedRenderMeshBatchGroup(EntityManager, this, default);
    //     }
    //
    //     protected override void OnUpdate()
    //     {
    //         _group.
    //     }
    // }

    class ItemSpawningSystem : SystemBase
    {
        private ItemSpawningCommandSystem _ecbSystem;
        private EntityQuery _getEntityQuery;

        public struct SpawnedItemVisual : IComponentData
        {
            public Entity BeltSegment;
            public int BeltItemIndex;
        }

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystem<ItemSpawningCommandSystem>();
            _getEntityQuery = GetEntityQuery(ComponentType.ReadWrite<SpawnedItemVisual>());
        }

        protected override void OnUpdate()
        {
            // var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.MultiPlayback);
            
            var settings = GetSingleton<World.Settings>();
            var itemBuffers = GetBufferFromEntity<BeltItem>();
            Entities.ForEach((Entity e, in SpawnedItemVisual v) =>
            {
                
                var b = itemBuffers[v.BeltSegment];
                b.ElementAt(v.BeltItemIndex).Entity = e;
            }).Run();

            EntityManager.RemoveComponent<SpawnedItemVisual>(_getEntityQuery);
            
            var prefab = GetSingleton<World.Prefabs>();

            var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
            _ecbSystem.AddJobHandleForProducer(
                Dependency = Entities
                .ForEach((Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    float dist = 0;
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref var item = ref items.ElementAt(i);
                        dist += (item.Distance /(float)settings.BeltDistanceSubDiv);
                        var beltItemVisual = new BeltItemVisual
                        {
                            Type = item.Type,
                            AccumulatedDistance = segment.ComputePosition(dist)
                        };
                        // Debug.Log(String.Format("Compute dist {0} at {1}", dist, beltItemVisual.AccumulatedDistance));
                        if (item.Entity == Entity.Null)
                        {
                            var itemEntity = ecb.Instantiate(entityInQueryIndex, item.Type == EntityType.A ? prefab.ItemPrefab : prefab.Item2Prefab);
                            ecb.AddComponent(entityInQueryIndex, itemEntity, new SpawnedItemVisual
                            {
                                BeltSegment = e,BeltItemIndex = i,
                            });
                       
                            ecb.AddComponent(entityInQueryIndex, itemEntity, beltItemVisual);
                        }
                        else
                            ecb.SetComponent(entityInQueryIndex, item.Entity, beltItemVisual);

                    }
                })
                .ScheduleParallel(Dependency)
                    )
                    ;
        }
    }
}