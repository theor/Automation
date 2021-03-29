using System;
using System.Drawing;

namespace Automation
{
    class Driver: GameConsole
    {
        public bool Paused { get; set; } = true;
        private World _world;
        private TimeSpan _prev;
        private TimeSpan _acc;


        private const int FontSize = 16;
        public Driver():base(64,32, fontwidth:FontSize, fontheight:FontSize, font:"Courier New")
        {
        }


        public override bool OnUserCreate()
        {
            _world = LoadWorld();
            return true;
        }

        public Func<World> LoadWorld;
        public override bool OnUserUpdate(TimeSpan elapsedTime)
        {
            var delta = elapsedTime - _prev;
            _prev = elapsedTime;
            _acc += delta;
            if (GetKeyState(ConsoleKey.R).Pressed)
                _world = LoadWorld();
            if (GetKeyState(ConsoleKey.F).Pressed)
                Paused = !Paused;
            if ((!Paused && _acc.TotalMilliseconds > 60) || GetKeyState(ConsoleKey.Spacebar).Pressed)
            {
                Tick();
                _acc = TimeSpan.Zero;
            }
            for (var index = 1; index < _world.Segments.Count; index++)
            {
                var segment = _world.Segments[index];
                var d = segment.RevDir;
                var p = segment.End;
                var end = segment.Start;
                end.Offset(d);
                int dist = 0;
                int itemIdx = 0;
                while (p != end)
                {
                    char c = (d.X,d.Y) switch
                    {
                        (1,0) => '<',
                        (-1,0) => '>',
                        (0,1) => '^',
                        _ => 'v',
                    };
                    var attrs = (short) (index % 2 == 0 ? COLOR.BG_GREY : COLOR.BG_DARK_GREY);
                    attrs |= (short) COLOR.FG_WHITE;
                    if (segment.Items != null && itemIdx < segment.Items.Count)
                    {
                        var nextItem = segment.Items[itemIdx];
                        if (nextItem.Distance == dist)
                        {
                            c = _world.Entities[(int) nextItem.ItemID].Type.ToString()[0];
                            attrs ^= (short) COLOR.FG_WHITE;
                            attrs |= (short) COLOR.FG_RED;

                            itemIdx++;
                            dist = 0;
                        }
                        else
                            dist++;
                    }

                    SetChar(p.X, p.Y, c, attrs);
                    p.Offset(d);
                }
            }

            foreach (var worldEntity in _world.WorldEntities)
            {
                Entity e = _world.Entities[(int) worldEntity.Id];
                char c = e.Type switch
                {
                    EntityType.Spawner => 'S',
                    _ => '?',
                };
                SetChar(worldEntity.Pos.X, worldEntity.Pos.Y, c);
            }

            return true;
        }

        private void Tick()
        {
            foreach (var worldEntity in _world.WorldEntities)
            {
                var e = _world.Entities[(int) worldEntity.Id];
                switch (e.Type)
                {
                    case EntityType.Spawner:
                        break;
                }
            }

            for (var index = 1; index < _world.Segments.Count; index++)
            {
                var segment = _world.Segments[index];
                if (segment.Items != null)
                {
                    int itemIdx = 0;
                    while (itemIdx < segment.Items.Count)
                    {
                        var segmentItem = segment.Items[itemIdx];
                        if (segmentItem.Distance > 0)
                        {
                            segmentItem.Distance--;
                            segment.Items[itemIdx] = segmentItem;
                            break;
                        }

                        if (segment.Next != 0)
                        {
                            Point dropPoint = segment.DropPoint;
                            var worldSegment = _world.Segments[segment.Next];
                            worldSegment.InsertItem(segmentItem, dropPoint, segment.Next > index);
                            _world.Segments[segment.Next] = worldSegment;
                            segment.Items.RemoveAt(itemIdx);
                            if (itemIdx < segment.Items.Count)
                            {
                                var nextItem = segment.Items[itemIdx];
                                nextItem.Distance++;
                                segment.Items[itemIdx] = nextItem;
                            }

                            itemIdx--;
                        }

                        itemIdx++;
                    }
                }
            }
        }
    }
}