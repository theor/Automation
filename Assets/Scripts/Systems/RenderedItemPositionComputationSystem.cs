using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Automation
{
    [UpdateBefore(typeof(CullingSystem))]
    class RenderedItemPositionComputationSystem : SystemBase
    {

        public NativeArray<float3> RenderedItemPositions;
        public JobHandle SetupDependency;
        private NativeArray<int> _itemPositionArrayIndex;
        private CullingSystem _cullingSystem;
        public int RenderedItemCount;

        protected override void OnCreate()
        {
            _cullingSystem = World.GetExistingSystem<CullingSystem>();
            _itemPositionArrayIndex = new NativeArray<int>(1, Allocator.Persistent);
            base.OnCreate();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if(RenderedItemPositions.IsCreated)
                RenderedItemPositions.Dispose();
            _itemPositionArrayIndex.Dispose();
        }

        protected override unsafe void OnUpdate()
         
        {
            if(!_cullingSystem.RenderedItemCount.IsCreated || _cullingSystem.RenderedItemCount[0] == 0)
                return;
            
            RenderedItemCount = _cullingSystem.RenderedItemCount[0];
            if (!RenderedItemPositions.IsCreated || RenderedItemCount != RenderedItemPositions.Length)
            {
                if (RenderedItemPositions.IsCreated)
                    RenderedItemPositions.Dispose();
                RenderedItemPositions = new NativeArray<float3>(RenderedItemCount, Allocator.Persistent);
            }
            
            float3* renderedItemPositionsPointer = (float3*)RenderedItemPositions.GetUnsafePtr();
            _itemPositionArrayIndex[0] = -1;
            int* itemPositionArrayIndexPointer = (int*)_itemPositionArrayIndex.GetUnsafePtr();

            int renderedItemCount = RenderedItemCount;
            World.Settings settings = GetSingleton<World.Settings>();
            SetupDependency = Dependency = Entities.ForEach((DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    if(!segment.Rendered)
                        return;
                    float dist = 0;
                    int2 dropPoint = segment.DropPoint;
                    int2 revDir = segment.RevDir;
                    ref int instanceIndexRef = ref UnsafeUtility.AsRef<int>(itemPositionArrayIndexPointer);
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref BeltItem item = ref items.ElementAt(i);
                        dist += item.Distance / (float) settings.BeltDistanceSubDiv;
                        float3 computePosition = new float3(dropPoint.x + dist * revDir.x, 0, dropPoint.y + dist * revDir.y);
                        int index = Interlocked.Increment(ref instanceIndexRef);
                        if(index < renderedItemCount)
                            renderedItemPositionsPointer[index] = computePosition;
                    }
                })
                .WithNativeDisableUnsafePtrRestriction(renderedItemPositionsPointer)
                .WithNativeDisableUnsafePtrRestriction(itemPositionArrayIndexPointer)
                .ScheduleParallel(Dependency);
        }
    }
}