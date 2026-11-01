using FactorySabotage.Gameplay.Fire;
using UnityEngine;

namespace FactorySabotage.Gameplay.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    [RequireComponent(typeof(BurnableObject))]
    [DefaultExecutionOrder(-200)]
    public sealed class HouseModelPicker : MonoBehaviour
    {
        [SerializeField] private HouseModelCatalog modelCatalog;
        [SerializeField] private Transform fireAnchor;
        [Tooltip("Choose a new model when gameplay starts. Turn off to keep the saved scene selection.")]
        [SerializeField] private bool randomizeOnAwake = true;
        [SerializeField, HideInInspector] private GameObject selectedModel;

        public GameObject SelectedModel => selectedModel;

        private void Awake()
        {
            // Run before BurnableObject and RandomFireController initialize.
            if (randomizeOnAwake || !selectedModel) RandomizeModel();
            else SetModel(selectedModel);
        }

        [ContextMenu("Randomize House Model")]
        private void RandomizeFromMenu() => RandomizeModel();

        public bool RandomizeModel()
        {
            if (!modelCatalog || !modelCatalog.TryPick(out var model))
            {
                Debug.LogWarning("[HouseModelPicker] Assign a House Model Catalog with single-mesh FBX models.", this);
                return false;
            }
            return SetModel(model);
        }

        public bool SetModel(GameObject model)
        {
            if (!HouseModelCatalog.IsUsableModel(model)) return false;
            var burnable = GetComponent<BurnableObject>();
            // Keep a burning target's collider and flame origin stable until it is extinguished.
            if (burnable.IsBurning) return false;
            if (!fireAnchor || fireAnchor.parent != transform)
            {
                Debug.LogWarning("[HouseModelPicker] Assign a FireAnchor directly under this house.", this);
                return false;
            }

            var filter = GetComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            var collider = GetComponent<MeshCollider>();
            var changedObjects = new Object[] { this, filter, renderer, collider, burnable, fireAnchor };
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RecordObjects(changedObjects, "Randomize house model");
#endif

            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            filter.sharedMesh = mesh;
            renderer.sharedMaterials = model.GetComponent<MeshRenderer>().sharedMaterials;
            collider.sharedMesh = mesh;
            collider.convex = false;
            collider.isTrigger = false;

            var bounds = mesh.bounds;
            var origin = bounds.center;
            origin.y = Mathf.Lerp(bounds.min.y, bounds.max.y, 0.9f);
            fireAnchor.localPosition = origin;
            burnable.ConfigureFireVisuals(fireAnchor,
                Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.4f, 0.15f, 0.7f));
            selectedModel = model;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                foreach (var changed in changedObjects)
                {
                    UnityEditor.EditorUtility.SetDirty(changed);
                    if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(changed))
                        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
                }
#endif
            return true;
        }
    }
}
