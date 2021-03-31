using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Automation
{
    // class ItemSpawningSystem : SystemBase
    // {
    //     private EntityQuery _getEntityQuery;
    //     public NativeArray<int> _count;
    //     
    //     protected override void OnCreate()
    //     {
    //         _getEntityQuery = GetEntityQuery(ComponentType.ReadWrite<BeltSegment>());
    //         _count = new NativeArray<int>(1, Allocator.Persistent);
    //     }
    //
    //     protected override void OnDestroy()
    //     {
    //         base.OnDestroy();
    //         _count.Dispose();
    //     }
    //
    //     protected override unsafe void OnUpdate()
    //     {
    //         _count[0] = 0;
    //         var countPtr = (int*)_count.GetUnsafePtr();
    //         Dependency =
    //             Entities
    //             .ForEach((Entity e, int nativeThreadIndex, int entityInQueryIndex, DynamicBuffer<BeltItem> items,
    //                 ref BeltSegment segment) =>
    //             {
    //                 segment.Rendered = segment.Start.x > -200;
    //                 if(segment.Rendered)
    //                     Interlocked.Add(ref UnsafeUtility.AsRef<int>(countPtr), items.Length);
    //             })
    //             .WithNativeDisableUnsafePtrRestriction(countPtr)
    //             .ScheduleParallel(Dependency);
    //     }
    // }
}