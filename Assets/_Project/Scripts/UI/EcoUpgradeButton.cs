using Sastermate.Core.Events;
using Sastermate.Gameplay.Climate;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sastermate.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Sastermate/UI/Eco Upgrade Button")]
    public sealed class EcoUpgradeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        [SerializeField] private EcoUpgradeManager upgrades;
        [SerializeField] private EcoUpgradeDefinition upgrade;

        private Button _button;
        private string _tooltip;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (!upgrades) upgrades = FindFirstObjectByType<EcoUpgradeManager>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(Buy);
            if (upgrades) upgrades.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(Buy);
            if (upgrades) upgrades.Changed -= Refresh;
            EventBus.Tooltip.RequestHide(this);
        }

        public void Buy()
        {
            if (upgrades && upgrade) upgrades.TryPurchase(upgrade);
            Refresh();
        }

        private void Refresh()
        {
            var status = upgrades ? upgrades.GetPurchaseStatus(upgrade) : EcoPurchaseStatus.MissingClimate;
            _button.interactable = status == EcoPurchaseStatus.Available;
            _tooltip = BuildTooltip(status);
            EventBus.Tooltip.RequestUpdate(this, _tooltip);
        }

        private string BuildTooltip(EcoPurchaseStatus status)
        {
            if (!upgrade) return "Upgrade unavailable";
            var reduction = upgrade.WarmingReduction;
            if (upgrades && upgrades.Climate)
            {
                reduction = Mathf.Min(reduction, Mathf.Max(0f,
                    upgrades.Climate.MaximumWarmingReduction - upgrades.Climate.WarmingReduction));
            }
            var count = upgrades ? upgrades.GetBuiltCount(upgrade) : 0;
            var costColor = status == EcoPurchaseStatus.InsufficientCP ? "#FF8888" : "#FFE299";
            var description = string.IsNullOrWhiteSpace(upgrade.Description) ? "" : upgrade.Description + "\n\n";
            var availability = status switch
            {
                EcoPurchaseStatus.Available => "Click to build",
                EcoPurchaseStatus.InsufficientCP => "Not enough CP",
                EcoPurchaseStatus.NoFreeSites => count > 0 ? "All sites built" : "No available building location",
                EcoPurchaseStatus.MaximumProtection => "Maximum protection reached",
                EcoPurchaseStatus.NotInGameplay => "Available during gameplay",
                EcoPurchaseStatus.Busy => "Building...",
                _ => "Unavailable"
            };
            return $"<b>{upgrade.DisplayName}</b>\n{description}" +
                $"Cost: <color={costColor}>{upgrade.CarbonCost:N0} CP</color>\n" +
                $"Prevents {reduction * 100f:0.#}% of base warming\nBuilt: {count}\n\n{availability}";
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Refresh();
            EventBus.Tooltip.RequestShow(this, _tooltip);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Refresh();
            EventBus.Tooltip.RequestShow(this, _tooltip, immediate: true);
        }

        public void OnPointerExit(PointerEventData eventData) => EventBus.Tooltip.RequestHide(this);
    }
}
