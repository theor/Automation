using Unity.Entities;

namespace Automation
{
    struct BeltItem : IBufferElementData
    {
        public EntityType Type;
        public ushort Distance;

        public BeltItem(EntityType type, ushort distance)
        {
            Type = type;
            Distance = distance;
        }
    }
}