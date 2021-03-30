using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
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
        // private ItemSpawningCommandSystem _ecbSystem;
        private EntityQuery _getEntityQuery;
        private NativeStream _toInstantiate;
        private NativeArray<int> _count;

        // public struct SpawnedItemVisual : IComponentData
        // {
        //     public Entity BeltSegment;
        //     public int BeltItemIndex;
        // }

        struct ToInstantiate
        {
            public Entity VisualEntity;
            public Entity BeltSegment;
            public int BeltItemIndex;
        }
        
        protected override void OnCreate()
        {
            // _ecbSystem = World.GetExistingSystem<ItemSpawningCommandSystem>();
            _getEntityQuery = GetEntityQuery(ComponentType.ReadWrite<BeltSegment>());
            _count = new NativeArray<int>(1, Allocator.TempJob);
        }
        
        [BurstCompile]
         struct CollectInstantiateJob : IJob
         {
             [ReadOnly]
             public NativeStream.Reader ToInstantiateStream;
             public BufferFromEntity<BeltItem> BeltItemBuffers;

             public void Execute()
             {
                 // Debug.Log(ToInstantiateStream.ForEachCount);
                 for (int i = 0; i < ToInstantiateStream.ForEachCount; i++)
                 {
                     ToInstantiateStream.BeginForEachIndex(i);
                     // Debug.Log(ToInstantiateStream.RemainingItemCount);
                     while (ToInstantiateStream.RemainingItemCount > 0)
                     {
                         var toi = ToInstantiateStream.Read<ToInstantiate>();
                         var b = BeltItemBuffers[toi.BeltSegment];
                         b.ElementAt(toi.BeltItemIndex).Entity = toi.VisualEntity;
                     }
                     ToInstantiateStream.EndForEachIndex();
                 }
             }
         }

        protected override unsafe void OnUpdate()
        {
            // var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.MultiPlayback);
            
            var settings = GetSingleton<World.Settings>();
            // var itemBuffers = GetBufferFromEntity<BeltItem>();
            // Entities.WithNone<Prefab>().ForEach((Entity e, in SpawnedItemVisual v) =>
            // {
            //     
            //     var b = itemBuffers[v.BeltSegment];
            //     b.ElementAt(v.BeltItemIndex).Entity = e;
            // }).Run();
            //
            // EntityManager.RemoveComponent<SpawnedItemVisual>(_getEntityQuery);

            // q.AsParallelReader().

            NativeArray<Entity> instances;
            // Debug.Log("Count at frame start: " + _count[0]);
            if (_count[0] > 0)
            {
                var prefab = GetSingleton<World.Prefabs>();
                instances = new NativeArray<Entity>(_count[0], Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                EntityManager.Instantiate(prefab.ItemPrefab, instances);
            }
            else
                instances = new NativeArray<Entity>(0, Allocator.Temp);

            _count[0] = 0;
            int instanceIndex = -1;

            // var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
            ComponentDataFromEntity<BeltItemVisual> beltItemVisuals = GetComponentDataFromEntity<BeltItemVisual>();

            var beltcount = _getEntityQuery.CalculateEntityCount();
            // if (!_toInstantiate.IsCreated || _toInstantiate.ForEachCount < beltcount)
            {
                if(_toInstantiate.IsCreated)
                    _toInstantiate.Dispose();
                _toInstantiate = new NativeStream(beltcount, Allocator.Persistent);
            }

            var writer = _toInstantiate.AsWriter();

            var countPtr = (int*)_count.GetUnsafePtr();
            // _ecbSystem.AddJobHandleForProducer(
            Dependency =
                Entities
                .ForEach((Entity e, int nativeThreadIndex, int entityInQueryIndex, DynamicBuffer<BeltItem> items,
                    in BeltSegment segment) =>
                {
                    writer.BeginForEachIndex(entityInQueryIndex);
                    float dist = 0;
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref var item = ref items.ElementAt(i);
                        dist += (item.Distance / (float) settings.BeltDistanceSubDiv);
                        var beltItemVisual = new BeltItemVisual
                        {
                            Type = item.Type,
                            AccumulatedDistance = segment.ComputePosition(dist)
                        };
                        // Debug.Log(String.Format("Compute dist {0} at {1}", dist, beltItemVisual.AccumulatedDistance));
                        if (item.Entity == Entity.Null)
                        {

                            var index = Interlocked.Increment(ref instanceIndex);
                            // Debug.Log($"Created {instances.IsCreated} len {instances.Length}");
                            if (instances.IsCreated && index < instances.Length)
                            {
                                // Debug.Log("Write");
                                writer.Write(new ToInstantiate
                                {
                                    BeltSegment = e,
                                    BeltItemIndex = i,
                                    VisualEntity = instances[index]
                                });}
                            var newone = Interlocked.Increment(ref UnsafeUtility.AsRef<int>(countPtr));
                            // Debug.Log(String.Format("Inst: {0}", newone));
                        }
                        else
                            beltItemVisuals[item.Entity] = beltItemVisual;

                    }

                    writer.EndForEachIndex();
                })
                .WithNativeDisableContainerSafetyRestriction(beltItemVisuals)
                .WithNativeDisableContainerSafetyRestriction(instances)
                .WithNativeDisableUnsafePtrRestriction(countPtr)
                // .Run();
                .ScheduleParallel(Dependency);
            
            // )
            ;
            Dependency =
                new CollectInstantiateJob
            {
                ToInstantiateStream = _toInstantiate.AsReader(),
                BeltItemBuffers = GetBufferFromEntity<BeltItem>(),
            }
                    // .Run();
                    .Schedule( Dependency);
            // Entities.
            // if (item.Entity == Entity.Null)
            // {
            //     var newCount = Interlocked.Increment(ref count);
            //     Debug.Log(String.Format("Inst {0}", newCount));
            //
            //     var itemEntity = ecb.Instantiate(entityInQueryIndex, item.Type == EntityType.A ? prefab.ItemPrefab : prefab.Item2Prefab);
            //     ecb.AddComponent(entityInQueryIndex, itemEntity, new SpawnedItemVisual
            //     {
            //         BeltSegment = e,BeltItemIndex = i,
            //     });
            //     ecb.SetComponent(entityInQueryIndex, itemEntity, beltItemVisual);
            //
            // }
            // else
        }
    }
}