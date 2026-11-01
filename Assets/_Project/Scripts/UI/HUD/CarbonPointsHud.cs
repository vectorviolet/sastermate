using FactorySabotage.Gameplay.Tasks;
using TMPro;
using UnityEngine;

namespace FactorySabotage.UI.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class CarbonPointsHud : MonoBehaviour
    {
        [SerializeField] private TaskRewardManager rewards;

        private TMP_Text _text;

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            if (!rewards)
            {
                Debug.LogWarning("[CarbonPointsHud] Assign the scene's Task Reward Manager.", this);
                return;
            }
            rewards.CarbonPointsChanged += Refresh;
            Refresh(rewards.CarbonPoints);
        }

        private void OnDisable()
        {
            if (rewards) rewards.CarbonPointsChanged -= Refresh;
        }

        private void Refresh(int carbonPoints) => _text.text = $"{carbonPoints:N0} CP";
    }
}
