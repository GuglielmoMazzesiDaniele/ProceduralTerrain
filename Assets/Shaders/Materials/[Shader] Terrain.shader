Shader "Custom/Terrain"
{
    HLSLINCLUDE
    
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    
    #include "../Resources/Noise.hlsl"
    #include "../Resources/Biome.hlsl"
    #include "../Resources/Auxiliary.hlsl"
    
    // Provided by CPU
    int _SliceIndex;
    TEXTURE2D_ARRAY(_HeightMaps);
    TEXTURE2D_ARRAY(_NormalMaps);
    TEXTURE2D_ARRAY(_BiomeMaps);
    
    TEXTURE2D_ARRAY(_BiomesGradient);
    StructuredBuffer<float2> _BiomesHeightRanges;
    
    // --- MACROS ---
    #define SAMPLE_HEIGHTMAP(uv, OUT) { \
        OUT = SAMPLE_TEXTURE2D_ARRAY_LOD(_HeightMaps, sampler_PointClamp, uv, _SliceIndex, 0); \
    }

    #define SAMPLE_NORMALMAP(uv, OUT) { \
        OUT = SAMPLE_TEXTURE2D_ARRAY_LOD(_NormalMaps, sampler_PointClamp, uv, _SliceIndex, 0); \
    }

    // --- FUNCTIONS ---
    float3 biome_color(uint biome_index, float height, float slope)
    {
        // Computing UV coordinates based on the height
        float2 colormap_uv = float2(
            (height - _BiomesHeightRanges[biome_index].x)
            / (_BiomesHeightRanges[biome_index].y - _BiomesHeightRanges[biome_index].x),
            0);

        colormap_uv *= slope * 0.75 + 0.25f;

        // Sampling the color of the current biome
        return SAMPLE_TEXTURE2D_ARRAY_LOD(_BiomesGradient, sampler_PointClamp,
            colormap_uv, biome_index, 0);
    }

    ENDHLSL
    
    SubShader
    {
        // -- RENDERING PASS --
        Pass
        {
            Name "Terrain Chunk"
            Tags
            {
                "RenderType" = "Opaque"
                "Queue" = "Geometry"
                
            }
            
            ZWrite On
            
            HLSLPROGRAM

            #pragma vertex vert
            #pragma geometry geometry
            #pragma fragment frag
            
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
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
                float4 shadowCoords: TEXCOORD2;
            };

            struct geometry_output
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 shadowCoords: TEXCOORD3;
            };
            
            vertex_output vert(attributes input)
            {
                // Initializing the output
                vertex_output output;

                // Sampling the height
                float height;
                SAMPLE_HEIGHTMAP(input.uv, height);

                // Computing the object space with height
                float3 object_pos = input.positionOS + float3(0, height, 0);

                // Computing world position
                float3 world_pos = TransformObjectToWorld(object_pos);
                output.positionWS = world_pos;
                
                // Computing the shadow coordinates
                output.shadowCoords = TransformWorldToShadowCoord(world_pos);

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
                    current_vertex.shadowCoords = vertices[i].shadowCoords;
                    current_vertex.normalWS = normalWS;

                    // Pushing on the triangles stream
                    output_stream.Append(current_vertex);
                }

                // Restarting strip
                output_stream.RestartStrip();
            }
            
            float4 frag(geometry_output input) : SV_Target
            {
                // Computing the vectors used in the computation of the final color
                float3 normal = normalize(input.normalWS);

                // Converting the biome pos in a biome blend struct
                height_blend blend = world_to_biomes_blend(input.positionWS.xz);

                // Auxiliary parameters
                float3 unlit_color;
                float height = input.positionWS.y;
                float slope = saturate(dot(normal, float3(0, 1, 0)));

                switch (blend.blend_type)
                {
                    // --- PURE BIOME ---
                    case 0:
                    default:
                        unlit_color = biome_color(blend.top_left, height, slope);
                        break;
                    // --- BLENDING ON X AXIS ---
                    case 1:
                        unlit_color = lerp(biome_color(blend.top_left, height, slope),
                            biome_color(blend.top_right, height, slope), blend.blending.x);
                        break;
                    // --- BLENDING ON Y AXIS ---
                    case 2:
                        unlit_color = lerp(biome_color(blend.top_left, height, slope),
                            biome_color(blend.bottom_left, height, slope), blend.blending.y);
                        break;
                    // --- BILINEAR INTERPOLATION ---
                    case 3:
                        // Top row color
                        float3 top_row_color = lerp(biome_color(blend.top_left, height, slope),
                            biome_color(blend.top_right, height, slope), blend.blending.x);

                        // Bottom row color
                        float3 bottom_row_color = lerp(biome_color(blend.bottom_left, height, slope),
                            biome_color(blend.bottom_right, height, slope), blend.blending.x);

                        unlit_color = lerp(top_row_color, bottom_row_color, blend.blending.y);
                        break;
                }

                float3 test_normal;
                SAMPLE_NORMALMAP(input.uv, test_normal)

                // Shading
                float3 lit_color = phong(unlit_color, 0.2f, max(0, dot(normal, GetMainLight().direction)));

                // Shadowing
                lit_color *= GetMainLight(input.shadowCoords).shadowAttenuation;

                return float4(lit_color, 1);
            }
            
            ENDHLSL
        }
            
        // --- SHADOW CASTER PASS ---
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM

            // -------------------------------------
            // Shader Stages
            #pragma vertex CustomShadowPassVertex
            #pragma geometry CustomShadowPassGeometry
            #pragma fragment ShadowPassFragment
            
            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            Attributes CustomShadowPassVertex(Attributes input)
            {
                Attributes output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                #if defined(_ALPHATEST_ON)
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                #endif

                // Moving the position in OS based on the height
                SAMPLE_HEIGHTMAP(input.texcoord, input.positionOS.y);
                
                return input;
            }

            [maxvertexcount(3)]
            void CustomShadowPassGeometry(triangle Attributes vertices[3],
                inout TriangleStream<Varyings> output_stream)
            {
                // Computing the normal in object space
                float3 normalOS = normalize(cross(vertices[1].positionOS - vertices[0].positionOS,
                    vertices[2].positionOS - vertices[0].positionOS));

                // Pushing the triangles back on the stream
                for(int i = 0; i < 3; i++)
                {
                    // Creating the geometry output
                    Varyings current_vertex = (Varyings)0;
                    vertices[i].normalOS = normalOS;
                    current_vertex.positionCS = GetShadowPositionHClip(vertices[i]);

                    // Pushing on the triangles stream
                    output_stream.Append(current_vertex);
                }

                // Restarting strip
                output_stream.RestartStrip();
            }
            
            ENDHLSL
        }

        // -- DEPTH PASS ---
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex CustomDepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            Varyings CustomDepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #if defined(_ALPHATEST_ON)
                    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                #endif

                // Moving the position in OS based on the height
                SAMPLE_HEIGHTMAP(input.texcoord, input.position.y);
                
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }
            
            ENDHLSL
        }

        // -- DEPTH NORMALS PASS ---
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex CustomDepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"

            Varyings CustomDepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #if defined(REQUIRES_UV_INTERPOLATOR)
                    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                #endif

                // Moving the position in OS based on the height
                SAMPLE_HEIGHTMAP(input.texcoord, input.positionOS.y);
                SAMPLE_NORMALMAP(input.texcoord, input.normal);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normal, input.tangentOS);

                output.normalWS = half3(normalInput.normalWS);
                
                #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
                    || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                    float sign = input.tangentOS.w * float(GetOddNegativeScale());
                    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
                #endif

                #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
                    output.tangentWS = tangentWS;
                #endif

                #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
                    output.viewDirTS = viewDirTS;
                #endif

                return output;
            }
            
            ENDHLSL
        }
    }
}
