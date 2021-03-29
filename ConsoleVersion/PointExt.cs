using System;
using System.Drawing;

namespace Automation
{
    static class PointExt
    {
        public static Point Dir(Point a, Point b) => new(Math.Sign(b.X - a.X), Math.Sign(b.Y - a.Y));
    }
}