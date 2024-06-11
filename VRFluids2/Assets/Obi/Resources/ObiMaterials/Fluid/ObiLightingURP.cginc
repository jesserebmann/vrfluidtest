#ifndef OBILIGHTINGBUILTURP_INCLUDED
#define OBILIGHTINGBUILTURP_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
    #undef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
#endif

void MainLight_half(float3 WorldPos, out half3 Direction, out half3 color, out half DistanceAtten, out half ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
   Direction = half3(0.5, 0.5, 0);
   color = 1;
   DistanceAtten = 1;
   ShadowAtten = 1;
#else
    #ifdef UNIVERSAL_LIGHTING_INCLUDED

        #if SHADOWS_SCREEN
           half4 clipPos = TransformWorldToHClip(WorldPos);
           half4 shadowCoord = ComputeScreenPos(clipPos);
        #else
           half4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
        #endif
           Light mainLight = GetMainLight(shadowCoord);
           Direction = mainLight.direction;
           color = mainLight.color;
           DistanceAtten = mainLight.distanceAttenuation;
           ShadowAtten = mainLight.shadowAttenuation;
    #else
       Direction = _WorldSpaceLightPos0;
       color = _LightColor0;
       DistanceAtten = 1;
       ShadowAtten = 1;
    #endif
#endif
}

void Ambient_half(half3 WorldPos, half3 WorldNormal, out half3 ambient)
{
#ifdef SHADERGRAPH_PREVIEW
ambient = half3(0,0,0);
#else
    #ifdef UNIVERSAL_LIGHTING_INCLUDED
        // Samples spherical harmonics, which encode light probe data
        float3 vertexSH;
        OUTPUT_SH(WorldNormal, vertexSH);

        float2 lightmapUV = float2(0, 0); // Initialize lightmapUV with default values
        // This function calculates the final baked lighting from light maps or probes
        ambient = SAMPLE_GI(lightmapUV, vertexSH, WorldNormal);
    #else
        #if UNITY_SHOULD_SAMPLE_SH
        ambient = ShadeSHPerPixel(half4(WorldNormal, 1.0),half3(0,0,0),WorldPos);
        #else
        ambient = UNITY_LIGHTMODEL_AMBIENT;
        #endif

    #endif
#endif
}

void DirectSpecular_half(half3 specular, half Smoothness, half3 Direction, half3 color, half3 WorldNormal, half3 WorldView, out half3 Out)
{
#if SHADERGRAPH_PREVIEW
   Out = 0;
#else
   Smoothness = exp2(10 * Smoothness + 1);
   WorldNormal = normalize(WorldNormal);
   WorldView = SafeNormalize(WorldView);
   Out = LightingSpecular(color, Direction, WorldNormal, WorldView, half4(specular, 0), Smoothness);
#endif
}

/*#ifndef UNIVERSAL_LIGHTING_INCLUDED
    sampler2D _CameraDepthTexture;

    void SceneEyeDepth_float(float4 clipPos, out float depth) 
    {
        float z = tex2D(_CameraDepthTexture,clipPos).r;

        if (unity_OrthoParams.w < 0.5)
            depth = 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w); //LinearEyeDepth only works for perspective cameras.
        else{

            // since we're not using LinearEyeDepth in orthographic, we must reverse depth direction ourselves:
            #if UNITY_REVERSED_Z 
                z = 1-z;
            #endif

            depth = ((_ProjectionParams.z - _ProjectionParams.y) * z + _ProjectionParams.y);
        }
    }
#endif*/

#endif
