using System.Collections.Generic;
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
            var s2 = CreateSegment(dstManager, prefabEntity, new BeltSegment
            {
                Start = new int2(3, 5),
                End = new int2(12, 5),
            }, 
(EntityType.A, 1),(EntityType.B, 2));
            CreateSegment(dstManager, prefabEntity, new BeltSegment
            {
                Start = new int2(13, 5),
                End = new int2(13, 10),
                Next = s2,
            });
        }

        private static Entity CreateSegment(EntityManager dstManager, Entity prefabEntity, BeltSegment segment, params (EntityType, byte)[] beltItems)
        {
            var beltSegmentEntity = dstManager.Instantiate(prefabEntity);
            dstManager.AddComponentData(beltSegmentEntity, segment);
            dstManager.SetComponentData(beltSegmentEntity, new Translation
            {
                Value = new float3(
                    (segment.Start.x + segment.End.x) / 2f, 0, (segment.Start.y + segment.End.y) / 2f)
            });
            var items = dstManager.AddBuffer<BeltItem>(beltSegmentEntity);
            foreach (var beltItem in beltItems) items.Add(new BeltItem(beltItem.Item1, beltItem.Item2));
            dstManager.SetName(beltSegmentEntity, segment.ToString());

            // RenderMeshUtility.AddComponents(beltSegmentEntity, dstManager, new RenderMeshDescription(Prefab.GetComponent<Renderer>(), Prefab.GetComponent<MeshFilter>().sharedMesh));
            var size = new float3(segment.End.x - segment.Start.x + 1, 1, segment.End.y - segment.Start.y + 1);
            dstManager.AddComponentData(beltSegmentEntity, new NonUniformScale {Value = size});
            return beltSegmentEntity;
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(BeltPrefab);
        }
    }
}