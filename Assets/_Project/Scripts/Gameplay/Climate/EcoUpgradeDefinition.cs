using UnityEngine;

namespace Sastermate.Gameplay.Climate
{
    [CreateAssetMenu(fileName = "NewEcoUpgrade", menuName = "Sastermate/Climate/Eco Upgrade")]
    public sealed class EcoUpgradeDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Eco Upgrade";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField, Min(0)] private int carbonCost = 150;
        [Tooltip("Fraction of the original fire warming prevented by each purchase. 0.1 means 10%.")]
        [SerializeField, Range(0f, 1f)] private float warmingReduction = 0.1f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public int CarbonCost => Mathf.Max(0, carbonCost);
        public float WarmingReduction => float.IsNaN(warmingReduction) || float.IsInfinity(warmingReduction)
            ? 0f : Mathf.Clamp01(warmingReduction);
    }
}
