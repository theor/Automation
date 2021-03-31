using Unity.Entities;

namespace Automation
{
    [UpdateAfter(typeof(BeltUpdateSystem))]
    class InsertItemsInQueuesSystem : SystemBase
    {

        protected override void OnUpdate()
        {
            
            var settings = GetSingleton<World.Settings>();
            Dependency = Entities
                .ForEach(
                    (Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items,DynamicBuffer<InsertInQueue> toInsert, ref BeltSegment segment) =>
                    {
                        for (var index = 0; index < toInsert.Length; index++)
                        {
                            InsertInQueue insertInQueue = toInsert[index];
                            segment.InsertItem(in settings, ref items, insertInQueue.Item, insertInQueue.DropPoint);
                        }

                        toInsert.Clear();
                    })
                // .WithoutBurst()
                .ScheduleParallel(Dependency);
        }
    }
}