Shader "Obi/Foam" {

Properties {

    _RadiusScale("Radius scale",float) = 1
}

    SubShader { 

        Tags {"Queue"="Transparent-1" "IgnoreProjector"="True" "RenderType"="Transparent"}
        Blend One OneMinusSrcAlpha  
        ZWrite Off

        Pass { 
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest

            #include "ObiEllipsoids.cginc"
            #include "ObiUtils.cginc"
            #include "UnityCG.cginc"

            struct vin
            {
                float4 vertex   : POSITION;
                float3 corner   : NORMAL;
                fixed4 color    : COLOR;
                float4 velocity : TEXCOORD0; 
                float4 attributes : TEXCOORD1;
            };
            
            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color    : COLOR;
                float4 mapping  : TEXCOORD0;
                float3 viewRay : TEXCOORD1;
                float3 a2 : TEXCOORD2;
                float3 a3 : TEXCOORD3;
                float4 screenPos: TEXCOORD4;
            };

            sampler2D _TemporaryBuffer;
            float _FadeDepth;
            float _FadeIn;
            float _FadeOut;
            float _VelocityStretching;

            v2f vert(vin v)
            { 
                float4 worldVertex = mul(unity_ObjectToWorld,v.vertex);

                float3x3 P, IP;
                float4 t0,t1,t2;
                BuildVelocityStretchedBasis(v.velocity.xyz, _VelocityStretching, v.attributes.z, t0, t1, t2);
                BuildParameterSpaceMatrices(t0,t1,t2,P,IP);
            
                float3 worldPos;
                float3 view;
                float3 eye;
                float radius = BuildEllipsoidBillboard(worldVertex,v.corner,P,IP,worldPos,view,eye);
            
                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos,v.vertex.w));
                o.mapping = float4(v.corner.xy,1/length(eye),radius); // A[1]
                o.viewRay = mul((float3x3)UNITY_MATRIX_V,view);       // A[0]
                o.color = v.color;

                float fadeIn =  1 - saturate(v.attributes.x - (1 - _FadeIn)) / _FadeIn;
                float fadeOut = 1 - saturate((1 - _FadeOut) - v.attributes.x) / (1 - _FadeOut);
                o.color.a *= min(fadeIn,fadeOut);
            
                BuildAuxiliaryNormalVectors(worldVertex,worldPos,view,P,IP,o.a2,o.a3);

                o.screenPos = ComputeScreenPos(o.pos);

                return o;
            }

            sampler2D _CameraDepthTexture;

            float Z2EyeDepth(float z) 
            {
                if (unity_OrthoParams.w < 0.5)
                    return LinearEyeDepth(z); // Unity's LinearEyeDepth only works for perspective cameras.
                else{

                    // since we're not using LinearEyeDepth in orthographic, we must reverse depth direction ourselves:
                    #if UNITY_REVERSED_Z 
                        z = 1-z;
                    #endif

                    return ((_ProjectionParams.z - _ProjectionParams.y) * z + _ProjectionParams.y);
                }
            }

            float4 frag(v2f i) : SV_Target
            {
           
                float4 color = i.color;

                // generate sphere normals:
                float3 p,n;
                float thickness = IntersectEllipsoid(i.viewRay,i.mapping, i.a2, i.a3, p, n);
                color.a *= (thickness * 0.5f / i.mapping.w);

                //get depth from depth texture
                float sceneDepth = tex2Dproj(_CameraDepthTexture,i.screenPos).r;
                float eyeDepth = Z2EyeDepth(sceneDepth);
                if (-p.z > eyeDepth) discard;
                
                float fluidSurface = tex2D(_TemporaryBuffer, i.screenPos.xy /  i.screenPos.w).x;
                float distanceToSurface = -fluidSurface - p.z;

                if (_FadeDepth > 0.0001f)
                {
                   //clip(distanceToSurface);
                    color.a *= 1 - saturate(distanceToSurface/_FadeDepth);
                }

                // premultiply:
                color.rgb *= color.a;

                return color;
            }
             
            ENDCG

        } 
       
    } 
FallBack "Diffuse"
}

