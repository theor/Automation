using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Automation
{
    [UpdateBefore(typeof(ItemSpawningSystem))]
    class ItemRenderingSetupSystem : SystemBase
     {

         private ItemSpawningSystem _iss;
         public NativeArray<float3> _positions1;
         public int RenderCount;
         public JobHandle SetupDependency;
         private NativeArray<int> _instanceIndex;

         protected override void OnCreate()
         {
             _iss = World.GetExistingSystem<ItemSpawningSystem>();
             _instanceIndex = new NativeArray<int>(1, Allocator.Persistent);
             base.OnCreate();
         }

         protected override void OnDestroy()
         {
             base.OnDestroy();
             _positions1.Dispose();
             _instanceIndex.Dispose();
         }

         protected override unsafe void OnUpdate()
         
         {
             if(!_iss._count.IsCreated || _iss._count[0] == 0)
                return;
             var settings = GetSingleton<World.Settings>();
             RenderCount = math.min(499,_iss._count[0]);
             // Debug.Log("count " + count);
             if (!_positions1.IsCreated || RenderCount >= _positions1.Length)
             {
                 if (_positions1.IsCreated)
                     _positions1.Dispose();
                 _positions1 = new NativeArray<float3>(RenderCount, Allocator.Persistent);
             }
             var nativeArray = (float3*)_positions1.GetUnsafePtr();
             _instanceIndex[0] = -1;
             var instanceIndexPtr = (int*)_instanceIndex.GetUnsafePtr();
             var renderCount = RenderCount;

             SetupDependency = Dependency = Entities.ForEach((DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                 {
                     if(!segment.Rendered)
                         return;
                     float dist = 0;
                     var dropPoint = segment.DropPoint;
                     var revDir = segment.RevDir;
                     ref var instanceIndexRef = ref UnsafeUtility.AsRef<int>(instanceIndexPtr);
                     for (int i = 0; i < items.Length; i++)
                     {
                         ref var item = ref items.ElementAt(i);
                         dist += item.Distance / (float) settings.BeltDistanceSubDiv;
                         var computePosition = new float3(dropPoint.x + dist * revDir.x, 0, dropPoint.y + dist * revDir.y);
                         var index = Interlocked.Increment(ref instanceIndexRef);
//                         Debug.Log(String.Format("Index {0} / {1}", index, renderCount));
                         if(index < renderCount)
                            nativeArray[index] = computePosition;
                     }
                 })
                 .WithNativeDisableUnsafePtrRestriction(nativeArray)
                 .WithNativeDisableUnsafePtrRestriction(instanceIndexPtr)
                 .ScheduleParallel(Dependency);
         }
     }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    class ItemRenderingSystem : SystemBase
    {
        private ItemRenderingSetupSystem _irss;
        private GraphicsBuffer _bufferWithArgs;

        protected override void OnCreate()
        {
            base.OnCreate();
            _irss = World.GetExistingSystem<ItemRenderingSetupSystem>();
        }

        protected override void OnUpdate()
        {
            if (_irss.RenderCount == 0)
                return;
            _irss.SetupDependency.Complete();
            var prefabs = GetSingleton<World.Prefabs>();
            var m = EntityManager.GetSharedComponentData<Unity.Rendering.RenderMesh>(prefabs.ItemPrefab);
            if (_bufferWithArgs == null || _bufferWithArgs.count != _irss.RenderCount)
            {
                _bufferWithArgs?.Dispose();
                _bufferWithArgs = new GraphicsBuffer( GraphicsBuffer.Target.Structured, _irss.RenderCount, 12);
            }
            _bufferWithArgs.SetData(_irss._positions1);
            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetBuffer("_AllInstancesTransformBuffer", _bufferWithArgs);
            Graphics.DrawMeshInstancedProcedural(m.mesh, 0, m.material, new Bounds(Vector3.zero, Vector3.one*10000),_irss.RenderCount, materialPropertyBlock);
        }
    }

    class ItemSpawningSystem : SystemBase
    {
        private EntityQuery _getEntityQuery;
        public NativeArray<int> _count;
        
        protected override void OnCreate()
        {
            _getEntityQuery = GetEntityQuery(ComponentType.ReadWrite<BeltSegment>());
            _count = new NativeArray<int>(1, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _count.Dispose();
        }

        protected override unsafe void OnUpdate()
        {
            _count[0] = 0;

            var countPtr = (int*)_count.GetUnsafePtr();
            Dependency =
                Entities
                .ForEach((Entity e, int nativeThreadIndex, int entityInQueryIndex, DynamicBuffer<BeltItem> items,
                    ref BeltSegment segment) =>
                {
                    segment.Rendered = segment.Start.x > -200;
                    if(segment.Rendered)
                        Interlocked.Add(ref UnsafeUtility.AsRef<int>(countPtr), items.Length);
                })
                .WithNativeDisableUnsafePtrRestriction(countPtr)
                .ScheduleParallel(Dependency);
        }
    }
}