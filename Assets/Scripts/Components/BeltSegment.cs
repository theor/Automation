using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Automation
{
    struct BeltSegment : IComponentData
    {
        public int2 Start;
        public int2 End;
        public Entity Next;
        public bool Rendered;
        [JsonIgnore] public readonly int2 Dir => PointExt.Dir(Start, End);
        [JsonIgnore] public readonly int2 RevDir => PointExt.Dir(End, Start);

        [JsonIgnore] public readonly int2 DropPoint => End + Dir;

        public override string ToString() => $"Segment {Start} -> {End}";

        public void InsertItem(in World.Settings settings, ref DynamicBuffer<BeltItem> items, BeltItem segmentItem,
            int2 dropPoint)
        {
            segmentItem.Distance = 0;
            var targetDist = math.abs(dropPoint.x - DropPoint.x + dropPoint.y - DropPoint.y) * settings.BeltDistanceSubDiv;
            int itemIdx = 0;
            int iter = items.Length + 2;
            var dist = 0;
            // Debug.Log($"Insert start in {Start} -> {End} target {dropPoint} dir {Dir} revdir {RevDir} targetDist {targetDist}");
            while (dist != targetDist)
            {
                if(iter-- <= 0)
                    throw new NotImplementedException();
                var delta = (ushort) (targetDist - dist);
                if (itemIdx < items.Length)
                {
                    ref var item = ref items.ElementAt(itemIdx);
                    if (item.Distance + dist < targetDist)
                    {
                        dist += item.Distance;
                        itemIdx++;
                    }
                    else
                    {
                        segmentItem.Distance = delta;
                        item.Distance -= segmentItem.Distance;
                        break;
                    }
                }
                else
                {
                    segmentItem.Distance = delta;
                    break;
                }
            }
        
            // if (itemWillBeTickedAgain)
            //     segmentItem.Distance++;
            // if not last item, patch next one
            // if (itemIdx < Items.Count)
            // {
            //     var i = Items[itemIdx];
            //     i.Distance = (byte) (i.Distance - segmentItem.Distance - 1);
            //     Items[itemIdx] = i;
            // }
            items.Insert(itemIdx, segmentItem);
        }
        public readonly float3 ComputePosition(float dist)
        {
            return new float3(DropPoint.x + (dist) * RevDir.x, 0, DropPoint.y + (dist) * RevDir.y);
        }
    }
}