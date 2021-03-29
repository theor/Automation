using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Automation
{
    [UpdateAfter(typeof(ItemSpawningCommandSystem))]
    class BeltUpdateSystem : SystemBase
    {
        private float _acc;
        private BeltUpdateCommandSystem _ecbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _ecbSystem = World.GetExistingSystem<BeltUpdateCommandSystem>();
        }

        protected override void OnUpdate()
        {
            _acc += Time.DeltaTime;
            // if (_acc > .5f)
            {
                var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
                _acc = 0;
                Dependency = Entities.ForEach((Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref var item = ref items.ElementAt(i);
                        if (item.Distance > 0)
                        {
                            item.Distance--;
                            Debug.Log("Move");
                            break;
                        }

                        if (segment.Next != Entity.Null)
                        {
                            int2 dropPoint = segment.DropPoint;
                            Debug.Log(string.Format("Insert {0} of {1} in queue {2}", item.Type, e.Index, segment.Next.Index));
                            ecb.AppendToBuffer(entityInQueryIndex, segment.Next, new InsertInQueue(item, dropPoint));
                            items.RemoveAt(i);
                            if (i < items.Length)
                            {
                                ref var nextItem = ref items.ElementAt(i);
                                nextItem.Distance++;
                            }
                        }
                    }
                }).ScheduleParallel(Dependency);
                
                Dependency = Entities
                    .ForEach(
                        (Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items,DynamicBuffer<InsertInQueue> toInsert, ref BeltSegment segment) =>
                        {
                            for (var index = 0; index < toInsert.Length; index++)
                            {
                                InsertInQueue insertInQueue = toInsert[index];
                                segment.InsertItem(ref items, insertInQueue.Item, insertInQueue.DropPoint);
                            }

                            toInsert.Clear();
                        }).ScheduleParallel(Dependency);
                _ecbSystem.AddJobHandleForProducer(Dependency);
                // _ecbSystem.pos
            }
        }
    }
    
    [UpdateAfter(typeof(BeltUpdateSystem))]
    class BeltUpdateCommandSystem : EntityCommandBufferSystem{}
    
}