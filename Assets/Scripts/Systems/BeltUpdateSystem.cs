using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Automation
{
    [UpdateAfter(typeof(CullingSystem))]
    class BeltUpdateSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            World.Settings settings = GetSingleton<World.Settings>();
            BufferFromEntity<InsertInQueue> queues = GetBufferFromEntity<InsertInQueue>();
            Dependency = Entities.ForEach(
                (Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref BeltItem item = ref items.ElementAt(i);
                        if (segment.Next == Entity.Null)
                        {
                            if (item.Distance > settings.BeltDistanceSubDiv)
                            {
                                item.Distance--;
                                break;
                            }

                            continue;
                        }

                        // there's a next segment
                        if (item.Distance > 0)
                        {
                            item.Distance--;
                            // Debug.Log("Move");
                        }
                        
                        if (item.Distance > 0)
                            break;

                        int2 dropPoint = segment.DropPoint;
                        queues[segment.Next].Add(new InsertInQueue(item, dropPoint));
                        items.RemoveAt(i);
                        if (i < items.Length)
                        {
                            ref BeltItem nextItem = ref items.ElementAt(i);
                            nextItem.Distance++;
                        }

                        i--;
                    }
                }).Schedule(Dependency);
            // ugly. use a parallel nativestream ? actual insertions probably need to be sequential anyway
            // _ecbSystem.AddJobHandleForProducer(Dependency);
            // ecbuffer.Playback(EntityManager);
            // _ecbSystem.pos
        }
    }
}