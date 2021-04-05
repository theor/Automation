using System;
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
    [UpdateAfter(typeof(BeltSplitterUpdateSystem))]
    class CullingSystem : SystemBase
    {
        private Vector3[] _corners;
        private NativeArray<float4> planes;

        public NativeArray<int> RenderedItemCount;
        protected override void OnCreate()
        {
            _corners = new Vector3[4];
            planes = new NativeArray<float4>(6, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            planes.Dispose();
            if(RenderedItemCount.IsCreated)
                RenderedItemCount.Dispose();
        }


        protected override unsafe void OnUpdate()
        {
            if(!RenderedItemCount.IsCreated)
                RenderedItemCount = new NativeArray<int>(2, Allocator.Persistent);
            else
                for (var index = 0; index < RenderedItemCount.Length; index++)
                    RenderedItemCount[index] = 0;

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
            Dependency = Entities.ForEach((DynamicBuffer<BeltItem> items, ref BeltSegment s) =>
                {
                    var sAABB = s.AABB;
                    s.Rendered = FrustumPlanes.Intersect(cullingPlanes, sAABB) != FrustumPlanes.IntersectResult.Out;
                    if (s.Rendered)
                    {
                        int* counts = stackalloc int[2];
                        for (int i = 0; i < 2; i++) counts[i] = 0;
                        
                        for (int i = 0; i < items.Length; i++)
                            counts[items[i].Type - EntityType.A]++;

                        for (int i = 0; i < 2; i++)
                        {
                            // var prev = countPtr[i];
                            // var newCount =
                                Interlocked.Add(ref UnsafeUtility.ArrayElementAsRef<int>(countPtr, i), counts[i]);
                            // Debug.Log(String.Format("New count {0}: {1} + {3} => {2}", i, prev, newCount, counts[i]));
                        }
                    }
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
                .ScheduleParallel(Dependency);
            var countPtr2 = (int*)RenderedItemCount.GetUnsafePtr();
            Dependency = Entities.ForEach((ref BeltSplitter s) =>
            {
                var sAABB = s.AABB;
                s.Rendered = FrustumPlanes.Intersect(cullingPlanes, sAABB) != FrustumPlanes.IntersectResult.Out;
                if (s.Rendered)
                {
                    int* counts = stackalloc int[2];
                    for (int i = 0; i < 2; i++)
                        counts[i] = 0;
                    if (s.Input.Type != EntityType.None)
                        counts[s.Input.Type - EntityType.A]++;
                    if (s.Output1.Type != EntityType.None)
                        counts[s.Output1.Type - EntityType.A]++;
                    if (s.Output2.Type != EntityType.None)
                        counts[s.Output2.Type - EntityType.A]++;
                    for (int i = 0; i < 2; i++)
                    {
                        // var newCount =
                            Interlocked.Add(ref UnsafeUtility.ArrayElementAsRef<int>(countPtr2, i), counts[i]);
                        // Debug.Log(String.Format("New count {0} = {1}", i, newCount));
                    }
                }
            })
                .WithNativeDisableUnsafePtrRestriction(countPtr2)
                .WithNativeDisableParallelForRestriction(cullingPlanes)
                .ScheduleParallel(Dependency);
            // Dependency.Complete();
            // for (var index = 0; index < RenderedItemCount.Length; index++)
            // {
            //     var i = RenderedItemCount[index];
            //     Debug.Log($"RenderedItemCount {index} = {i}");
            // }
        }
    }
}