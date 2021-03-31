using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Automation
{
    
    [ExecuteAlways]
    public class ItemRendering : MonoBehaviour
    {
        public Mesh Mesh;
        public Material Material;
        private GraphicsBuffer _bufferWithArgs;

        private void OnDestroy()
        {
            _bufferWithArgs?.Release();
        }

        private void LateUpdate()
        {
            _bufferWithArgs = new GraphicsBuffer( GraphicsBuffer.Target.Structured, 4, 12);
            _bufferWithArgs.SetData(new List<Vector3>
            {
                new float3(-1,0,0),
                new float3(0,1,0),
                new float3(1,0,0),
                new float3(2,0,0),
            });
            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetBuffer("_AllInstancesTransformBuffer", _bufferWithArgs);
            Graphics.DrawMeshInstancedProcedural(Mesh, 0, Material, new Bounds(Vector3.zero, Vector3.one*200),4, materialPropertyBlock);
        }
    }
}