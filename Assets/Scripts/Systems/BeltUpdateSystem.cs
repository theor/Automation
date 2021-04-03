using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace Automation
{
    [UpdateAfter(typeof(CullingSystem))]
    class BeltUpdateSystem : SystemBase
    {
        private NativeArray<Entity> _simulationChunksFirstSegment;

        [BurstCompile]
        struct BeltUpdateJob : IJobParallelFor
        {
            public NativeArray<Entity> SimulationChunksFirstSegment;

            [NativeDisableContainerSafetyRestriction]
            public ComponentDataFromEntity<BeltSegment> Segments;
            [NativeDisableContainerSafetyRestriction]
            public BufferFromEntity<BeltItem> Items;
            public World.Settings settings;
            public void Execute(int index)
            {
                Entity e = SimulationChunksFirstSegment[index];
                int iter = 0;
                do
                {
                    if(iter++ >= 100000)
                        throw new NotImplementedException();
                    var segment = Segments[e];
                    var items = Items[e];
                    // Debug.Log($"{e} {segment} {items.Length}");

                    for (int i = 0; i < items.Length; i++)
                    {
                        ref BeltItem item = ref items.ElementAt(i);

                        // simple case, too far from belt end to care about a next segment
                        if (item.Distance > settings.BeltDistanceSubDiv)
                        {
                            item.Distance--;
                            segment.DistanceToInsertAtStart++;
                            Segments[e] = segment;
                            break;
                        }
                        // no next segment, so BeltDistanceSubDiv is the min distance
                        // continue to move the next item on the belt
                        if (segment.Next == Entity.Null)
                        {
                            continue;
                        }

                        // only move if the next segment has room
                        var nextBeltSegment = Segments[segment.Next];
                        if(nextBeltSegment.DistanceToInsertAtStart == 0)
                            continue; // move next item on this belt

                        if (item.Distance > 0) // still inserting
                        {
                             item.Distance--;
                             segment.DistanceToInsertAtStart++;
                             Segments[e] = segment;
                             break;
                        }

                        // insertion done, distance == 0
                        var nextSegmentItems = Items[segment.Next];

                        item.Distance = nextBeltSegment.DistanceToInsertAtStart;
                        nextBeltSegment.DistanceToInsertAtStart = 0;
                        Segments[segment.Next] = nextBeltSegment;

                        nextSegmentItems.Insert(nextSegmentItems.Length, item);
                        items.RemoveAt(i);

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
                var ns = new NativeList<Entity>(Allocator.Persistent);

                Entities.ForEach((Entity e, int nativeThreadIndex, in BeltSegment s) =>
                {
                    if (s.Next == Entity.Null)
                    {
                        ns.Add(e);
                    }
                }).Schedule(Dependency).Complete();
                _simulationChunksFirstSegment = ns;
            }

            World.Settings settings = GetSingleton<World.Settings>();
            Dependency = new BeltUpdateJob
            {
                settings = settings,
                Items = GetBufferFromEntity<BeltItem>(),
                Segments = GetComponentDataFromEntity<BeltSegment>(),
                SimulationChunksFirstSegment = _simulationChunksFirstSegment,
            }.Schedule(1, 1, Dependency);


            // Debug draw
            if(settings.DebugDraw)
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
        }
    }
}