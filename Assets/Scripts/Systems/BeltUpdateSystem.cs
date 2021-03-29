using Unity.Entities;

namespace Automation
{
    [UpdateAfter(typeof(ItemSpawningCommandSystem))]
    class BeltUpdateSystem : SystemBase
    {
        private float _acc;

        protected override void OnUpdate()
        {
            _acc += Time.DeltaTime;
            if (_acc > .5f)
            {
                _acc = 0;
                Dependency = Entities.ForEach((Entity e, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref var item = ref items.ElementAt(i);
                        if (item.Distance > 0)
                        {
                            item.Distance--;
                            break;
                        }

                        // if (segment.Next != Entity.Null)
                        // {
                        //     int2 dropPoint = segment.DropPoint;
                        //     var worldSegment = _world.Segments[segment.Next];
                        //     worldSegment.InsertItem(segmentItem, dropPoint);//, segment.Next > index);
                        //     _world.Segments[segment.Next] = worldSegment;
                        //     segment.Items.RemoveAt(itemIdx);
                        //     if (itemIdx < segment.Items.Count)
                        //     {
                        //         var nextItem = segment.Items[itemIdx];
                        //         nextItem.Distance++;
                        //         segment.Items[itemIdx] = nextItem;
                        //     }
                        //
                        //     // itemIdx--;
                        // }
                    }
                }).ScheduleParallel(Dependency);
            }
        }
    }
}