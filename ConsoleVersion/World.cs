using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Automation
{
    class World
    {
        [JsonInclude]
        public List<Entity> Entities = new List<Entity>();
        public List<WorldEntity> WorldEntities = new List<WorldEntity>();
        public List<BeltSegment> Segments = new List<BeltSegment>();
        // public int GetComponent<T>(ulong Id)
    }

    public interface IComponentData{}

    struct SpawnerData : IComponentData
    {
        public byte Timer;
        public EntityType SpawnType;
    }

    struct WorldEntity
    {
        public ulong Id;
        public Point Pos;

        public WorldEntity(ulong id, Point pos)
        {
            Id = id;
            Pos = pos;
        }
    }

    struct BeltItem
    {
        public ulong ItemID;
        public byte Distance;

        public BeltItem(ulong itemId, byte distance)
        {
            ItemID = itemId;
            Distance = distance;
        }
    }
    struct BeltSegment
    {
        public Point Start;
        public Point End;
        public int Next;
        [JsonIgnore]
        public Point Dir => PointExt.Dir(Start, End);
        [JsonIgnore]
        public Point RevDir => PointExt.Dir(End, Start);

        [JsonIgnore]
        public Point DropPoint
        {
            get
            {
                var end = End;
                end.Offset(Dir);
                return end;
            }
        }

        public List<BeltItem> Items;

        public void InsertItem(BeltItem segmentItem, Point dropPoint, bool itemWillBeTickedAgain)
        {
            segmentItem.Distance = 0;
            var p = End;
            var d = RevDir;
            int itemIdx = 0;
            while (p != dropPoint)
            {
                p.Offset(d);
                Items ??= new List<BeltItem>();
                if (itemIdx < Items.Count)
                {
                    if (Items[itemIdx].Distance == segmentItem.Distance)
                    {
                        segmentItem.Distance = 0;
                        itemIdx++;
                        continue;
                    }
                }
                segmentItem.Distance++;
            }

            if (itemWillBeTickedAgain)
                segmentItem.Distance++;
            // if not last item, patch next one
            if (itemIdx < Items.Count)
            {
                var i = Items[itemIdx];
                i.Distance = (byte) (i.Distance - segmentItem.Distance - 1);
                Items[itemIdx] = i;
            }
            Items.Insert(itemIdx, segmentItem);
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
    struct Entity
    {
        public ulong Id;
        public EntityType Type;

        public Entity(ulong id, EntityType type)
        {
            Id = id;
            Type = type;
        }
    }
}