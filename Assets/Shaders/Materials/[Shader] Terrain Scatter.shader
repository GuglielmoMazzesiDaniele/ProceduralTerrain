Shader "Custom/Terrain Scattering"
{
    Properties
    {
        
    }
    
    HLSLINCLUDE

    #include "../Resources/Auxiliary.hlsl"
    
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }
        
        Pass
        {
            Name "Terrain Scatter"

            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            #define UNITY_INSTANCING_ENABLED
            #define UNITY_ANY_INSTANCING_ENABLED
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs

            #include "UnityCG.cginc"
            #include "UnityIndirect.cginc"
            
            StructuredBuffer<float4x4> _Transforms;
            StructuredBuffer<float4x4> _VisibleTransforms;

            struct attributes
            {
                float3 positionOS : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : NORMAL;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            Varyings vert(appdata_base input)
            {
                // Initializing the indirect stuff
                InitIndirectDrawArgs(0);
                
                Varyings output;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4x4 model = _VisibleTransforms[input.instanceID];

                // Computing the world position of the vertex
                float3 positionWS = mul(model, input.vertex);
                
                output.positionHCS = UnityWorldToClipPos(positionWS);
                output.normalWS = normalize(mul((float3x3)model, input.normal));
                output.positionWS = positionWS;
                
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Rockish color
                float3 rock_color = float3(0.35, 0.35, 0.35);

                float3 light_direction = normalize(float3(-0.5f, -1.0f, -0.5f));
                
                // Shading
                float3 lit_color = phong(rock_color, 0.2f, max(0, dot(input.normalWS, light_direction)));
                
                return float4(lit_color, 1);
            }
            
            ENDHLSL
        }
    }
}