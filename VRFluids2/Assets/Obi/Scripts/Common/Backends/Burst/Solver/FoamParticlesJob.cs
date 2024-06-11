#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;

namespace Obi
{
    [BurstCompile]
    unsafe struct EmitParticlesJob : IJobParallelFor
    {
        [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<int> activeParticles;
        [ReadOnly] public NativeArray<float4> positions;
        [ReadOnly] public NativeArray<float4> velocities;
        [ReadOnly] public NativeArray<float4> principalRadii;
        [ReadOnly] public NativeArray<float4> angularVelocities;

        [NativeDisableParallelForRestriction] public NativeArray<float> emitPotentials;

        [NativeDisableParallelForRestriction] public NativeArray<float4> outputPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputVelocities;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputColors;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<int> dispatchBuffer;

        public float2 vorticityRange;
        public float2 velocityRange;
        public float foamGenerationRate;

        public float lifetime;
        public float lifetimeRandom;
        public float particleSize;
        public float sizeRandom;
        public float buoyancy;
        public float drag;
        public float4 foamColor;

        public float deltaTime;

        //https://www.shadertoy.com/view/4djSRW
        float3 hash33(float3 p3)
        {
            p3 = math.frac(p3 * new float3(.1031f, .1030f, .0973f));
            p3 += math.dot(p3, p3.yxz + 33.33f);
            return math.frac((p3.xxy + p3.yxx) * p3.zyx);
        }

        float hash13(float3 p3)
        {
            p3 = math.frac(p3 * .1031f);
            p3 += math.dot(p3, p3.zyx + 31.32f);
            return math.frac((p3.x + p3.y) * p3.z);
        }


        void RandomInCylinder(float seed, float4 pos1, float4 pos2, float radius, out float4 position, out float3 velocity)
        {
            float3 rand = hash33(math.lerp(pos1.xyz, pos2.xyz, seed));

            float3 v = pos2.xyz - pos1.xyz;
            float d = math.length(v);
            float3 b1 = d > BurstMath.epsilon ? v / d : v;
            float3 b2 = math.normalizesafe(math.cross(b1, new float3(1, 0, 0)));
            float3 b3 = math.cross(b2, b1);

            float theta = rand.y * 2 * math.PI;
            float2 disc = radius * math.sqrt(rand.x) * new float2(math.cos(theta), math.sin(theta));

            velocity = b2 * disc.x + b3 * disc.y;
            position = new float4(pos1.xyz + b1 * d * rand.z + velocity, 0);
        }

        float Remap01(float value, float min_, float max_)
        {
            return (math.min(value, max_) - math.min(value, min_)) / (max_ - min_);
        }

        public void Execute(int i)
        {
            int* dispatch = (int*)dispatchBuffer.GetUnsafePtr();

            int p = activeParticles[i];

            // calculate fluid potential for foam generation:
            float vorticityPotential = Remap01(math.length(angularVelocities[p]), vorticityRange.x, vorticityRange.y);
            float velocityPotential = Remap01(math.length(velocities[p]), velocityRange.x, velocityRange.y);
            float totalPotential = velocityPotential * (vorticityPotential);

            // calculate amount of emitted particles
            emitPotentials[p] += totalPotential * foamGenerationRate * deltaTime;
            int emitCount = (int)emitPotentials[p];
            emitPotentials[p] -= emitCount;

            for (int j = 0; j < emitCount; ++j)
            {
                // atomically increment alive particle counter:
                int count = Interlocked.Add(ref dispatch[3], 1) - 1;

                if (count < outputPositions.Length) 
                {
                    // initialize foam particle in a random position inside the cylinder spawned by fluid particle:
                    float3 radialVelocity;
                    float4 pos;
                    RandomInCylinder(j, positions[p], positions[p] + velocities[p] * deltaTime, principalRadii[p].x, out pos, out radialVelocity);

                    // calculate initial life/size/color:
                    float initialLife = velocityPotential * (lifetime - hash13(positions[p].xyz) * lifetime * lifetimeRandom);
                    float initialSize = particleSize - hash13(positions[p].xyz + new float3(0.51f, 0.23f, 0.1f)) * particleSize * sizeRandom;

                    outputPositions[count] = pos;
                    outputVelocities[count] = velocities[p] + new float4(radialVelocity, buoyancy);
                    outputColors[count] = foamColor;
                    outputAttributes[count] = new float4(1, 1/initialLife, initialSize, drag); 
                }
            }
        }
    }

    [BurstCompile]
    unsafe struct UpdateParticlesJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<float4> positions;
        [ReadOnly] public NativeArray<float4> velocities;

        [ReadOnly] public NativeArray<int> simplices;
        [ReadOnly] public SimplexCounts simplexCounts;

        [ReadOnly] public NativeMultilevelGrid<int> grid;
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<int> gridLevels;

        [ReadOnly] public Poly6Kernel densityKernel;

        [ReadOnly] public NativeArray<float4> inputPositions;
        [ReadOnly] public NativeArray<float4> inputVelocities;
        [ReadOnly] public NativeArray<float4> inputColors;
        [ReadOnly] public NativeArray<float4> inputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<float4> outputPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputVelocities;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputColors;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<int> dispatchBuffer;

