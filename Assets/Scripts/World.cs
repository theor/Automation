using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
        }
        public GameObject BeltPrefab;
        public GameObject ItemPrefab;
        public GameObject Item2Prefab;
        [Min(1)]
        public int SubdivCount;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new Settings {BeltDistanceSubDiv = (ushort) SubdivCount});
            
            var prefabEntity = conversionSystem.GetPrimaryEntity(BeltPrefab);
            var itemEntity = conversionSystem.GetPrimaryEntity(ItemPrefab);
            var item2Entity = conversionSystem.GetPrimaryEntity(Item2Prefab);
            dstManager.AddComponentData(entity, new Prefabs
            {
                BeltPrefab = prefabEntity,
                ItemPrefab = itemEntity,
                Item2Prefab = item2Entity,
            });
            
            // MakeT(dstManager, prefabEntity);
            Make3BeltsU(dstManager, prefabEntity, 2, 5);
        }

        private void MakeT(EntityManager dstManager, Entity prefabEntity)
        {
            var entities = dstManager.Instantiate(prefabEntity, 2, Allocator.Temp);
            CreateSegment(dstManager, entities[0],
                new BeltSegment
                {
                    Start = new int2(3, 5),
                    End = new int2(12, 5),
                    Next = entities[1],
                },(EntityType.A, 1), (EntityType.A, 4));
            CreateSegment(dstManager, entities[1],
                new BeltSegment
                {
                    Start = new int2(13, 2),
                    End = new int2(13, 10),
                }, (EntityType.B, 8));
            entities.Dispose();
        }

        private void Make3BeltsU(EntityManager dstManager, Entity prefabEntity, int itemCount = 2, int beltCount = 2)
        {
            var entities = dstManager.Instantiate(prefabEntity, 2 + beltCount, Allocator.Temp);
            Entity first = entities[0];
            for (int i = 0; i < beltCount; i++)
            {
                CreateSegment(dstManager, entities[2+i],
                    new BeltSegment
                    {
                        Start = new int2(-5*(beltCount-i)-1, 5),
                        End = new int2(-5*(beltCount-i-1), 5),
                        Next = i < beltCount - 1 ? entities[2+i+1] : first,
                    }, /*i != 0 ? null :*/ Enumerable.Range(0, itemCount).Select(x => (x % 2 == 0 ? EntityType.A : EntityType.B, (ushort) 2)).ToArray()
                );
            }
            CreateSegment(dstManager, entities[0], new BeltSegment
            {
                Start = new int2(1, 5),
                End = new int2(1, 10),
                Next = entities[1],
            });
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
            entities.Dispose();
        }

        private void CreateSegment(EntityManager dstManager, Entity beltSegmentEntity, BeltSegment segment,
            params (EntityType, ushort)[] beltItems)
        {
            dstManager.AddComponentData(beltSegmentEntity, segment);
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

            // RenderMeshUtility.AddComponents(beltSegmentEntity, dstManager, new RenderMeshDescription(Prefab.GetComponent<Renderer>(), Prefab.GetComponent<MeshFilter>().sharedMesh));
            var size = new float3(length+1, .1f, 1);
            var yRot = (dir.x, dir.y) switch
            {
                (1, 0) => 0,
                (-1,0) => 2,
                (0,1) => 1,
                (0,-1) => -1,
            };
            dstManager.AddComponentData(beltSegmentEntity, new RotationEulerXYZ {Value = new float3(0, yRot * math.PI/2f, 0)});
            dstManager.AddComponentData(beltSegmentEntity, new ShaderRotation() {Value = yRot});
            dstManager.AddComponentData(beltSegmentEntity, new NonUniformScale {Value = size});
            dstManager.AddBuffer<InsertInQueue>(beltSegmentEntity);
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(BeltPrefab);
            referencedPrefabs.Add(ItemPrefab);
            referencedPrefabs.Add(Item2Prefab);
        }
    }
}