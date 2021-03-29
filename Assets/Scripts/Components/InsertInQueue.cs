using Unity.Entities;
using Unity.Mathematics;

namespace Automation
{
    struct InsertInQueue : IBufferElementData
    {
        public readonly BeltItem Item;
        public readonly int2 DropPoint;

        public InsertInQueue(BeltItem item, int2 dropPoint)
        {
            Item = item;
            DropPoint = dropPoint;
        }
    }
}