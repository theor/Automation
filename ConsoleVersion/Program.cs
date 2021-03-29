using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Automation
{
    class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true, WriteIndented = true,
            AllowTrailingCommas = true,
            IgnoreNullValues = true,
            IgnoreReadOnlyProperties = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
        static void Main(string[] args)
        {
            // File.WriteAllText("../../../Cases/loop.json",JsonSerializer.Serialize(world, JsonOptions));
            var fileName =
                // "T"
                "loop"
                ;
            using(var d = new Driver(){LoadWorld =
                () =>
                    // Deserialize(fileName)
                    CreateTestWorld()
            })
                d.Start();
        }

        private static World Deserialize(string file)
        {
            World world;
            world = JsonSerializer.Deserialize<World>(File.ReadAllText("../../../Cases/" + Path.ChangeExtension(file, "json")), JsonOptions);
            if (world.Entities.Count == 0 || world.Entities.FirstOrDefault().Type != EntityType.None)
                world.Entities.Insert(0, default);
            if (world.Segments.Count == 0 || world.Segments.FirstOrDefault().Next != 0)
                world.Segments.Insert(0, default);
            return world;
        }

        static World CreateTestWorld()
        {
            return new World()
            {
                Entities =
                {
                    default,
                    new Entity(1, EntityType.A),
                    new Entity(2, EntityType.B),
                    new Entity(3, EntityType.Spawner),
                },
                WorldEntities =
                {
                    new WorldEntity(3, new Point(2,7))
                },
                Segments =
                {
                    default,
                    new BeltSegment // 1
                    {
                        Start = new Point(3,5),
                        End = new(12,5),
                        Next = 2,
                        Items = new List<BeltItem>
                        {
                            new(1, 2),
                            new (2, 3)
                        },
                    },
                    new BeltSegment // 2
                    {
                        Start = new(13,5),
                        End = new(13,10),
                        Next = 3,
                    },
                    new BeltSegment // 3
                    {
                        Start = new(13,11),
                        End = new(4,11),
                        Next = 4,
                    },
                    new BeltSegment // 3
                    {
                        Start = new(3,11),
                        End = new(3,6),
                        Next = 1,
                    },
                }
            };

        }
    }
}