using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Automation
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    class BeltUpdateSystem : SystemBase
    {
        private EntityQueryMask _hasBeltSegmentMask, _hasBeltSplitterMask;
        private EntityQuery _lastBeltSegmentQuery;

        struct LastBeltSegment: IComponentData{}
        
        [BurstCompile]
        struct BeltUpdateJob : IJobFor
        {
            public NativeArray<Entity> SimulationChunksFirstSegment;

            public EntityQueryMask HasBeltSegmentMask;
            public EntityQueryMask HasBeltSplitterMask;
            [NativeDisableContainerSafetyRestriction]
            public ComponentDataFromEntity<BeltSegment> Segments;
            [NativeDisableContainerSafetyRestriction]
            public ComponentDataFromEntity<BeltSplitter> Splitters;
            [NativeDisableContainerSafetyRestriction]
            public BufferFromEntity<BeltItem> Items;
            public World.Settings Settings;
            public void Execute(int index)
            {
                Entity e = SimulationChunksFirstSegment[index];
                int iter = 0;
                do
                {
                    if(iter++ >= 1000000)
                        throw new NotImplementedException();
                    var segment = Segments[e];
                    var items = Items[e];
                    // Debug.Log($"{e} {segment} {items.Length}");

                    for (int i = 0; i < items.Length; i++)
                    {
                        ref BeltItem item = ref items.ElementAt(i);

                        // simple case, too far from belt end to care about a next segment
                        if (item.Distance > Settings.BeltDistanceSubDiv)
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

                        if (!HasBeltSegmentMask.Matches(segment.Next))
                        {
                            if (HasBeltSplitterMask.Matches(segment.Next))
                            {
                                var splitter = Splitters[segment.Next];
                                // no room in input
                                if (splitter.Input.Type != ItemType.None)
                                    continue;

                                if (item.Distance > 0) // still inserting
                                {
                                    item.Distance--;
                                    segment.DistanceToInsertAtStart++;
                                    Segments[e] = segment;
                                    break;
                                }

                                // will be update this frame
                                item.Distance = (ushort) (Settings.BeltDistanceSubDiv);
                                splitter.Input = item;
                                Splitters[segment.Next] = splitter;
                                items.RemoveAt(i);
                                // Debug.Log("MOVE TO SPLITTER");
                                i--;
                            }

                            continue;
                        }

                        // only move if the next segment has room
                        var nextBeltSegment = Segments[segment.Next];
                        if (nextBeltSegment.DistanceToInsertAtStart == 0)
                            continue;

                        if (item.Distance > 0) // still inserting
                        {
                            item.Distance--;
                            segment.DistanceToInsertAtStart++;
                            Segments[e] = segment;
                        }
                        else if (InsertInSegment(ref Items, ref Segments, item, segment.Next))
                        {
                            items.RemoveAt(i);
                            i--;
                        }

                        break;
                    }
                    e = segment.Prev;
                } while (e != Entity.Null);
            }
        }

        protected override void OnCreate()
        {
            _lastBeltSegmentQuery = GetEntityQuery(ComponentType.ReadOnly<LastBeltSegment>());
        }

        protected override void OnDestroy()
        {
        }

        protected override void OnUpdate()
        {
            World.Settings settings = GetSingleton<World.Settings>();
            if(_lastBeltSegmentQuery.IsEmpty)
            {
                _hasBeltSplitterMask = GetEntityQuery(ComponentType.ReadOnly<BeltSplitter>()).GetEntityQueryMask();
                var beltSegmentMask = _hasBeltSegmentMask = GetEntityQuery(ComponentType.ReadOnly<BeltSegment>()).GetEntityQueryMask();
                var segments = GetComponentDataFromEntity<BeltSegment>();
                Entities.ForEach((Entity e, DynamicBuffer<BeltItem> dynamicBuffer, ref BeltSegment segment) =>
                {
                    segment.ComputeInsertionPoint(ref dynamicBuffer, settings.BeltDistanceSubDiv);

                    var next = segment.Next;
                    if (next != Entity.Null)
                    {
                        if (beltSegmentMask.Matches(next))
                        {
                            var nextBeltSegment = segments[next];
                            nextBeltSegment.Prev = e;
                            segments[next] = nextBeltSegment;
                        }
                    }
                }).Run();
                var lastSegments = new NativeList<Entity>(Allocator.TempJob);
                Entities.ForEach((Entity e, int nativeThreadIndex, in BeltSegment s) =>
                {
                    if (s.Next == Entity.Null || !beltSegmentMask.Matches(s.Next))
                        lastSegments.Add(e);
                })
                    .Schedule(Dependency)
                    .Complete();
                EntityManager.AddComponent<LastBeltSegment>(lastSegments);
                lastSegments.Dispose();
            }

            // Debug.Log(String.Join(", ", _simulationChunksFirstSegment.ToArray()));

            var simulationChunksFirstSegment = _lastBeltSegmentQuery.ToEntityArrayAsync(Allocator.TempJob, out var entitiesReady);
            Dependency =
                new BeltUpdateJob
            {
                Settings = settings,
                HasBeltSegmentMask = _hasBeltSegmentMask,
                HasBeltSplitterMask = _hasBeltSplitterMask,
                Items = GetBufferFromEntity<BeltItem>(),
                Segments = GetComponentDataFromEntity<BeltSegment>(),
                Splitters = GetComponentDataFromEntity<BeltSplitter>(),
                SimulationChunksFirstSegment = simulationChunksFirstSegment,
            }
                    // .Run(_simulationChunksFirstSegment.Length);
                .ScheduleParallel(_lastBeltSegmentQuery.CalculateEntityCount(), 1, JobHandle.CombineDependencies( entitiesReady, Dependency));
            Dependency = simulationChunksFirstSegment.Dispose(Dependency);

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

        public static bool InsertInSegment(
            ref BufferFromEntity<BeltItem> segmentItems,
            ref ComponentDataFromEntity<BeltSegment> segments,
            BeltItem item, Entity nextSegment)
        {
            // only move if the next segment has room
            var nextBeltSegment = segments[nextSegment];

            if (item.Distance > 0) // still inserting
                return false;

            // insertion done, distance == 0
            var nextSegmentItems = segmentItems[nextSegment];

            item.Distance = nextBeltSegment.DistanceToInsertAtStart;
            nextBeltSegment.DistanceToInsertAtStart = 0;
            segments[nextSegment] = nextBeltSegment;

            nextSegmentItems.Insert(nextSegmentItems.Length, item);
            return true;
        }
    }
}