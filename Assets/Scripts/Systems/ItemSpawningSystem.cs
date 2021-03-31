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

         protected override void OnCreate()
         {
             _iss = World.GetExistingSystem<ItemSpawningSystem>();
             base.OnCreate();
         }
    
         protected override unsafe void OnUpdate()
         
         {
             if(!_iss._count.IsCreated || _iss._count[0] == 0)
                return;
             var settings = GetSingleton<World.Settings>();
             RenderCount = _iss._count[0];
             // Debug.Log("count " + count);
             if (!_positions1.IsCreated || RenderCount >= _positions1.Length)
             {
                 if (_positions1.IsCreated)
                     _positions1.Dispose();
                 _positions1 = new NativeArray<float3>(RenderCount, Allocator.Persistent);
             }
             var nativeArray = (float3*)_positions1.GetUnsafePtr();
             var instanceIndex = new NativeArray<int>(1, Allocator.Temp);
             instanceIndex[0] = -1;
             var instanceIndexPtr = (int*)instanceIndex.GetUnsafePtr();
             SetupDependency = Dependency = Entities.ForEach((DynamicBuffer<BeltItem> items, in BeltSegment segment) =>
                 {
                     float dist = 0;
                     var dropPoint = segment.DropPoint;
                     var revDir = segment.RevDir;
                     ref var arrayRef = ref UnsafeUtility.AsRef<int>(instanceIndexPtr);
                     // return new float3(DropPoint.x + (dist) * RevDir.x, 0, DropPoint.y + (dist) * RevDir.y);
                     for (int i = 0; i < items.Length; i++)
                     {
                         ref var item = ref items.ElementAt(i);
                         dist += item.Distance / (float) settings.BeltDistanceSubDiv;
                         var computePosition = new float3(dropPoint.x + dist * revDir.x, 0, dropPoint.y + dist * revDir.y);
                         var index = Interlocked.Increment(ref arrayRef);
                         // Debug.Log(index);
                         nativeArray[index] = computePosition;
                     }
                 })
                 .WithNativeDisableUnsafePtrRestriction(nativeArray)
                 .WithNativeDisableUnsafePtrRestriction(instanceIndexPtr)
                 .ScheduleParallel(Dependency);
             
             // Dependency.Complete(); 
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
            _count = new NativeArray<int>(1, Allocator.TempJob);
        }

        protected override unsafe void OnUpdate()
        {
            _count[0] = 0;

            var countPtr = (int*)_count.GetUnsafePtr();
            Dependency =
                Entities
                .ForEach((Entity e, int nativeThreadIndex, int entityInQueryIndex, DynamicBuffer<BeltItem> items,
                    in BeltSegment segment) =>
                {
                    var newone = Interlocked.Add(ref UnsafeUtility.AsRef<int>(countPtr), items.Length);
                })
                .WithNativeDisableUnsafePtrRestriction(countPtr)
                .ScheduleParallel(Dependency);
        }
    }
}