using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Automation
{
    [UpdateAfter(typeof(ItemSpawningSystem))]
    class BeltUpdateSystem : SystemBase
    {
        private float _acc;
        // private BeltUpdateCommandSystem _ecbSystem;
        // public const ushort BeltDistanceSubDiv = 2;

        protected override void OnCreate()
        {
            base.OnCreate();
            // _ecbSystem = World.GetExistingSystem<BeltUpdateCommandSystem>();
        }

        protected override void OnUpdate()
        {
            var settings = GetSingleton<World.Settings>();
            _acc += Time.DeltaTime;
            // if (_acc > .5f)
            // var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
            // var ecbuffer = new EntityCommandBuffer(Allocator.TempJob, PlaybackPolicy.SinglePlayback);
            // var ecb = ecbuffer.AsParallelWriter();
            var queues = GetBufferFromEntity<InsertInQueue>();
            _acc = 0;
            Dependency = Entities.ForEach(
                (Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref var item = ref items.ElementAt(i);
                        if (segment.Next == Entity.Null)
                        {
                            if (item.Distance > settings.BeltDistanceSubDiv)
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
                        
                        if (item.Distance > 0)
                            break;

                        if (segment.Next != Entity.Null)
                        {
                            int2 dropPoint = segment.DropPoint;
                            queues[segment.Next].Add(new InsertInQueue(item, dropPoint));
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
                }).Schedule(Dependency);
            // ugly. use a parallel nativestream ? actual insertions probably need to be sequential anyway
            // _ecbSystem.AddJobHandleForProducer(Dependency);
            // ecbuffer.Playback(EntityManager);
            // _ecbSystem.pos
        }
    }
    
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