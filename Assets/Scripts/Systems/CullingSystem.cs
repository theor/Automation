using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using FrustumPlanes = Unity.Rendering.FrustumPlanes;

namespace Automation
{
    // [UpdateInGroup(typeof(PresentationSystemGroup))]
    class CullingSystem : SystemBase
    {
        private Vector3[] _corners;
        private NativeArray<float4> planes;

        public NativeArray<int> RenderedItemCount;
        protected override void OnCreate()
        {
            _corners = new Vector3[4];
            planes = new NativeArray<float4>(6, Allocator.Persistent);
            RenderedItemCount = new NativeArray<int>(1, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            planes.Dispose();
            RenderedItemCount.Dispose();
        }


        protected override unsafe void OnUpdate()
        {
            RenderedItemCount[0] = 0;
            var countPtr = (int*)RenderedItemCount.GetUnsafePtr();
            var camera = Camera.main;
            var camPos = camera.transform.position;
            camera.CalculateFrustumCorners(new Rect(0,0,1,1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, _corners);
            for (int i = 0; i < 4; i++)
            {
                var worldSpaceCorner = camera.transform.TransformVector(_corners[i]);
                Debug.DrawRay(camPos, worldSpaceCorner, Color.blue);
            }

            var cullingPlanes = planes;
            FrustumPlanes.FromCamera(camera, cullingPlanes);
            Entities.ForEach((DynamicBuffer<BeltItem> items, ref BeltSegment s) =>
                {
                    var sAABB = s.AABB;
                    s.Rendered = FrustumPlanes.Intersect(cullingPlanes, sAABB) != FrustumPlanes.IntersectResult.Out;
                    if(s.Rendered)
                        Interlocked.Add(ref UnsafeUtility.AsRef<int>(countPtr), items.Length);
                    // Color c = FrustumPlanes.Intersect(cullingPlanes, sAABB) switch
                    // {
                    //     FrustumPlanes.IntersectResult.Out => Color.black,
                    //     FrustumPlanes.IntersectResult.In => Color.white,
                    //     FrustumPlanes.IntersectResult.Partial => Color.cyan,
                    // };
                    // Debug.DrawLine(sAABB.Min, sAABB.Max, c);
                    // Debug.DrawLine(FromI2(s.Start, 1), FromI2(s.End, 1), c);
                })
                .WithNativeDisableUnsafePtrRestriction(countPtr)
                .WithNativeDisableParallelForRestriction(cullingPlanes)
                .ScheduleParallel();
        }

        private static Vector3 FromI2(int2 i2, float y = 0)
        {
            return new Vector3(i2.x, y, i2.y);
        }
    }
}