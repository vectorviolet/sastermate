using Sastermate.Gameplay.Fire;
using UnityEngine;

namespace Sastermate.UI.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class FireWarningMarker : MonoBehaviour
    {
        [SerializeField] private RectTransform directionArrow;
        [SerializeField, Min(0f)] private float arrowOffset = 34f;

        public BurnableObject Target { get; private set; }

        public void Bind(BurnableObject target)
        {
            Target = target;
            var group = GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        public void Place(Vector2 localPosition, Vector2 direction)
        {
            transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            if (directionArrow && direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                directionArrow.anchoredPosition = direction * arrowOffset;
                directionArrow.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
            SetVisible(true);
        }
    }
}
