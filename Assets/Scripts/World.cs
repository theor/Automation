using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Automation
{
    class World : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public GameObject Prefab;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
            // void Start()
        {
            // var w = Unity.Entities.World.DefaultGameObjectInjectionWorld;

            // var dstManager = w.EntityManager;
            var prefabEntity =
                // dstManager.crea
                // GameObjectConversionUtility.pre) //
                conversionSystem.GetPrimaryEntity(Prefab);
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
            referencedPrefabs.Add(Prefab);
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

        protected override void OnUpdate()
        {
            var prefab = GetSingletonEntity<Prefab>();

            var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
            _ecbSystem.AddJobHandleForProducer(Entities
                .ForEach((Entity e, int entityInQueryIndex, BeltSegment segment, DynamicBuffer<BeltItem> items) =>
                {
                    float dist = 0;
                    for (int i = 0; i < items.Length; i++)
                    {
                        ref var item = ref items.ElementAt(i);
                        dist += item.Distance;
                        if (item.Entity == Entity.Null)
                        {
                            var itemEntity = ecb.Instantiate(entityInQueryIndex, prefab);

                            item.Entity = itemEntity;
                            ecb.AddComponent(entityInQueryIndex, itemEntity, new BeltItemVisual
                            {
                                Type = item.Type,
                                AccumulatedDistance = segment.ComputePosition(dist)
                            });
                        }
                        else
                            ecb.SetComponent(entityInQueryIndex, item.Entity, new BeltItemVisual
                            {
                                Type = item.Type,
                                AccumulatedDistance = segment.ComputePosition(dist)
                            });
                    }
                })
                .ScheduleParallel(Dependency));
        }
    }

    [UpdateAfter(typeof(ItemSpawningSystem))]
    class ItemSpawningCommandSystem : EntityCommandBufferSystem
    {
    }

    [UpdateAfter(typeof(BeltUpdateSystem))]
    class ItemPositionUpdateSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity e, BeltItemVisual itemVisual, ref Translation ltw) =>
            {
                ltw.Value = itemVisual.AccumulatedDistance;
            }).ScheduleParallel();
        }
    }

    [UpdateAfter(typeof(ItemSpawningCommandSystem))]
    class BeltUpdateSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Entity e, BeltSegment s, DynamicBuffer<BeltItem> items) =>
            {
                Debug.Log($"{e} {s.Start} -> {s.End} {items.Length}");
            }).Run();
        }
    }
}