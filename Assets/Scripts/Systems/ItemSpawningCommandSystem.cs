using Unity.Entities;

namespace Automation
{
    [UpdateAfter(typeof(ItemSpawningSystem))]
    class ItemSpawningCommandSystem : EntityCommandBufferSystem
    {
    }
}