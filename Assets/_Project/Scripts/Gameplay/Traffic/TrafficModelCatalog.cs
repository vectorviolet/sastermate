using UnityEngine;

namespace FactorySabotage.Gameplay.Traffic
{
    [CreateAssetMenu(fileName = "TrafficModels", menuName = "Climate Guardians/Traffic/Model Catalog")]
    public sealed class TrafficModelCatalog : ScriptableObject
    {
        [Tooltip("Visual FBX models or visual-only prefabs. Movement, Rigidbody, and the main collider belong on TrafficCar.")]
        [SerializeField] private GameObject[] models;
        [SerializeField] private Vector3 modelScale = Vector3.one;
        [Tooltip("Shared correction so the models face the car root's +Z direction.")]
        [SerializeField] private Vector3 modelRotation;
        [Tooltip("Center the chosen model on the car root in XZ and put its lowest rendered point at root Y.")]
        [SerializeField] private bool centerAndGround;

        internal Vector3 ModelScale => new(Mathf.Max(0.001f, modelScale.x),
            Mathf.Max(0.001f, modelScale.y), Mathf.Max(0.001f, modelScale.z));
        internal Quaternion ModelRotation => Quaternion.Euler(modelRotation);
        internal bool CenterAndGround => centerAndGround;

        internal bool TryPick(out GameObject model)
        {
            model = null;
            if (models == null) return false;
            var eligible = 0;
            foreach (var candidate in models)
            {
                if (!candidate || !candidate.GetComponentInChildren<Renderer>(true) ||
                    candidate.GetComponentInChildren<TrafficVehicle>(true) ||
                    candidate.GetComponentInChildren<Rigidbody>(true)) continue;
                if (Random.Range(0, ++eligible) == 0) model = candidate;
            }
            return model;
        }
    }
}
