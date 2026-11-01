using UnityEngine;

namespace FactorySabotage.Core.Utilities
{
    public class HashedAnimatorParameters
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int IsSpraying = Animator.StringToHash("IsSpraying");
        public static readonly int Horizontal = Animator.StringToHash("Horizontal");
        public static readonly int Vertical = Animator.StringToHash("Vertical");
    }
}
