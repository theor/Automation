using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Automation
{
    // class PrefabTag : MonoBehaviour, IConvertGameObjectToEntity
    // {
    //     
    // }
    class World : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public GameObject BeltPrefab;
        // public GameObject ItemPrefab;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
            // void Start()
        {
            // var w = Unity.Entities.World.DefaultGameObjectInjectionWorld;

            // var dstManager = w.EntityManager;
            var prefabEntity =
                // dstManager.crea
                // GameObjectConversionUtility.pre) //
                conversionSystem.GetPrimaryEntity(BeltPrefab);
            // w.GetExistingSystem<GameObjectConversionSystem>().GetPrimaryEntity(Prefab);
            // Debug.Log(prefabEntity);
            var beltSegmentEntity = //conversionSystem.CreateAdditionalEntity(this);
                dstManager.Instantiate(prefabEntity);
            var segment = new BeltSegment
            {
                Start = new int2(3, 5),
                End = new int2(12, 5),
            };
            dstManager.AddComponentData(beltSegmentEntity, segment);
            dstManager.SetComponentData(beltSegmentEntity, new Translation{Value=new float3(
                (segment.Start.x + segment.End.x)/2f, 0, (segment.Start.y+segment.End.y)/2f)});
            var items = dstManager.AddBuffer<BeltItem>(beltSegmentEntity);
            items.Add(new BeltItem(EntityType.A, 1));
            items.Add(new BeltItem(EntityType.B, 2));
            dstManager.SetName(beltSegmentEntity, segment.ToString());

            // RenderMeshUtility.AddComponents(beltSegmentEntity, dstManager, new RenderMeshDescription(Prefab.GetComponent<Renderer>(), Prefab.GetComponent<MeshFilter>().sharedMesh));
            var size = new float3(segment.End.x - segment.Start.x + 1, 1, segment.End.y - segment.End.y + 1);
            dstManager.AddComponentData(beltSegmentEntity, new NonUniformScale {Value = size});
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(BeltPrefab);
            // referencedPrefabs.Add(ItemPrefab);
        }
    }

    // [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    // class PrefabConverterDeclare : GameObjectConversionSystem
    // {
    //     protected override void OnUpdate()
    //     {
    //         Entities.ForEach((World prefabReference) =>
    //         {
    //             DeclareReferencedPrefab(prefabReference.Prefab);
    //         });
    //     }
    // }

    class ItemSpawningSystem : SystemBase
    {
        private ItemSpawningCommandSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystem<ItemSpawningCommandSystem>();
        }

        public struct SpawnedItemVisual : IComponentData
        {
            public Entity BeltSegment;
            public int BeltItemIndex;
        }
        
        protected override void OnUpdate()
        {
            var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.MultiPlayback);
            

            Entities.ForEach((Entity e, in SpawnedItemVisual v) =>
            {
                
                var b = EntityManager.GetBuffer<BeltItem>(v.BeltSegment);
                b.ElementAt(v.BeltItemIndex).Entity = e;
                entityCommandBuffer.RemoveComponent<SpawnedItemVisual>(e);
            }).WithoutBurst().Run();
            entityCommandBuffer.Playback(EntityManager);
            
            var prefab = GetSingletonEntity<Prefab>();

            var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
            _ecbSystem.AddJobHandleForProducer(Dependency = Entities
                .ForEach((Entity e, int entityInQueryIndex, DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                {
                    float dist = 0;
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref var item = ref items.ElementAt(i);
                        dist += item.Distance + 1;
                        var beltItemVisual = new BeltItemVisual
                        {
                            Type = item.Type,
                            AccumulatedDistance = segment.ComputePosition(dist)
                        };
                        if (item.Entity == Entity.Null)
                        {
                            var itemEntity = ecb.Instantiate(entityInQueryIndex, prefab);
                            ecb.AddComponent(entityInQueryIndex, itemEntity, new SpawnedItemVisual
                            {
                                BeltSegment = e,BeltItemIndex = i,
                            });

                            ecb.AddComponent(entityInQueryIndex, itemEntity, beltItemVisual);
                        }
                        else
                            ecb.SetComponent(entityInQueryIndex, item.Entity, beltItemVisual);
                    }
                })
                .ScheduleParallel(Dependency));
        }
    }

    [UpdateAfter(typeof(ItemSpawningSystem))]
    class ItemSpawningCommandSystem : EntityCommandBufferSystem
    {
    }

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

    [UpdateAfter(typeof(BeltUpdateSystem))]
    class ItemPositionUpdateSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity e, ref Translation ltw, in BeltItemVisual itemVisual) =>
            {
                ltw.Value = itemVisual.AccumulatedDistance;
            }).ScheduleParallel();
        }
    }
}