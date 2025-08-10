Shader "PostProcessing/Fog"
{
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        
        ZWrite Off Cull Off

        Pass
        {
            Name "FogPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // FOG
            float4 _FogColor;
            float _FogDensity;
            float _HeavyFogDistance;

            // NOISE
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            float _NoiseScale;
            float _NoiseScroll;

            // SSCS
            float2 _SSCS_TexelSize;
            int _SSCS_Radius;
            float _SSCS_Intensity;

            // Precomputed 16-point Poisson disc samples
            #define SSCS_SAMPLES_COUNT 16
            
            static const float2 poisson_offsets[SSCS_SAMPLES_COUNT] = {
                float2(-0.94201624, -0.39906216),
                float2( 0.94558609, -0.76890725),
                float2(-0.094184101, -0.92938870),
                float2( 0.34495938,  0.29387760),
                float2(-0.91588581,  0.45771432),
                float2(-0.81544232, -0.87912464),
                float2(-0.38277543,  0.27676845),
                float2( 0.97484398,  0.75648379),
                float2( 0.44323325, -0.97511554),
                float2( 0.53742981, -0.47373420),
                float2(-0.26496911, -0.41893023),
                float2( 0.79197514,  0.19090188),
                float2(-0.24188840,  0.99706507),
                float2(-0.81409955,  0.91437590),
                float2( 0.19984126,  0.78641367),
                float2( 0.14383161, -0.14100790)
            };
            
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Initializing UV
                float2 uv = input.texcoord;

                // Sample the depth from the Camera depth texture.
                float raw_depth = SampleSceneDepth(uv);
                float linear_depth = LinearEyeDepth(raw_depth, _ZBufferParams);

                // Computing the fragment position in world space
                float3 world_pos = ComputeWorldSpacePosition(uv, raw_depth, UNITY_MATRIX_I_VP);

                // Get base color from blit source texture
                float4 sampled_color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Compute fog factor based on camera distance
                float world_distance = distance(_WorldSpaceCameraPos, world_pos);

                // Computing the fog weight based on
                float layer1 = saturate(1.0 - exp(-world_distance * _FogDensity));
                float layer2 = saturate(1.0 - exp(-world_distance * (_FogDensity * 0.5)));
                float volumetric_weight = lerp(layer1, layer2, saturate(world_pos.y * 0.05));
                float fog_weight = saturate(world_distance / (_HeavyFogDistance * pow(2.0 - _FogDensity, 4.0)))
                    * volumetric_weight;
                
                // Noise
                float noise = 1.0;
                // Excluding value over the maximum fog distance
                if (world_distance < 100000.0f)
                {
                    float2 noise_uv = world_pos.xz * _NoiseScale + _Time.x * _NoiseScroll;
                    noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noise_uv).r;
                    noise = lerp(0.2, 1.0, noise);
                }
                fog_weight *= noise;

                // SSCS
                float shadowing = 0.0;

                for (int i = 0; i < SSCS_SAMPLES_COUNT; ++i)
                {
                    // Computing the UV offset
                    float2 uv_offset = uv + poisson_offsets[i] * _SSCS_Radius * _SSCS_TexelSize;

                    // Computing the neighbor linear depth
                    float neighbor_raw_depth = SampleSceneDepth(uv_offset);
                    float neighbor_linear_depth = LinearEyeDepth(neighbor_raw_depth, _ZBufferParams);

                    // Computing occlusion and adding it
                    float occlusion = saturate((neighbor_linear_depth - linear_depth) * 10.0);
                    shadowing += occlusion;
                }

                // Computing final shadowing value
                shadowing = saturate(shadowing / SSCS_SAMPLES_COUNT * _SSCS_Intensity);

                fog_weight *= lerp(0.75, 1.0, shadowing);

                // Computing the fog color based on the global directional light
                float view_dot_light = dot(GetMainLight().direction, normalize(world_pos - _WorldSpaceCameraPos))
                    * 0.5 + 0.5;
                float3 final_fog_color = lerp(_FogColor, GetMainLight().color, view_dot_light);

                // Computing the final color
                float4 final_color = lerp(sampled_color, float4(final_fog_color, sampled_color.a), fog_weight);
                
                // Linear interpolation between sampled color and fog color
                return final_color;
            }
            
            ENDHLSL
        }
    }
}

