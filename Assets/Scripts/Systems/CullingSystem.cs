using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using FrustumPlanes = Unity.Rendering.FrustumPlanes;

namespace Automation
{
    class CullingSystem : SystemBase
    {
        private Vector3[] _corners;
        private NativeArray<float4> _cameraPlanes;
        private RenderedItemPositionComputationSystem _renderedItemPositionComputationSystem;

        public NativeArray<int> RenderedItemCount;
        public JobHandle CountDependency;
        protected override void OnCreate()
        {
            _corners = new Vector3[4];
            _cameraPlanes = new NativeArray<float4>(6, Allocator.Persistent);
            _renderedItemPositionComputationSystem = World.GetExistingSystem<RenderedItemPositionComputationSystem>();
        }

        protected override void OnDestroy()
        {
            _cameraPlanes.Dispose();
            if(RenderedItemCount.IsCreated)
                RenderedItemCount.Dispose();
        }

        const int ItemTypes = 2;

        protected override unsafe void OnUpdate()
        {
            _renderedItemPositionComputationSystem.SetupDependency.Complete();
            if(!RenderedItemCount.IsCreated)
                RenderedItemCount = new NativeArray<int>(ItemTypes, Allocator.Persistent);
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

            var cullingPlanes = _cameraPlanes;
            FrustumPlanes.FromCamera(camera, cullingPlanes);
            Dependency = Entities.ForEach((DynamicBuffer<BeltItem> items, ref BeltSegment s) =>
                {
                    var sAABB = s.AABB;
                    s.Rendered = FrustumPlanes.Intersect(cullingPlanes, sAABB) != FrustumPlanes.IntersectResult.Out;
                    if (s.Rendered)
                    {
                        int* counts = stackalloc int[2];
                        UnsafeUtility.MemClear(counts, UnsafeUtility.SizeOf<int>() * ItemTypes);
                        
                        for (int i = 0; i < items.Length; i++)
                            counts[items[i].Type - ItemType.PaintBucket]++;

                        for (int i = 0; i < ItemTypes; i++)
                            Interlocked.Add(ref UnsafeUtility.ArrayElementAsRef<int>(countPtr, i), counts[i]);
                    }
                })
                .WithNativeDisableUnsafePtrRestriction(countPtr)
                .WithNativeDisableParallelForRestriction(cullingPlanes)
                .ScheduleParallel(Dependency);
            var countPtr2 = (int*)RenderedItemCount.GetUnsafePtr();
             CountDependency = Dependency = Entities.ForEach((ref BeltSplitter s) =>
            {
                var sAABB = s.AABB;
                s.Rendered = FrustumPlanes.Intersect(cullingPlanes, sAABB) != FrustumPlanes.IntersectResult.Out;
                if (s.Rendered)
                {
                    int* counts = stackalloc int[2];
                    UnsafeUtility.MemClear(counts, UnsafeUtility.SizeOf<int>() * 2);

                    if (s.Input.Type != ItemType.None)
                        counts[s.Input.Type - ItemType.PaintBucket]++;
                    if (s.Output1.Type != ItemType.None)
                        counts[s.Output1.Type - ItemType.PaintBucket]++;
                    if (s.Output2.Type != ItemType.None)
                        counts[s.Output2.Type - ItemType.PaintBucket]++;
                    for (int i = 0; i < 2; i++)
                        Interlocked.Add(ref UnsafeUtility.ArrayElementAsRef<int>(countPtr2, i), counts[i]);
                }
            })
                .WithNativeDisableUnsafePtrRestriction(countPtr2)
                .WithNativeDisableParallelForRestriction(cullingPlanes)
                .ScheduleParallel(Dependency);
        }
    }
}