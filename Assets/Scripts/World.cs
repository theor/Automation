using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Automation
{
    class World : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public struct Prefabs:IComponentData
        {
            public Entity BeltPrefab;
            public Entity ItemPrefab;
            public Entity Item2Prefab;
        }
        public struct Settings:IComponentData
        {
            public ushort BeltDistanceSubDiv;
            public bool DebugDraw;
        }
        public GameObject BeltPrefab;
        public GameObject SplitterPrefab;
        public GameObject ItemPrefab;
        public GameObject Item2Prefab;
        [Min(1)]
        public int SubdivCount;
        public bool DebugDraw;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new Settings
            {
                BeltDistanceSubDiv = (ushort) SubdivCount,
                DebugDraw = DebugDraw,

            });

            var prefabEntity = conversionSystem.GetPrimaryEntity(BeltPrefab);
            var spltterPrefab = conversionSystem.GetPrimaryEntity(SplitterPrefab);
            var itemEntity = conversionSystem.GetPrimaryEntity(ItemPrefab);
            var item2Entity = conversionSystem.GetPrimaryEntity(Item2Prefab);
            // dstManager.AddComponent<BeltItemVisual>(itemEntity);
            // dstManager.AddComponent<BeltItemVisual>(item2Entity);
            dstManager.AddComponentData(entity, new Prefabs
            {
                BeltPrefab = prefabEntity,
                ItemPrefab = itemEntity,
                Item2Prefab = item2Entity,
            });
            var entities =
                // MakeSplitter(dstManager, prefabEntity, spltterPrefab,10);
                MakeSplitter(dstManager, prefabEntity, spltterPrefab,100);
                // Make3BeltsU(dstManager, prefabEntity, 2, 2);
                // Make3BeltsU(dstManager, prefabEntity, 100, 10000);
            // MakeT(dstManager, prefabEntity);
                // MakeT2(dstManager, prefabEntity);
            // for (var index = 0; index < entities.Length; index++)
            // {
            //     var e = entities[index];
            //     if (dstManager.HasComponent<BeltSegment>(e))
            //     {
            //         var segment = dstManager.GetComponentData<BeltSegment>(e);
            //         var dynamicBuffer = dstManager.GetBuffer<BeltItem>(e);
            //         segment.ComputeInsertionPoint(ref dynamicBuffer, (ushort) SubdivCount);
            //         dstManager.SetComponentData(e, segment);
            //
            //         var next = segment.Next;
            //         if (next != Entity.Null)
            //         {
            //             if (dstManager.HasComponent<BeltSegment>(next))
            //             {
            //                 var nextBeltSegment = dstManager.GetComponentData<BeltSegment>(next);
            //                 nextBeltSegment.Prev = e;
            //                 dstManager.SetComponentData(next, nextBeltSegment);
            //             }
            //         }
            //     }
            // }

            entities.Dispose();
        }

        private NativeArray<Entity> MakeSplitter(EntityManager dstManager, Entity prefabEntity, Entity splitterPrefab, int setupCount)
        {
            var entities = dstManager.Instantiate(prefabEntity, 3 * setupCount, Allocator.Temp);
            var spltterEntities = dstManager.Instantiate(splitterPrefab, setupCount, Allocator.Temp);
            for (int i = 0; i < setupCount; i++)
            {

                CreateSplitter(dstManager, spltterEntities[i],
                    new BeltSplitter(new int2(6,4*i), new int2(7,4*i),
                        entities[3*i+1],
                        entities[3*i+2]));
                // incoming
                CreateSegment(dstManager, entities[3*i],
                    new BeltSegment(new int2(0, 4*i), new int2(5, 4*i), spltterEntities[i]),
                    (EntityType.A, 1)
                    , (EntityType.A, 1)
                    , (EntityType.A, 1)
                    , (EntityType.B, 1)
                    , (EntityType.A, 1)
                    , (EntityType.A, 1)
                    , (EntityType.B, 1)
                    , (EntityType.A, 1)
                    , (EntityType.A, 1)
                    , (EntityType.B, 1)
                    , (EntityType.A, 1)
                    , (EntityType.A, 1)
                    , (EntityType.A, 1)
                    );
                // outcoming
                CreateSegment(dstManager, entities[3*i+1],
                    new BeltSegment(new int2(7, 4*i),new int2(10, 4*i)));
                CreateSegment(dstManager, entities[3*i+2],
                    new BeltSegment(new int2(7, 4*i+1),new int2(10, 4*i+1)));
            }
            return entities;
        }

        private NativeArray<Entity> MakeT(EntityManager dstManager, Entity prefabEntity)
        {
            var entities = dstManager.Instantiate(prefabEntity, 2, Allocator.Temp);
            CreateSegment(dstManager, entities[0],
                new BeltSegment(new int2(3, 5), new int2(12, 5), entities[1]),(EntityType.A, 1), (EntityType.A, 4));
            CreateSegment(dstManager, entities[1],
                new BeltSegment(new int2(13, 2), new int2(13, 10)), (EntityType.B, 8));
            return entities;
        }

        private NativeArray<Entity> MakeT2(EntityManager dstManager, Entity prefabEntity)
        {
            var entities = dstManager.Instantiate(prefabEntity, 2, Allocator.Temp);
            CreateSegment(dstManager, entities[0],
                new BeltSegment(new int2(3, 5), new int2(12, 5), entities[1])
                //,(EntityType.A, 1)
                //, (EntityType.A, 4)
                , (EntityType.B, 3),(EntityType.B, 1),(EntityType.B, 1),(EntityType.B, 1),(EntityType.B, 1)
                );
            CreateSegment(dstManager, entities[1],
                new BeltSegment(new int2(13, 5), new int2(13, 7))//,(EntityType.B, 1)
            );
            return entities;

        }

        private NativeArray<Entity> Make3BeltsU(EntityManager dstManager, Entity prefabEntity, int itemCount = 2, int beltCount = 2)
        {
            var entities = dstManager.Instantiate(prefabEntity, 2 + beltCount, Allocator.Temp);
            Entity first = entities[0];
            for (int i = 0; i < beltCount; i++)
            {
                CreateSegment(dstManager, entities[2+i],
                    new BeltSegment(new int2(-100*(beltCount-i)-1, 5),
                        new int2(-100*(beltCount-i-1), 5),
                        i < beltCount - 1 ? entities[2+i+1] : first)
                    , /*i != 0 ? null :*/ Enumerable.Range(0, itemCount).Select(x => (x % 2 == 0 ? EntityType.A : EntityType.B, (ushort) 2)).ToArray()
                );
            }
            CreateSegment(dstManager, entities[0], new BeltSegment(new int2(1, 5),
                new int2(1, 10),
                entities[1])
            );
            CreateSegment(dstManager, entities[1], new BeltSegment
            {
                Start = new int2(1, 11),
                End = new int2(4, 11),
                // Next = entities[3],
            });
            // CreateSegment(dstManager, new BeltSegment
            // {
            //     Start = new int2(3,11),
            //     End = new int2(3,6),
            //     Next = entities[0],
            // }, entities[3]);
            return entities;
        }

        private void CreateSplitter(EntityManager dstManager, Entity beltSegmentEntity, BeltSplitter segment)
        {
            dstManager.AddComponentData(beltSegmentEntity, segment);
            // dstManager.AddComponent<DisableRendering>(beltSegmentEntity);
            var dir = segment.End - segment.Start;
            dstManager.SetComponentData(beltSegmentEntity, new Translation
            {
                Value = new float3(segment.Start.x, .4f, segment.Start.y)
            });
            dstManager.SetName(beltSegmentEntity, "splitter");

            var yRot = GetRotationValue(dir);
            dstManager.AddComponentData(beltSegmentEntity, new RotationEulerXYZ {Value = new float3(math.PI/2f, (yRot+1) * math.PI/2f, 0)});
            // dstManager.AddComponentData(beltSegmentEntity, new NonUniformScale {Value = size});

        }

        private void CreateSegment(EntityManager dstManager, Entity beltSegmentEntity, BeltSegment segment,
            params (EntityType, ushort)[] beltItems)
        {
            dstManager.AddComponentData(beltSegmentEntity, segment);
            // dstManager.AddComponent<DisableRendering>(beltSegmentEntity);
            var length = math.max(math.abs(segment.End.x - segment.Start.x), math.abs(segment.End.y - segment.Start.y));
            var dir = segment.Dir;
            dstManager.SetComponentData(beltSegmentEntity, new Translation
            {
                Value = new float3(
                    (segment.End.x + segment.Start.x) / 2f, -.05f, (segment.End.y + segment.Start.y) / 2f)
            });
            var items = dstManager.AddBuffer<BeltItem>(beltSegmentEntity);
            if(beltItems != null)
                foreach (var beltItem in beltItems)
                    items.Add(new BeltItem(beltItem.Item1, (ushort) (beltItem.Item2*SubdivCount)));
            dstManager.SetName(beltSegmentEntity, "segment");

            var size = new float3(length+1, .1f, .9f);
            var yRot = GetRotationValue(dir);
            dstManager.AddComponentData(beltSegmentEntity, new RotationEulerXYZ {Value = new float3(0, yRot * math.PI/2f, 0)});
            dstManager.AddComponentData(beltSegmentEntity, new ShaderRotation() {Value = yRot});
            dstManager.AddComponentData(beltSegmentEntity, new NonUniformScale {Value = size});
        }

        private static int GetRotationValue(int2 dir)
        {
            return (dir.x, dir.y) switch
            {
                (1, 0) => 0,
                (-1,0) => 2,
                (0,1) => 1,
                (0,-1) => -1,
                _ => throw new NotImplementedException(),
            };
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(BeltPrefab);
            referencedPrefabs.Add(SplitterPrefab);
            referencedPrefabs.Add(ItemPrefab);
            referencedPrefabs.Add(Item2Prefab);
        }
    }
}