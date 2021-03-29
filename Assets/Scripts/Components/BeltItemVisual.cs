using Unity.Entities;
using Unity.Mathematics;

namespace Automation
{
    struct BeltItemVisual : IComponentData
    {
        public EntityType Type;
        public float3 AccumulatedDistance;
    }
}