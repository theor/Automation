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
        public GameObject BeltPrefab;
        // public GameObject ItemPrefab;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            var prefabEntity = conversionSystem.GetPrimaryEntity(BeltPrefab);
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
                Next = entities[3],
            }, entities[2]);
            CreateSegment(dstManager, new BeltSegment
            {
                Start = new int2(3,11),
                End = new int2(3,6),
                Next = entities[0],
            }, entities[3]);
            entities.Dispose();
        }

        private static Entity CreateSegment(EntityManager dstManager, BeltSegment segment, Entity beltSegmentEntity, params (EntityType, byte)[] beltItems)
        {
            dstManager.AddComponentData(beltSegmentEntity, segment);
            var dx = math.abs(segment.End.x - segment.Start.x);
            var dz = math.abs(segment.End.y - segment.Start.y);
            dstManager.SetComponentData(beltSegmentEntity, new Translation
            {
                Value = new float3(
                    (segment.End.x + segment.Start.x) / 2f, 0, (segment.End.y + segment.Start.y) / 2f)
            });
            var items = dstManager.AddBuffer<BeltItem>(beltSegmentEntity);
            foreach (var beltItem in beltItems) items.Add(new BeltItem(beltItem.Item1, beltItem.Item2));
            dstManager.SetName(beltSegmentEntity, segment.ToString());

            // RenderMeshUtility.AddComponents(beltSegmentEntity, dstManager, new RenderMeshDescription(Prefab.GetComponent<Renderer>(), Prefab.GetComponent<MeshFilter>().sharedMesh));
            var size = new float3(dx+1, 1, dz + 1);
            dstManager.AddComponentData(beltSegmentEntity, new NonUniformScale {Value = size});
            dstManager.AddBuffer<InsertInQueue>(beltSegmentEntity);
            return beltSegmentEntity;
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(BeltPrefab);
        }
    }
}