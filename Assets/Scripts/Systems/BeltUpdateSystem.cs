using Unity.Collections;
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
        public const ushort BeltDistanceSubDiv = 16;

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
                // var ecbuffer = new EntityCommandBuffer(Allocator.TempJob, PlaybackPolicy.SinglePlayback);
                // var ecb = ecbuffer.AsParallelWriter();
                _acc = 0;
                Dependency = Entities.ForEach((Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref var item = ref items.ElementAt(i);
                        if (segment.Next == Entity.Null)
                        {
                            if (item.Distance > BeltDistanceSubDiv)
                            {
                                item.Distance--;
                                break;
                            }
                            continue;
                        }
                        if (item.Distance > 0)
                        {
                            item.Distance--;
                            // Debug.Log("Move");
                        }
                        if(item.Distance > 0)
                            break;

                        if (segment.Next != Entity.Null)
                        {
                            int2 dropPoint = segment.DropPoint;
                            // Debug.Log(string.Format("Insert {0} of {1} in queue {2}", item.Type, e.Index, segment.Next.Index));
                            ecb.AppendToBuffer(entityInQueryIndex, segment.Next, new InsertInQueue(item, dropPoint));
                            items.RemoveAt(i);
                            if (i < items.Length)
                            {
                                ref var nextItem = ref items.ElementAt(i);
                                nextItem.Distance++;
                                // nextItem.SubDistance+= item.SubDistance;
                            }

                            i--;
                        }
                    }
                }).ScheduleParallel(Dependency);
                _ecbSystem.AddJobHandleForProducer(Dependency);
                // ecbuffer.Playback(EntityManager);
                // _ecbSystem.pos
            }
        }
    }
    
    [UpdateAfter(typeof(BeltUpdateSystem))]
    class BeltUpdateCommandSystem : EntityCommandBufferSystem{}


    [UpdateAfter(typeof(BeltUpdateCommandSystem))]
    class InsertItemsInQueuesSystem : SystemBase
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
                    })
                // .WithoutBurst()
                .ScheduleParallel(Dependency);
        }
    }

}