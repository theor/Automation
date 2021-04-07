using System;
using System.Threading;
using Unity.Burst;
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
        private NativeList<int> _itemPositionArrayIndex;
        private CullingSystem _cullingSystem;
        private unsafe float3** _renderedItemPositionsPointer;
        private EntityQuery _segmentQuery;
        private EntityQuery _splitterQuery;

        protected override unsafe void OnCreate()
        {
            _cullingSystem = World.GetExistingSystem<CullingSystem>();
            _itemPositionArrayIndex = new NativeList<int>(2, Allocator.Persistent);
            _itemPositionArrayIndex.Add(-1);
            _itemPositionArrayIndex.Add(-1);
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
                    RenderedItemPositions[index] = new NativeArray<float3>(_cullingSystem.RenderedItemCount[index], Allocator.Persistent);
                }
                _renderedItemPositionsPointer[index] = (float3*) RenderedItemPositions[index].GetUnsafePtr();
                _itemPositionArrayIndex[index] = -1;
            }

            World.Settings settings = GetSingleton<World.Settings>();
            Dependency = new ComputePositionsJob
            {
                Settings = settings,
                // EntityHandle = GetEntityTypeHandle(),
                BeltItemsHandle = GetBufferTypeHandle<BeltItem>(),
                BeltSegmentsHandle = GetComponentTypeHandle<BeltSegment>(),
                _renderedItemPositionsPointer = _renderedItemPositionsPointer,
                itemPositionArrayIndexPointer = _itemPositionArrayIndex,
            }.ScheduleParallel(_segmentQuery, Dependency);
            Dependency = new ComputeSplitterItemPositions
            {
                Settings = settings,
                BeltSplittersHandle = GetComponentTypeHandle<BeltSplitter>(),
                _renderedItemPositionsPointer = _renderedItemPositionsPointer,
                itemPositionArrayIndexPointer = _itemPositionArrayIndex,
            }.ScheduleParallel(_splitterQuery, Dependency);
            SetupDependency = Dependency;
        }

        [BurstCompile]
        struct ComputeSplitterItemPositions : IJobChunk
        {
            public ComponentTypeHandle<BeltSplitter> BeltSplittersHandle;
            public World.Settings Settings;
            [NativeDisableUnsafePtrRestriction]
            public unsafe float3** _renderedItemPositionsPointer;

            [NativeDisableContainerSafetyRestriction]
            public NativeList<int> itemPositionArrayIndexPointer;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var splitters = chunk.GetNativeArray(BeltSplittersHandle);
                for (int chunkIdx = 0; chunkIdx != splitters.Length; chunkIdx++)
                {
                    var splitter = splitters[chunkIdx];
                    if(!splitter.Rendered)
                        continue;
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
                if (item.Type == ItemType.None)
                    return;

                var itemTypeIndex = item.Type - ItemType.PaintBucket;
                ref int instanceIndexRef = ref itemPositionArrayIndexPointer.ElementAt(itemTypeIndex);
                var dist = item.Distance / (float) settings.BeltDistanceSubDiv;
                var cross = -new int2(-revDir.y, revDir.x) * zOffset;
                float3 computePosition = new float3(splitter.End.x + dist * revDir.x + cross.x, 0, splitter.End.y + dist * revDir.y + cross.y);
                int index = Interlocked.Increment(ref instanceIndexRef);
                _renderedItemPositionsPointer[itemTypeIndex][index] = computePosition;
            }
        }

        [BurstCompile]
        struct ComputePositionsJob : IJobChunk
        {
            public World.Settings Settings;
            [ReadOnly]
            public ComponentTypeHandle<BeltSegment> BeltSegmentsHandle;
            [ReadOnly]
            public BufferTypeHandle<BeltItem> BeltItemsHandle;
            [NativeDisableUnsafePtrRestriction]
            public unsafe float3** _renderedItemPositionsPointer;
            
            [NativeDisableContainerSafetyRestriction]
            public NativeList<int> itemPositionArrayIndexPointer;

            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                NativeArray<BeltSegment> segments = chunk.GetNativeArray(BeltSegmentsHandle);
                BufferAccessor<BeltItem> allItems = chunk.GetBufferAccessor(BeltItemsHandle);
                for (int chunkIdx = 0; chunkIdx != chunk.Count; chunkIdx++)
                {
                    BeltSegment segment = segments[chunkIdx];
                    if (!segment.Rendered)
                        continue;
                    DynamicBuffer<BeltItem> items = allItems[chunkIdx];
                    float dist = 0;
                    int2 dropPoint = segment.DropPoint;
                    int2 revDir = segment.RevDir;
                    for (int i = 0; i < items.Length; i++)
                    {
                        BeltItem item = items[i];
                        dist += item.Distance / (float) Settings.BeltDistanceSubDiv;
                        float3 computePosition =
                            new float3(dropPoint.x + dist * revDir.x, 0, dropPoint.y + dist * revDir.y);
                        byte itemTypeIndex = (byte) (item.Type - 1);
                        ref int instanceIndexRef = ref itemPositionArrayIndexPointer.ElementAt(itemTypeIndex);
                        int index = Interlocked.Increment(ref instanceIndexRef);
                        _renderedItemPositionsPointer[itemTypeIndex][index] = computePosition;
                    }
                }
            }
        }
    }
}