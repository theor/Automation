// This shader fills the mesh shape with a color predefined in the code.
Shader "Custom/ItemURPShader"
{
    // The properties block of the Unity shader. In this example this block is empty
    // because the output color is predefined in the fragment shader code.
    Properties
    { }

    // The SubShader block containing the Shader code.
    SubShader
    {
        // SubShader Tags define when and under which conditions a SubShader block or
        // a pass is executed.
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            // The HLSL code block. Unity SRP uses the HLSL language.
            HLSLPROGRAM
            #pragma multi_compile_instancing
            // This line defines the name of the vertex shader.
            #pragma vertex vert
            // This line defines the name of the fragment shader.
            #pragma fragment frag

            // The Core.hlsl file contains definitions of frequently used HLSL
            // macros and functions, and also contains #include references to other
            // HLSL files (for example, Common.hlsl, SpaceTransforms.hlsl, etc.).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // The structure definition defines which variables it contains.
            // This example uses the Attributes structure as an input structure in
            // the vertex shader.
            struct Attributes
            {
                // The positionOS variable contains the vertex positions in object
                // space.
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                // The positions in this struct must have the SV_POSITION semantic.
                float4 positionHCS  : SV_POSITION;
                half3 color : COLOR;
            };
            CBUFFER_START(UnityPerMaterial)
            StructuredBuffer<float3> _AllInstancesTransformBuffer;
            CBUFFER_END

            // The vertex shader definition with properties defined in the Varyings
            // structure. The type of the vert function must match the type (struct)
            // that it returns.
            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                // Declaring the output object (OUT) with the Varyings struct.
                Varyings OUT;
                // IN.positionOS = float4(1,0,0,0);
                // The TransformObjectToHClip function transforms vertex positions
                // from object space to homogenous clip space.
                // float3 w = TransformObjectToWorld(IN.positionOS.xyz) + float3(-(float)instanceID,0,0);// _AllInstancesTransformBuffer[instanceID];
                // OUT.positionHCS = TransformWorldToHClip(w);
                // VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                // OUT.positionHCS = TransformObjectToHClip(
                //  IN.positionOS.xyz + _AllInstancesTransformBuffer[instanceID]//float3((float)instanceID,0,0)
                // );

                float3 pos = _AllInstancesTransformBuffer[instanceID];
                float4x4 tr = {
                    1,0,0,0,
                    0,1,0,0,
                    0,0,1,0,
                    pos.x,pos.y,pos.z,1,
                };
                float scale = 3;
                float4x4 scaleMat= {
                    scale,0,0,0,
                    0,scale,0,0,
                    0,0,scale,0,
                    0,0,0,1,
                };
                OUT.positionHCS = mul(
                    GetWorldToHClipMatrix(),
                    mul(
                        mul(
                            mul(
                                GetObjectToWorldMatrix(),
                                scaleMat
                            ),
                            float4(
                                IN.positionOS.xyz, 1.0)
                        ),
                     tr)
                );
                
                // OUT.positionHCS.w /= 2;
                OUT.color = half4(0.5, 1, 0.1, 1);
                // OUT.color = _AllInstancesTransformBuffer[instanceID];
                // Returning the output.
                return OUT;
            }

            // The fragment shader definition.
            half4 frag(Varyings IN) : SV_Target
            {
                // Defining the color variable and returning it.
                // half4 customColor = half4(0.5, 0, 0, 1);
                // return customColor;
                return half4(IN.color, 1);
            }
            ENDHLSL
        }
    }
}