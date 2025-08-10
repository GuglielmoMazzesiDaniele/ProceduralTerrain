Shader "PostProcessing/Water"
{
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        
        Cull Off ZWrite Off 

        Pass
        {
            Name "WaterPass"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Positioning
            float _Height;

            // Shading
            float4 _ShallowWater;
            float4 _DeepWater;
            float _Density;
            float _MaxDepth;

            // Rendering
            TEXTURE2D(_FirstNormalMap);  SAMPLER(sampler_FirstNormalMap);
            TEXTURE2D(_SecondNormalMap); SAMPLER(sampler_SecondNormalMap);
            float _NormalMapScaling;
            float _Shininess;

            float3 fresnel_effect(float3 normal, float3 view)
            {
                float F0 = 0.02;
                return F0 + (1 - F0) * pow(1 - saturate(dot(normal, view)), 5);
            }

            float3 sample_normal(float3 water_world)
            {
                float2 first_uv = water_world.xz * _NormalMapScaling + _Time.x * float2(0.2f, 0.1f);
                float2 second_uv = water_world.xz * _NormalMapScaling * 2 - _Time.x * float2(0.15f, 0.25f);

                float3 first_normal = UnpackNormal(_FirstNormalMap.Sample(sampler_FirstNormalMap, first_uv));
                float3 second_normal = UnpackNormal(_FirstNormalMap.Sample(sampler_FirstNormalMap, second_uv));

                return normalize(lerp(first_normal, second_normal, 0.5));
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Initializing UV
                float2 uv = input.texcoord;

                // Sample the depth from the Camera depth texture.
                float raw_depth = SampleSceneDepth(uv);
                float linear_depth = LinearEyeDepth(raw_depth, _ZBufferParams);

                // Computing the fragment position in world space
                float3 opaque_world = ComputeWorldSpacePosition(uv, raw_depth, UNITY_MATRIX_I_VP);

                // Get base color from blit source texture
                float4 sampled_color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Setting the final color to the same as sampled
                float4 final_color = sampled_color;

                // Building ray from the camera to the opaque fragment
                float3 camera_world = _WorldSpaceCameraPos;

                // Auxiliary directions
                float3 view = normalize(camera_world - opaque_world);
                float3 light = normalize(GetMainLight().direction);
                float3 half_vec = normalize(view + light);

                // Computing numerator and denominator for ray plane intersection
                float denominator = -view.y;
                float numerator = _Height - camera_world.y;
                
                // Avoiding parallel rays
                if (abs(denominator) > 1e-5)
                {
                    // Computing the scalar the ray length to intersect water
                    float distance_to_water = numerator / denominator;

                    // Computing the distance between the camera and the opaque fragment
                    float distance_to_opaque = length(opaque_world - camera_world);

                    // Case in which the water plane is behind the camera
                    if(distance_to_water < 0)
                        return  final_color;
                    
                    // Case in which the camera is above the water
                    if(distance_to_water < distance_to_opaque)
                    {
                        // Computing the distance travelled within the water
                        float distance_in_water = distance_to_opaque - distance_to_water;

                        // Computing the light absorbed into the water
                        float absorption = 1 - exp(-distance_in_water * _Density);

                        // Computing the position in world space of the water
                        float3 water_world = camera_world + distance_to_water * -view;

                        // Sampling the normal from the textures
                        float3 normal = float3(0, 1, 0);

                        float water_depth = saturate((_Height - opaque_world.y) / _MaxDepth);
                        
                        // Computing the color based on the distance travelled in water
                        float3 water_color = lerp(_ShallowWater, _DeepWater, absorption);

                        // Computing the glossiness
                        float gloss = pow(saturate(dot(normal, half_vec)), _Shininess);

                        // Computing the specular hightlight color
                        float3 specular_color = GetMainLight().color * gloss;

                        return float4(water_color, absorption) + float4(specular_color, 1);
                    }
                }
                return final_color;
            }
            
            ENDHLSL
        }
    }
}
