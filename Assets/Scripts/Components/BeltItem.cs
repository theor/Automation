using Unity.Entities;

namespace Automation
{
    struct BeltItem : IBufferElementData
    {
        public ItemType Type;
        public ushort Distance;

        public BeltItem(ItemType type, ushort distance)
        {
            Type = type;
            Distance = distance;
        }
    }
}