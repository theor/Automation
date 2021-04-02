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
            Entities.ForEach(
                (Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    var p = BeltSegment.FromI2(segment.DropPoint);
                    var rev = BeltSegment.FromI2(segment.RevDir)/settings.BeltDistanceSubDiv;
                    for (int i = 0; i < items.Length; i++)
                    {
                        var n = items[i].Distance * rev;
                        Debug.DrawRay((Vector3)p + Vector3.up * (i+1)/10f, n, HaltonSequence.ColorFromIndex(i+1));
                        p += n;
                    }

                    if (segment.DistanceToInsertAtStart == 0)
                    {
                        Debug.DrawRay(p, Vector3.up, Color.white);
                    }
                    else
                        Debug.DrawRay((Vector3)p + Vector3.up * .1f, segment.DistanceToInsertAtStart*rev, HaltonSequence.ColorFromIndex(0));
                    
                }).Run();
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
    
    public static class HaltonSequence
    {
        public static double Halton(int index, int nbase)
        {
            double fraction = 1;
            double result = 0;
            while (index > 0)
            {
                fraction /= nbase;
                result += fraction * (index % nbase);
                index = ~~(index / nbase);
            }

            return result;
        }

        // shortcut for later
        public static int HaltonInt(int index, int nbase, int max) => (int) (Halton(index, nbase) * max);
        public static Color ColorFromIndex(int index, int hbase = 3, float v = 0.5f)
        {
            return Color.HSVToRGB((float) HaltonSequence.Halton(index, hbase), 1, v);
        }
    }
}