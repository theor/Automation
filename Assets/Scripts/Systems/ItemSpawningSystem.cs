using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Automation
{
    class ItemSpawningSystem : SystemBase
    {
        private ItemSpawningCommandSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystem<ItemSpawningCommandSystem>();
        }

        public struct SpawnedItemVisual : IComponentData
        {
            public Entity BeltSegment;
            public int BeltItemIndex;
        }
        
        protected override void OnUpdate()
        {
            var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.MultiPlayback);
            

            Entities.ForEach((Entity e, in SpawnedItemVisual v) =>
            {
                
                var b = EntityManager.GetBuffer<BeltItem>(v.BeltSegment);
                b.ElementAt(v.BeltItemIndex).Entity = e;
                entityCommandBuffer.RemoveComponent<SpawnedItemVisual>(e);
            }).WithoutBurst().Run();
            entityCommandBuffer.Playback(EntityManager);
            
            var prefab = GetSingleton<World.Prefabs>();

            var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
            _ecbSystem.AddJobHandleForProducer(Dependency = Entities
                .ForEach((Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    float dist = 0;
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref var item = ref items.ElementAt(i);
                        dist += (item.Distance /(float)BeltUpdateSystem.BeltDistanceSubDiv);
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
                .ScheduleParallel(Dependency));
        }
    }
}