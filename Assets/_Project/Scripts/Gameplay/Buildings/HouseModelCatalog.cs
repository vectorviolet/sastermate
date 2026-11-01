using UnityEngine;

namespace Sastermate.Gameplay.Buildings
{
    [CreateAssetMenu(fileName = "HouseModels", menuName = "Sastermate/Buildings/Model Catalog")]
    public sealed class HouseModelCatalog : ScriptableObject
    {
        [Tooltip("Single-mesh FBX models with a MeshFilter and MeshRenderer on their root. Gameplay components stay on BaseHouse.")]
        [SerializeField] private GameObject[] models;

        internal bool TryPick(out GameObject model)
        {
            model = null;
            if (models == null) return false;
            var eligible = 0;
            foreach (var candidate in models)
            {
                if (!IsUsableModel(candidate)) continue;
                if (Random.Range(0, ++eligible) == 0) model = candidate;
            }
            return model;
        }

        internal static bool IsUsableModel(GameObject model)
        {
            if (!model || !model.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh ||
                !model.TryGetComponent<MeshRenderer>(out _) ||
                model.GetComponentsInChildren<MeshFilter>(true).Length != 1) return false;

            // Copying a mesh onto the shared root requires the FBX's root transform to be neutral.
            return model.transform.localPosition == Vector3.zero &&
                   Quaternion.Angle(model.transform.localRotation, Quaternion.identity) < 0.001f &&
                   model.transform.localScale == Vector3.one;
        }
    }
}
