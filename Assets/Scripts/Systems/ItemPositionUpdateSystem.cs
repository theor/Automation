using Unity.Entities;
using Unity.Transforms;

namespace Automation
{
    // [UpdateAfter(typeof(InsertItemsInQueuesSystem))]
    // class ItemPositionUpdateSystem : SystemBase
    // {
    //     protected override void OnUpdate()
    //     {
    //         Entities.ForEach((Entity e, ref Translation ltw, in BeltItemVisual itemVisual) =>
    //         {
    //             ltw.Value = itemVisual.AccumulatedDistance;
    //         }).ScheduleParallel();
    //     }
    // }
}