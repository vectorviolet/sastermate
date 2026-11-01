using UnityEngine;

namespace Sastermate.Gameplay.Climate
{
    /// <summary>A prepared roof or ground spot that can be purchased once per run.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sastermate/Climate/Eco Upgrade Site")]
    public sealed class EcoUpgradeSite : MonoBehaviour
    {
        [SerializeField] private EcoUpgradeDefinition upgrade;
        [Tooltip("A model prefab from the Project window, or a model child placed under this site. Hidden until purchased.")]
        [SerializeField] private GameObject builtVisual;

        public EcoUpgradeDefinition Upgrade => upgrade;
        public bool IsBuilt { get; private set; }
        public bool IsAvailable => isActiveAndEnabled && !IsBuilt && IsConfigured;
        private bool IsConfigured => upgrade && builtVisual && builtVisual != gameObject &&
            (!builtVisual.scene.IsValid() || builtVisual.transform.IsChildOf(transform));

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.LogWarning("[EcoUpgradeSite] Assign an Eco Upgrade and a model prefab or a Built Visual child of this site.", this);
                return;
            }
            EnsureSceneVisual();
            if (!IsBuilt) builtVisual.SetActive(false);
        }

        private void EnsureSceneVisual()
        {
            if (!builtVisual.scene.IsValid())
            {
                builtVisual = Instantiate(builtVisual, transform, false);
                // Prefabs can carry a saved world placement; the site's position is the build location.
                builtVisual.transform.localPosition = Vector3.zero;
            }
        }

        internal void Build()
        {
            // Also covers a purchase before Awake or after a live script reload.
            EnsureSceneVisual();
            IsBuilt = true;
            builtVisual.SetActive(true);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsBuilt ? Color.green : Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.25f, new Vector3(0.5f, 0.5f, 0.5f));
        }
    }
}
