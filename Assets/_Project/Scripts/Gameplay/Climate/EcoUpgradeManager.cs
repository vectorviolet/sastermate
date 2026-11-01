using System;
using Sastermate.Core.App;
using UnityEngine;

namespace Sastermate.Gameplay.Climate
{
    public enum EcoPurchaseStatus
    {
        Available,
        MissingClimate,
        InvalidUpgrade,
        NotInGameplay,
        MaximumProtection,
        NoFreeSites,
        InsufficientCP,
        Busy
    }

    /// <summary>Purchases infrastructure at prepared sites using the existing climate/CP wallet.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sastermate/Climate/Eco Upgrade Manager")]
    public sealed class EcoUpgradeManager : GameStateListener
    {
        [SerializeField] private ClimateManager climate;
        [Tooltip("Optional root containing the prepared upgrade sites. Empty discovers them throughout the scene.")]
        [SerializeField] private Transform sitesRoot;

        private EcoUpgradeSite[] _sites = Array.Empty<EcoUpgradeSite>();
        private bool _buying;

        public ClimateManager Climate => climate;
        public event Action Changed;
        public event Action<EcoUpgradeSite> Purchased;

        private void Awake()
        {
            if (!climate) climate = FindFirstObjectByType<ClimateManager>();
            RefreshSites();
        }

        // Site Awake may create a hidden prefab model after the buttons first enable.
        private void Start() => NotifyChanged();

        protected override void OnEnable()
        {
            if (climate) climate.EconomyChanged += NotifyChanged;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (climate) climate.EconomyChanged -= NotifyChanged;
            base.OnDisable();
        }

        protected override void OnGameStateChanged(GameState state) => NotifyChanged();

        public void RefreshSites()
        {
            _sites = sitesRoot ? sitesRoot.GetComponentsInChildren<EcoUpgradeSite>(true) :
                FindObjectsByType<EcoUpgradeSite>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            NotifyChanged();
        }

        public EcoPurchaseStatus GetPurchaseStatus(EcoUpgradeDefinition upgrade)
        {
            if (_buying) return EcoPurchaseStatus.Busy;
            if (!climate) return EcoPurchaseStatus.MissingClimate;
            if (!upgrade || upgrade.WarmingReduction <= 0f) return EcoPurchaseStatus.InvalidUpgrade;
            if (!isActiveAndEnabled || !climate.CanPurchaseUpgrades) return EcoPurchaseStatus.NotInGameplay;
            if (climate.WarmingReduction >= climate.MaximumWarmingReduction) return EcoPurchaseStatus.MaximumProtection;
            if (!FindAvailableSite(upgrade)) return EcoPurchaseStatus.NoFreeSites;
            return climate.CarbonPoints < upgrade.CarbonCost ? EcoPurchaseStatus.InsufficientCP : EcoPurchaseStatus.Available;
        }

        public int GetBuiltCount(EcoUpgradeDefinition upgrade)
        {
            var count = 0;
            foreach (var site in _sites)
                if (site && site.Upgrade == upgrade && site.IsBuilt) count++;
            return count;
        }

        public bool TryPurchase(EcoUpgradeDefinition upgrade)
        {
            if (GetPurchaseStatus(upgrade) != EcoPurchaseStatus.Available) return false;
            var site = FindAvailableSite(upgrade);
            _buying = true;
            try
            {
                if (!climate.TryPurchaseWarmingReduction(upgrade.CarbonCost, upgrade.WarmingReduction)) return false;
                site.Build();
                Purchased?.Invoke(site);
                return true;
            }
            finally
            {
                _buying = false;
                NotifyChanged();
            }
        }

        private EcoUpgradeSite FindAvailableSite(EcoUpgradeDefinition upgrade)
        {
            foreach (var site in _sites)
                if (site && site.Upgrade == upgrade && site.IsAvailable) return site;
            return null;
        }

        private void NotifyChanged() => Changed?.Invoke();
    }
}
