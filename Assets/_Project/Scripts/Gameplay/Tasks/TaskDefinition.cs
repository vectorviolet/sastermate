using UnityEngine;

namespace FactorySabotage.Gameplay.Tasks
{
    [CreateAssetMenu(fileName = "NewTask", menuName = "Climate Guardians/Tasks/Task Definition")]
    public sealed class TaskDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "New Task";
        [SerializeField, Min(0)] private int carbonPoints = 100;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int CarbonPoints => Mathf.Max(0, carbonPoints);
    }
}
