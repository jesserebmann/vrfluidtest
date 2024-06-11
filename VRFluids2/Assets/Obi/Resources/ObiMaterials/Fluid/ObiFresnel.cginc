#ifndef OBIFRESNEL_INCLUDED
#define OBIFRESNEL_INCLUDED

void FresnelReflectAmount_float (float ior, float3 normal, float3 incident, out float ret)
{
    float cosi = dot(normalize(incident), normalize(normal));

    float etai = 1, etat = ior;
    if (cosi > 0) {
        float aux = etai;
        etai = etat;
        etat = aux;
    }
    // Compute sini using Snell's law
    float sint = etai / etat * sqrt(max(0, 1 - cosi * cosi));
    
    // Total internal reflection
    if (sint >= 1) {
        ret = 1;
        return;
    }
    else {
        float cost = sqrt(max(0, 1 - sint * sint));
        cosi = abs(cosi);
        float Rs = ((etat * cosi) - (etai * cost)) / ((etat * cosi) + (etai * cost));
        float Rp = ((etai * cosi) - (etat * cost)) / ((etai * cosi) + (etat * cost));
        ret = (Rs * Rs + Rp * Rp) / 2;
    }
}


// Use in underwater shader to read scene depth, and compare it with fluid depth to determine whether we should refract.
void Z2EyeDepth_float(float z, out float depth) 
{
    if (unity_OrthoParams.w < 0.5)
        depth = 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);//LinearEyeDepth(z); // Unity's LinearEyeDepth only works for perspective cameras.
    else{

        // since we're not using LinearEyeDepth in orthographic, we must reverse depth direction ourselves:
        #if UNITY_REVERSED_Z 
            z = 1-z;
        #endif

        depth = ((_ProjectionParams.z - _ProjectionParams.y) * z + _ProjectionParams.y);
    }
}

#endif
