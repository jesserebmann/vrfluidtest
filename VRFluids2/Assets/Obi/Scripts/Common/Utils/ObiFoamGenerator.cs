using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
	/**
	 * Foam generators create diffuse particles in areas where certain conditions meet (high velocity constrasts, high vorticity, low density, high normal values, etc.). These particles
	 * are then advected trough the fluid velocity field.
	 */

    [AddComponentMenu("Physics/Obi/Obi Foam Generator", 1000)]
    [ExecuteInEditMode]
    [RequireComponent(typeof(ObiActor))]
    public class ObiFoamGenerator : MonoBehaviour, ObiActorRenderer<ObiFoamGenerator>
    {
        public ObiActor actor { get; private set; }

        [Header("Foam spawning")]
        public float foamGenerationRate = 250;
        public Vector2 velocityRange = new Vector2(2, 4);
        public Vector2 vorticityRange = new Vector2(4, 8);

        [Header("Foam properties")]
        public Color color = new Color(1,1,1,0.25f);
        public float size = 0.02f;
        [Range(0,1)]
        public float sizeRandom = 0.2f;
        public float lifetime = 5;
        [Range(0, 1)]
        public float lifetimeRandom = 0.2f;

        public float buoyancy = 10;

        [NonSerialized] public ObiNativeFloatList emitPotential;

        [Range(0, 1)]
        public float drag = 0.5f;

        public void Awake()
        {
            actor = GetComponent<ObiActor>();
            emitPotential = new ObiNativeFloatList();

            if (actor.solver == null)
            {
                actor.OnBlueprintLoaded += Actor_OnBlueprintLoaded;
                actor.OnBlueprintUnloaded += Actor_OnBlueprintUnloaded;
            }
            else
            {
                // set initial size of potential list, then subscribe to changes in solver particle count.
                Solver_OnParticleCountChanged(actor.solver);
                Actor_OnBlueprintLoaded(actor, actor.sharedBlueprint);
            }
        }


        public void OnDestroy()
        {
            actor.OnBlueprintLoaded -= Actor_OnBlueprintLoaded;
            actor.OnBlueprintUnloaded -= Actor_OnBlueprintUnloaded;

            if (emitPotential != null && emitPotential.isCreated)
                emitPotential.Dispose();
        }

        private void Actor_OnBlueprintLoaded(ObiActor act, ObiActorBlueprint blueprint)
        {
            actor.solver.OnParticleCountChanged += Solver_OnParticleCountChanged;
        }

        private void Actor_OnBlueprintUnloaded(ObiActor act, ObiActorBlueprint blueprint)
        {
            actor.solver.OnParticleCountChanged -= Solver_OnParticleCountChanged;
        }

        private void Solver_OnParticleCountChanged(ObiSolver solver)
        {
            if (solver.positions.count > 0)
            {
                emitPotential.ResizeInitialized(solver.positions.count);
            }
        }

        public void OnEnable()
        {
            ((ObiActorRenderer<ObiFoamGenerator>)this).EnableRenderer();
        }

        public void OnDisable()
        {
            ((ObiActorRenderer<ObiFoamGenerator>)this).DisableRenderer();
        }

        public void OnValidate()
        {
            ((ObiActorRenderer<ObiFoamGenerator>)this).SetRendererDirty(Oni.RenderingSystemType.FoamParticles);
        }

        RenderSystem<ObiFoamGenerator> ObiRenderer<ObiFoamGenerator>.CreateRenderSystem(ObiSolver solver)
        {
            switch (solver.backendType)
            {

#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
                case ObiSolver.BackendType.Burst: return new BurstFoamRenderSystem(solver);
#endif
                case ObiSolver.BackendType.Compute:
                default:

                    if (SystemInfo.supportsComputeShaders)
                        return new ComputeFoamRenderSystem(solver);
                    return null;
            }
        }
    }
}
