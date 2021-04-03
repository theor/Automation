using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Automation
{
    struct BeltMerger : IComponentData
    {

    }
    struct BeltSplitter : IComponentData
    {
        public readonly int2 Start;
        public readonly int2 End;
        public readonly Entity Next1;
        public readonly Entity Next2;

        public BeltSplitter(int2 start, int2 end, Entity next1, Entity next2)
        {
            Start = start;
            End = end;
            Next1 = next1;
            Next2 = next2;
        }
    }
    struct BeltSegment : IComponentData
    {
        public static float3 FromI2(int2 i2) => new float3(i2.x, 0, i2.y);

        public int2 Start;
        public int2 End;
        public Entity Next;
        public Entity Prev;
        public bool Rendered;
        public ushort DistanceToInsertAtStart;

        public BeltSegment(int2 start, int2 end, Entity next = default) : this()
        {
            Start = start;
            End = end;
            Next = next;
        }

        [JsonIgnore] public readonly int2 Dir => PointExt.Dir(Start, End);
        [JsonIgnore] public readonly int2 RevDir => PointExt.Dir(End, Start);

        [JsonIgnore] public readonly int2 DropPoint => End + Dir;


        public readonly AABB AABB =>
            new AABB
            {
                Center = FromI2((Start + End) / 2),
                Extents = 0.5f * (FromI2(math.abs(End - Start))) + 1,
            };

        public void ComputeInsertionPoint(ref DynamicBuffer<BeltItem> items, ushort subdivCount)
        {
            var acc = 0;
            for (int i = 0; i < items.Length; i++)
                acc += items[i].Distance;

            var length = math.abs(DropPoint-Start) * subdivCount;
            DistanceToInsertAtStart = (ushort) (length.x + length.y - acc);
        }

        public override string ToString() => $"Segment {Start} -> {End}";
    }
}