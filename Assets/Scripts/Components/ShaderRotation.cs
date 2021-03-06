using Unity.Entities;
using Unity.Rendering;

namespace Automation
{
    [MaterialProperty("_Rotation", MaterialPropertyFormat.Float)]
    public struct ShaderRotation : IComponentData
    {
        public float Value;
    }
}