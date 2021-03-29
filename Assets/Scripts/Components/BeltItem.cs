using Unity.Entities;

namespace Automation
{
    struct BeltItem : IBufferElementData
    {
        public EntityType Type;
        public byte Distance;
        public byte SubDistance;
        public Entity Entity;
        public byte AccumulatedDistance;

        public BeltItem(EntityType type, byte distance)
        {
            Type = type;
            Distance = distance;
            SubDistance = 0;
            Entity = Entity.Null;
            AccumulatedDistance = default;
        }
    }
}