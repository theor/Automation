using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Automation
{
    [UpdateAfter(typeof(CullingSystem))]
    class BeltUpdateSystem : SystemBase
    {
        private NativeArray<Entity> _simulationChunksFirstSegment;

        struct S : IJobParallelFor
        {
            public NativeArray<Entity> SimulationChunksFirstSegment;
            
            [NativeDisableContainerSafetyRestriction]
            public ComponentDataFromEntity<BeltSegment> Segments;
            [NativeDisableContainerSafetyRestriction]
            public BufferFromEntity<BeltItem> Items;
            public World.Settings settings;
            // [NativeDisableContainerSafetyRestriction]
            // public BufferFromEntity<InsertInQueue> queues;
            public void Execute(int index)
            {
                Entity e = SimulationChunksFirstSegment[index];
                int iter = 0;
                do
                {
                    if(iter++ >= 1000)
                        throw new NotImplementedException();
                    var segment = Segments[e];
                    var items = Items[e];
                    // Debug.Log($"{e} {segment} {items.Length}");
                    
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref BeltItem item = ref items.ElementAt(i);
                        if (segment.Next == Entity.Null)
                        {
                            if (item.Distance > settings.BeltDistanceSubDiv)
                            {
                                item.Distance--;
                                break;
                            }

                            continue;
                        }
                        
                        // if (item.Distance > settings.BeltDistanceSubDiv)
                        // {
                        //     item.Distance--;
                        // }
                        // else // check if there's room in the next one
                        // {
                        //     continue;
                        // }

                        // there's a next segment
                        if (item.Distance > 0)
                        {
                            item.Distance--;
                            // Debug.Log("Move");
                        }
                        
                        if (item.Distance > 0)
                            break;

                        int2 dropPoint = segment.DropPoint;
                        // queues[segment.Next].Add(new InsertInQueue(item, dropPoint));

                        var nextSegmentItems = Items[segment.Next];
                        Segments[segment.Next].InsertItem(in settings, ref nextSegmentItems, item, dropPoint);
                        items.RemoveAt(i);
                        if (i < items.Length)
                        {
                            ref BeltItem nextItem = ref items.ElementAt(i);
                            nextItem.Distance++;
                        }

                        i--;
                    }
                    
                    
                    e = segment.Prev;

                } while (e != Entity.Null);
            }
        }

        protected override void OnCreate()
        {
            // _simulationChunksFirstSegment = new NativeArray<Entity>(1, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            _simulationChunksFirstSegment.Dispose();
        }

        protected override void OnUpdate()
        {
            if (!_simulationChunksFirstSegment.IsCreated)
            {
                NativeStream ns = new NativeStream(JobsUtility.JobWorkerMaximumCount, Allocator.TempJob);
                var w = ns.AsWriter();
                Entities.ForEach((Entity e, in BeltSegment s) =>
                {
                    w.BeginForEachIndex(1);
                    if (s.Next == Entity.Null)
                    {
                        w.Write(e);
                    }
                    w.EndForEachIndex();
                }).Schedule(Dependency).Complete();
                _simulationChunksFirstSegment = ns.ToNativeArray<Entity>(Allocator.Persistent);
                ns.Dispose();
            }

            World.Settings settings = GetSingleton<World.Settings>();
            // BufferFromEntity<InsertInQueue> queues = GetBufferFromEntity<InsertInQueue>();
            Dependency = new S
            {
                // queues = queues,
                settings = settings,
                Items = GetBufferFromEntity<BeltItem>(),
                Segments = GetComponentDataFromEntity<BeltSegment>(),
                SimulationChunksFirstSegment = _simulationChunksFirstSegment,
            }.Schedule(1, 1, Dependency);
            // Dependency = Entities.ForEach(
            //     (Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
            //     {
            //         for (int i = 0; i < items.Length; i++)
            //         {
            //             ref BeltItem item = ref items.ElementAt(i);
            //             if (segment.Next == Entity.Null)
            //             {
            //                 if (item.Distance > settings.BeltDistanceSubDiv)
            //                 {
            //                     item.Distance--;
            //                     break;
            //                 }
            //
            //                 continue;
            //             }
            //             
            //             if (item.Distance > settings.BeltDistanceSubDiv)
            //             {
            //                 item.Distance--;
            //             }
            //             else
            //                 break;
            //
            //             // there's a next segment
            //             if (item.Distance > 0)
            //             {
            //                 item.Distance--;
            //                 // Debug.Log("Move");
            //             }
            //             
            //             if (item.Distance > 0)
            //                 break;
            //
            //             int2 dropPoint = segment.DropPoint;
            //             queues[segment.Next].Add(new InsertInQueue(item, dropPoint));
            //             items.RemoveAt(i);
            //             if (i < items.Length)
            //             {
            //                 ref BeltItem nextItem = ref items.ElementAt(i);
            //                 nextItem.Distance++;
            //             }
            //
            //             i--;
            //         }
            //     }).Schedule(Dependency);
            // ugly. use a parallel nativestream ? actual insertions probably need to be sequential anyway
            // _ecbSystem.AddJobHandleForProducer(Dependency);
            // ecbuffer.Playback(EntityManager);
            // _ecbSystem.pos
        }
    }
}