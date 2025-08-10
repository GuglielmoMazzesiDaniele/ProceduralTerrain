Shader "Custom/Water"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    
    ENDHLSL
    
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            Name "Water Chunk"
            
            HLSLPROGRAM

            #pragma vertex vert
            #pragma geometry geometry
            #pragma fragment frag
            

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ MAIN_LIGHT_SHADOWS MAIN_LIGHT_SHADOWS_CASCADE MAIN_LIGHT_SHADOWS_SCREEN

            // Structs
            struct attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct vertex_output
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            struct geometry_output
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            vertex_output vert(attributes input)
            {
                // Initializing the output
                vertex_output output;

                // Computing world position
                float3 world_pos = TransformObjectToWorld(input.positionOS);
                output.positionWS = world_pos;
                
                // Passing down the uv coordinates
                output.uv = input.uv;

                return output;
            }

            [maxvertexcount(3)]
            void geometry(triangle vertex_output vertices[3], inout TriangleStream<geometry_output> output_stream)
            {
                // Computing the normal
                float3 normalWS = normalize(cross(vertices[1].positionWS - vertices[0].positionWS,
                    vertices[2].positionWS - vertices[0].positionWS));

                // Pushing the triangles back on the stream
                for(int i = 0; i < 3; i++)
                {
                    // Creating the geometry output
                    geometry_output current_vertex;
                    current_vertex.positionHCS = TransformWorldToHClip(vertices[i].positionWS);
                    current_vertex.uv = vertices[i].uv;
                    current_vertex.positionWS = vertices[i].positionWS;
                    current_vertex.normalWS = normalWS;

                    // Pushing on the triangles stream
                    output_stream.Append(current_vertex);
                }

                // Restarting strip
                output_stream.RestartStrip();
            }

            float4 frag(geometry_output input) : SV_Target
            {
                return float4(0.15, 0.2, 0.5, 0.25);
            }
            
            ENDHLSL
        }
    }
}