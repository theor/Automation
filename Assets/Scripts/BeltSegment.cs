using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.Entities;
using Unity.Mathematics;

namespace Automation
{
    static class PointExt
    {
        public static int2 Dir(int2 a, int2 b) => new int2( (int) math.sign(b.x - a.x), (int) math.sign(b.y - a.y));
    }

    struct BeltItemVisual : IComponentData
    {
        public EntityType Type;
        public float3 AccumulatedDistance;
    }
    struct BeltItem : IBufferElementData
    {
        public EntityType Type;
        public byte Distance;
        public Entity Entity;
        public byte AccumulatedDistance;

        public BeltItem(EntityType type, byte distance)
        {
            Type = type;
            Distance = distance;
            Entity = Entity.Null;
            AccumulatedDistance = default;
        }
    }

    struct BeltSegment : IComponentData
    {
        public int2 Start;
        public int2 End;
        public Entity Next;
        [JsonIgnore] public readonly int2 Dir => PointExt.Dir(Start, End);
        [JsonIgnore] public readonly int2 RevDir => PointExt.Dir(End, Start);

        [JsonIgnore] public readonly int2 DropPoint => End + Dir;

        public override string ToString() => $"Segment {Start} -> {End}";

        // public List<BeltItem> Items;

        // public void InsertItem(BeltItem segmentItem, int2 dropPoint, bool itemWillBeTickedAgain)
        // {
        //     segmentItem.Distance = 0;
        //     var p = End;
        //     var d = RevDir;
        //     int itemIdx = 0;
        //     while (!p.Equals(dropPoint))
        //     {
        //         p += d;
        //         Items ??= new List<BeltItem>();
        //         if (itemIdx < Items.Count)
        //         {
        //             if (Items[itemIdx].Distance == segmentItem.Distance)
        //             {
        //                 segmentItem.Distance = 0;
        //                 itemIdx++;
        //                 continue;
        //             }
        //         }
        //         segmentItem.Distance++;
        //     }
        //
        //     if (itemWillBeTickedAgain)
        //         segmentItem.Distance++;
        //     // if not last item, patch next one
        //     if (itemIdx < Items.Count)
        //     {
        //         var i = Items[itemIdx];
        //         i.Distance = (byte) (i.Distance - segmentItem.Distance - 1);
        //         Items[itemIdx] = i;
        //     }
        //     Items.Insert(itemIdx, segmentItem);
        // }
        public readonly float3 ComputePosition(float dist)
        {
            return new float3(DropPoint.x + dist * RevDir.x, 1, DropPoint.y + dist * RevDir.y);
        }
    }

    public enum EntityType : byte
    {
        None,
        A,
        B,
        C,
        Spawner
    }
}