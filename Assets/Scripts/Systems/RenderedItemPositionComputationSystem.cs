using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Automation
{
    [UpdateAfter(typeof(CullingSystem))]
    class RenderedItemPositionComputationSystem : SystemBase
    {

        public NativeArray<float3>[] RenderedItemPositions;
        public JobHandle SetupDependency;
        private NativeArray<int> _itemPositionArrayIndex;
        private CullingSystem _cullingSystem;
        private unsafe float3** _renderedItemPositionsPointer;
        private EntityQuery _segmentQuery;
        private EntityQuery _splitterQuery;

        protected override unsafe void OnCreate()
        {
            _cullingSystem = World.GetExistingSystem<CullingSystem>();
            _itemPositionArrayIndex = new NativeArray<int>(2, Allocator.Persistent);
            RenderedItemPositions = new NativeArray<float3>[2];
            _renderedItemPositionsPointer = (float3**) UnsafeUtility.Malloc(UnsafeUtility.SizeOf<IntPtr>() * 2, UnsafeUtility.AlignOf<IntPtr>(), Allocator.Persistent);
            _segmentQuery = GetEntityQuery(ComponentType.ReadOnly<BeltSegment>(), ComponentType.ReadOnly<BeltItem>());
            _splitterQuery = GetEntityQuery(ComponentType.ReadOnly<BeltSplitter>());
            base.OnCreate();
        }

        protected override unsafe void OnDestroy()
        {
            base.OnDestroy();
            for (int i = 0; i < RenderedItemPositions.Length; i++)
            {
                if(RenderedItemPositions[i].IsCreated)
                    RenderedItemPositions[i].Dispose();
            }
            UnsafeUtility.Free(_renderedItemPositionsPointer, Allocator.Persistent);
            _itemPositionArrayIndex.Dispose();
        }

        protected override unsafe void OnUpdate()

        {
            if(!_cullingSystem.RenderedItemCount.IsCreated)
                return;
            if(!SetupDependency.IsCompleted)
                throw new NotImplementedException();
            _cullingSystem.CountDependency.Complete();
            int total = 0;
            for (var index = 0; index < _cullingSystem.RenderedItemCount.Length; index++)
                total += _cullingSystem.RenderedItemCount[index];
            if (total == 0)
                return;

            for (var index = 0; index < _cullingSystem.RenderedItemCount.Length; index++)
            {
                if (!RenderedItemPositions[index].IsCreated || _cullingSystem.RenderedItemCount[index] != RenderedItemPositions[index].Length)
                {
                    if (RenderedItemPositions[index].IsCreated)
                        RenderedItemPositions[index].Dispose();
                    // Debug.Log($"Allocate {(EntityType.A + (byte)index)} {_cullingSystem.RenderedItemCount[index]}");
                    RenderedItemPositions[index] = new NativeArray<float3>(_cullingSystem.RenderedItemCount[index], Allocator.Persistent);
                }
            }

            {
               _renderedItemPositionsPointer[0] = (float3*) RenderedItemPositions[0].GetUnsafePtr();
               _renderedItemPositionsPointer[1] = (float3*) RenderedItemPositions[1].GetUnsafePtr();
            };
            _itemPositionArrayIndex[0] = -1;
            _itemPositionArrayIndex[1] = -1;
            int* itemPositionArrayIndexPointer = (int*)_itemPositionArrayIndex.GetUnsafePtr();

            World.Settings settings = GetSingleton<World.Settings>();
            Dependency = new ComputePositionsJob
            {
                Settings = settings,
                // EntityHandle = GetEntityTypeHandle(),
                BeltItemsHandle = GetBufferTypeHandle<BeltItem>(),
                BeltSegmentsHandle = GetComponentTypeHandle<BeltSegment>(),
                _renderedItemPositionsPointer = _renderedItemPositionsPointer,
                itemPositionArrayIndexPointer = itemPositionArrayIndexPointer,
            }.ScheduleParallel(_segmentQuery, Dependency);
                // Entities.ForEach((DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                // {
                //     if(!segment.Rendered)
                //         return;
                //     float dist = 0;
                //     int2 dropPoint = segment.DropPoint;
                //     int2 revDir = segment.RevDir;
                //     for (int i = 0; i < items.Length; i++)
                //     {
                //         ref BeltItem item = ref items.ElementAt(i);
                //         dist += item.Distance / (float) settings.BeltDistanceSubDiv;
                //         float3 computePosition = new float3(dropPoint.x + dist * revDir.x, 0, dropPoint.y + dist * revDir.y);
                //         ref int instanceIndexRef = ref UnsafeUtility.ArrayElementAsRef<int>(itemPositionArrayIndexPointer, item.Type - EntityType.A);
                //         int index = Interlocked.Increment(ref instanceIndexRef);
                //         _renderedItemPositionsPointer[i][index] = computePosition;
                //     }
                // })
                // .WithNativeDisableUnsafePtrRestriction<float3*>(_renderedItemPositionsPointer)
                // .WithNativeDisableUnsafePtrRestriction(itemPositionArrayIndexPointer)
                // .ScheduleParallel(Dependency);
                SetupDependency = Dependency = new ComputeSplitterItemPositions
                {
                    Settings = settings,
                    BeltSplittersHandle = GetComponentTypeHandle<BeltSplitter>(),
                    _renderedItemPositionsPointer = _renderedItemPositionsPointer,
                    itemPositionArrayIndexPointer = itemPositionArrayIndexPointer,
                }.ScheduleParallel(_splitterQuery, Dependency);
                // Entities.ForEach((in BeltSplitter splitter) =>
                //     {
                //         if(!splitter.Rendered)
                //             return;
                //         ref int instanceIndexRef = ref UnsafeUtility.AsRef<int>(itemPositionArrayIndexPointer);
                //         var revDir = splitter.RevDir;
                //         ProcessSplitterItem(splitter.Input, splitter, settings, revDir, ref instanceIndexRef, renderedItemCount, _renderedItemPositionsPointer);
                //         ProcessSplitterItem(splitter.Output1, splitter, settings, revDir, ref instanceIndexRef, renderedItemCount, _renderedItemPositionsPointer);
                //         ProcessSplitterItem(splitter.Output2, splitter, settings, revDir, ref instanceIndexRef, renderedItemCount, _renderedItemPositionsPointer, 1);
                //     })
                //     .WithNativeDisableUnsafePtrRestriction(_renderedItemPositionsPointer)
                //     .WithNativeDisableUnsafePtrRestriction(itemPositionArrayIndexPointer)
                //     .ScheduleParallel(Dependency);
        }

        internal struct ComputeSplitterItemPositions : IJobChunk
        {
            public ComponentTypeHandle<BeltSplitter> BeltSplittersHandle;
            public World.Settings Settings;
            [NativeDisableUnsafePtrRestriction]
            public unsafe float3** _renderedItemPositionsPointer;

            [NativeDisableUnsafePtrRestriction]
            public unsafe int* itemPositionArrayIndexPointer;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var splitters = chunk.GetNativeArray(BeltSplittersHandle);
                for (int chunkIdx = 0; chunkIdx != splitters.Length; chunkIdx++)
                {
                    var splitter = splitters[chunkIdx];
                    if(!splitter.Rendered)
                        continue;
                    // ref int instanceIndexRef =
                    //     ref UnsafeUtility.ArrayElementAsRef<int>(itemPositionArrayIndexPointer,
                    //         item.Type - EntityType.A);
                    var revDir = splitter.RevDir;
                    ProcessSplitterItem(splitter.Input, splitter, Settings, revDir);
                    ProcessSplitterItem(splitter.Output1, splitter, Settings, revDir);
                    ProcessSplitterItem(splitter.Output2, splitter, Settings, revDir, 1);
                }
            }
            

            private unsafe void ProcessSplitterItem(in BeltItem item, in BeltSplitter splitter,
                in World.Settings settings,
                int2 revDir, 
                int zOffset = 0)
            {
                if (item.Type == EntityType.None)
                    return;
                
                ref int instanceIndexRef =
                    ref UnsafeUtility.ArrayElementAsRef<int>(itemPositionArrayIndexPointer,
                        item.Type - EntityType.A);
                var dist = item.Distance / (float) settings.BeltDistanceSubDiv;
                var cross = -new int2(-revDir.y, revDir.x) * zOffset;
                float3 computePosition = new float3(splitter.End.x + dist * revDir.x + cross.x, 0, splitter.End.y + dist * revDir.y + cross.y);
                int index = Interlocked.Increment(ref instanceIndexRef);
                _renderedItemPositionsPointer[item.Type - EntityType.A][index] = computePosition;
            }
        }

        internal struct ComputePositionsJob : IJobChunk
        {
            public World.Settings Settings;
            [ReadOnly]
            public ComponentTypeHandle<BeltSegment> BeltSegmentsHandle;
            [ReadOnly]
            public BufferTypeHandle<BeltItem> BeltItemsHandle;
            // [ReadOnly]
            // public EntityTypeHandle EntityHandle;
            [NativeDisableUnsafePtrRestriction]
            public unsafe float3** _renderedItemPositionsPointer;

            [NativeDisableUnsafePtrRestriction]
            public unsafe int* itemPositionArrayIndexPointer;

            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                // var entities = chunk.GetNativeArray(EntityHandle);
                var segments = chunk.GetNativeArray(BeltSegmentsHandle);
                var allItems = chunk.GetBufferAccessor(BeltItemsHandle);
                for (int chunkIdx = 0; chunkIdx != chunk.Count; chunkIdx++)
                {
                    var segment = segments[chunkIdx];
                    if (!segment.Rendered)
                    {
                        // Debug.Log(String.Format("  SKIP {0}", entities[chunkIdx]));
                        continue;
                    }
                    var items = allItems[chunkIdx];
                    // Debug.Log(String.Format("  Process {0} {1} items", entities[chunkIdx], items.Length));
                    float dist = 0;
                    int2 dropPoint = segment.DropPoint;
                    int2 revDir = segment.RevDir;
                    for (int i = 0; i < items.Length; i++)
                    {
                        BeltItem item = items[i];
                        dist += item.Distance / (float) Settings.BeltDistanceSubDiv;
                        float3 computePosition =
                            new float3(dropPoint.x + dist * revDir.x, 0, dropPoint.y + dist * revDir.y);
                        ref int instanceIndexRef =
                            ref UnsafeUtility.ArrayElementAsRef<int>(itemPositionArrayIndexPointer,
                                item.Type - EntityType.A);
                        int index = Interlocked.Increment(ref instanceIndexRef);
                        // Debug.Log(String.Format("Inc {0} to {1}", item.Type - EntityType.A, index));
                        _renderedItemPositionsPointer[item.Type - EntityType.A][index] = computePosition;
                    }
                }

            }
        }
    }
}