using UnityEngine;

namespace Obi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObiCollider))]
	public class ObiForceZone : MonoBehaviour
	{
        public ForceZone.ZoneType type;
        public ForceZone.ForceMode mode;
        public float intensity;

        [Header("Damping")]
        public ForceZone.DampingDirection dampingDir;
        public float damping = 0;

        [Header("Falloff")]
        public float minDistance;
        public float maxDistance;
        [Min(0)]
        public float falloffPower = 1;

        [Header("Pulse")]
        public float pulseIntensity;
        public float pulseFrequency;
        public float pulseSeed;

        public ObiForceZoneHandle handle;

        protected float intensityVariation;

        public void OnEnable()
        {
            handle = ObiColliderWorld.GetInstance().CreateForceZone();
            handle.owner = this;
        }

        public void OnDisable()
        {
            ObiColliderWorld.GetInstance().DestroyForceZone(handle);
        }

        public virtual void UpdateIfNeeded()
        {
            var fc = ObiColliderWorld.GetInstance().forceZones[handle.index];
            fc.type = type;
            fc.mode = mode;
            fc.intensity = intensity + intensityVariation;
            fc.minDistance = minDistance;
            fc.maxDistance = maxDistance;
            fc.falloffPower = falloffPower;
            fc.damping = damping;
            fc.dampingDir = dampingDir;
            ObiColliderWorld.GetInstance().forceZones[handle.index] = fc;
        }

        public void Update()
        {
            intensityVariation = Mathf.PerlinNoise(Time.time * pulseFrequency, pulseSeed) * pulseIntensity;
        }
    }
}