        [ReadOnly] public Oni.SolverParameters parameters;

        public float deltaTime;

        static readonly int4[] offsets =
        {
                new int4(0, 0, 0, 1),
                new int4(1, 0, 0, 1),
                new int4(0, 1, 0, 1),
                new int4(1, 1, 0, 1),
                new int4(0, 0, 1, 1),
                new int4(1, 0, 1, 1),
                new int4(0, 1, 1, 1),
                new int4(1, 1, 1, 1)
            };

        public void Execute(int i)
        {
            int* dispatch = (int*)dispatchBuffer.GetUnsafePtr();
            int count = Interlocked.Add(ref dispatch[3], -1);

            if (count < inputPositions.Length && inputAttributes[count].x > 0)
            {
                int aliveCount = Interlocked.Add(ref dispatch[7], 1) - 1;

                int offsetCount = ((int)parameters.mode == 1) ? 4 : 8;
                float4 advectedVelocity = float4.zero;
                float kernelSum = 0;
                uint neighbourCount = 0;

                float4 diffusePos = inputPositions[count];
                float4 attributes = inputAttributes[count];
                float4 avgPos = float4.zero;

                for (int k = 0; k < gridLevels.Length; ++k)
                {
                    int l = gridLevels[k];
                    float radius = NativeMultilevelGrid<int>.CellSizeOfLevel(l);

                    float4 cellCoords = math.floor(diffusePos / radius);

                    cellCoords[3] = 0;
                    if ((int)parameters.mode == 1)
                        cellCoords[2] = 0;

                    float4 posInCell = diffusePos - (cellCoords * radius + new float4(radius * 0.5f));
                    int4 quadrant = (int4)math.sign(posInCell);

                    quadrant[3] = l;

                    for (int o = 0; o < offsetCount; ++o)
                    {
                        int cellIndex;
                        if (grid.TryGetCellIndex((int4)cellCoords + offsets[o] * quadrant, out cellIndex))
                        {
                            var cell = grid.usedCells[cellIndex];
                            for (int n = 0; n < cell.Length; ++n)
                            {
                                int simplexStart = simplexCounts.GetSimplexStartAndSize(cell[n], out int simplexSize);

                                for (int a = 0; a < simplexSize; ++a)
                                {
                                    int simplex = simplices[simplexStart + a];
                                    float4 r = diffusePos - positions[simplex];

                                    r[3] = 0;
                                    if ((int)parameters.mode == 1)
                                        r[2] = 0;

                                    float d = math.length(r);
                                    if (d <= radius)
                                    {
                                        float w = densityKernel.W(d, radius);
                                        kernelSum += w;
                                        advectedVelocity += velocities[simplex] * w;
                                        avgPos += positions[simplex] * w;
                                        neighbourCount++;
                                    }
                                }
                            }
                        }
                    }
                }

                float4 forces = float4.zero;

                // foam/bubble particle:
                if (kernelSum > BurstMath.epsilon && neighbourCount > 8)
                {
                    // advection: 
                    forces = attributes.w / deltaTime * (advectedVelocity / kernelSum - inputVelocities[count]);

                    // clumping:
                    forces += 0.5f / deltaTime * (avgPos / kernelSum - diffusePos);

                    // buoyancy:
                    forces += new float4(0, inputVelocities[count].w, 0, 0);

                }
                else // ballistic foam:
                {
                    // gravity:
                    forces += new float4(parameters.gravity * parameters.foamGravityScale, 0);
                }

                // don't change 4th component, as its used to store buoyancy control parameter.
                forces[3] = 0;

                // update particle data:
                attributes.x -= attributes.y * deltaTime;
                outputAttributes[aliveCount] = attributes;
                outputColors[aliveCount] = inputColors[count];

                // integrate:
                outputVelocities[aliveCount] = inputVelocities[count] + forces * deltaTime;
                outputPositions[aliveCount] = new float4((inputPositions[count] + outputVelocities[aliveCount] * deltaTime).xyz,neighbourCount);
            }
        }
    }

    [BurstCompile]
    unsafe struct CopyJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<float4> inputPositions;
        [ReadOnly] public NativeArray<float4> inputVelocities;
        [ReadOnly] public NativeArray<float4> inputColors;
        [ReadOnly] public NativeArray<float4> inputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<float4> outputPositions;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputVelocities;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputColors;
        [NativeDisableParallelForRestriction] public NativeArray<float4> outputAttributes;

        [NativeDisableParallelForRestriction] public NativeArray<int> dispatchBuffer;

        public void Execute(int i)
        {
            if (i == 0)
            {
                dispatchBuffer[3] = dispatchBuffer[7];
                dispatchBuffer[7] = 0;
            }

            outputPositions[i] = inputPositions[i];
            outputVelocities[i] = inputVelocities[i];
            outputColors[i] = inputColors[i];
            outputAttributes[i] = inputAttributes[i];
        }
    }
}
#endif