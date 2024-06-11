using System;
using System.Collections.Generic;
using UnityEngine;

namespace Obi
{
    public class InstancedRenderBatch : IRenderBatch
    {
        public RenderParams renderParams;
        public Mesh mesh;
        public Material material;

        public int firstRenderer;

        public int firstInstance;
        public int instanceCount;

        public GraphicsBuffer argsBuffer; 

        public InstancedRenderBatch(int rendererIndex, Mesh mesh, Material material)
        {
            this.renderParams = new RenderParams();
            this.firstRenderer = rendererIndex;
            this.mesh = mesh;
            this.material = material;
            this.firstInstance = 0;
            this.instanceCount = 0;
        }

        public void Initialize(bool gpu = false)
        {
            if (gpu)
                argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
        }

        public void Dispose()
        {
            argsBuffer?.Dispose();
            argsBuffer = null;
        }

        public bool TryMergeWith(IRenderBatch other)
        {
            var ibatch = other as InstancedRenderBatch;
            if (ibatch != null)
            {
                if (material == ibatch.material &&
                    mesh == ibatch.mesh &&
                    instanceCount + ibatch.instanceCount < Constants.maxInstancesPerBatch)
                {
                    instanceCount += ibatch.instanceCount;
                    return true;
                }
            }
            return false;
        }

        public int CompareTo(IRenderBatch other)
        {
            var ibatch = other as InstancedRenderBatch;
            int compareMat = material.GetInstanceID().CompareTo(ibatch.material.GetInstanceID());
            if (compareMat == 0)
                return mesh.GetInstanceID().CompareTo(ibatch.mesh.GetInstanceID());

            return compareMat;
        }
    }
}
