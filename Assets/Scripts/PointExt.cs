using Unity.Mathematics;

namespace Automation
{
    static class PointExt
    {
        public static int2 Dir(int2 a, int2 b) => new int2( (int) math.sign(b.x - a.x), (int) math.sign(b.y - a.y));
    }
}