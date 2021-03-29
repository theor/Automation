using System.Collections.Generic;
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
        }
        public GameObject BeltPrefab;
        public GameObject ItemPrefab;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            var prefabEntity = conversionSystem.GetPrimaryEntity(BeltPrefab);
            var itemEntity = conversionSystem.GetPrimaryEntity(ItemPrefab);
            dstManager.AddComponentData(entity, new Prefabs
            {
                BeltPrefab = prefabEntity,
                ItemPrefab = itemEntity,
            });
            var entities =dstManager.Instantiate(prefabEntity, 4, Allocator.Temp);
            CreateSegment(dstManager, new BeltSegment
            {
                Start = new int2(3, 5),
                End = new int2(12, 5),
                Next = entities[1],
            }, entities[0], 
(EntityType.A, 1),(EntityType.B, 2));
            CreateSegment(dstManager, new BeltSegment
            {
                Start = new int2(13, 5),
                End = new int2(13, 10),
                Next = entities[2],
            }, entities[1]);
            CreateSegment(dstManager, new BeltSegment
            {
                Start = new int2(13,11),
                End = new int2(4,11),
                // Next = entities[3],
            }, entities[2]);
            // CreateSegment(dstManager, new BeltSegment
            // {
            //     Start = new int2(3,11),
            //     End = new int2(3,6),
            //     Next = entities[0],
            // }, entities[3]);
            entities.Dispose();
        }

        private static Entity CreateSegment(EntityManager dstManager, BeltSegment segment, Entity beltSegmentEntity, params (EntityType, byte)[] beltItems)
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
            foreach (var beltItem in beltItems)
                items.Add(new BeltItem(beltItem.Item1, (ushort) (beltItem.Item2*BeltUpdateSystem.BeltDistanceSubDiv)));
            dstManager.SetName(beltSegmentEntity, segment.ToString());

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
            return beltSegmentEntity;
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(BeltPrefab);
            referencedPrefabs.Add(ItemPrefab);
        }
    }
}