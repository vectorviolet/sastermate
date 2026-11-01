// Headless fixtures for gameplay/economy logic. They do not emulate Unity rendering or scene lifecycle.
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static readonly List<Object> All = new();
        public static implicit operator bool(Object value) => !ReferenceEquals(value, null);
        public static T FindFirstObjectByType<T>() where T : Object => All.OfType<T>().FirstOrDefault();
        public static T[] FindObjectsByType<T>(FindObjectsSortMode sort) where T : Object => All.OfType<T>().ToArray();
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort) where T : Object =>
            All.OfType<T>().ToArray();
        public static GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            var instance = new GameObject { name = prefab.name, activeSelf = prefab.activeSelf };
            instance.transform.parent = parent;
            instance.transform.localPosition = prefab.transform.localPosition;
            return instance;
        }
    }
    public class ScriptableObject : Object { }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject = new();
        public Transform transform => gameObject.transform;
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
        public MonoBehaviour() { All.Add(this); }
        public T GetComponent<T>() where T : class => gameObject.Components.Values.OfType<T>().FirstOrDefault();
        public T GetComponentInChildren<T>(bool includeInactive) where T : class => GetComponent<T>();
    }
    public class GameObject : Object
    {
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (!transform.parent || transform.parent.gameObject.activeInHierarchy);
        public readonly Transform transform;
        public readonly Dictionary<Type, object> Components = new();
        public SceneManagement.Scene scene = SceneManagement.SceneManager.ActiveScene;
        public GameObject() { transform = new Transform(this); }
        public void SetActive(bool value) => activeSelf = value;
        public T AddComponent<T>() where T : MonoBehaviour, new()
        {
            var component = new T { gameObject = this };
            Components[typeof(T)] = component;
            return component;
        }
    }
    public class Transform : Object
    {
        public readonly GameObject gameObject;
        public Transform parent;
        public Vector3 position;
        public Vector3 localPosition;
        public Transform(GameObject value) => gameObject = value;
        public bool IsChildOf(Transform other) => this == other || (parent && parent.IsChildOf(other));
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : MonoBehaviour =>
            All.OfType<T>().Where(x => x.transform.IsChildOf(this)).ToArray();
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 zero => default;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
    }
    public struct RaycastHit { }
    public struct Color { public static Color green, cyan; }
    public static class Gizmos { public static Color color; public static void DrawWireCube(Vector3 center, Vector3 size) { } }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp(float value, float min, float max) => Math.Min(max, Math.Max(min, value));
        public static float Clamp01(float value) => Clamp(value, 0, 1);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int CeilToInt(float value) => (int)Math.Ceiling(value);
        public static int FloorToInt(float value) => (int)Math.Floor(value);
    }
    public static class Time { public static float deltaTime = 1, timeScale = 1; }
    public static class Debug
    {
        public static void Log(object value, Object context = null) { }
        public static void LogWarning(object value, Object context = null) { }
        public static void LogException(Exception value, Object context = null) { }
    }
    public static class Application { public static void Quit() { } }
    public enum FindObjectsSortMode { None, InstanceID }
    public enum FindObjectsInactive { Include }
    public class SerializeField : Attribute { }
    public class DisallowMultipleComponent : Attribute { }
    public class Header : Attribute { public Header(string value) { } }
    public class Tooltip : Attribute { public Tooltip(string value) { } }
    public class TextAreaAttribute : Attribute { public TextAreaAttribute(int min, int max) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int value) { } }
    public class RequireComponent : Attribute { public RequireComponent(Type value) { } }
    public class AddComponentMenu : Attribute { public AddComponentMenu(string value) { } }
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string path; public bool IsValid() => !string.IsNullOrEmpty(path); }
    public enum LoadSceneMode { Single, Additive }
    public static class SceneManager
    {
        public static Scene ActiveScene = new() { path = "Assets/_Project/Scenes/DemoScene.unity" };
        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        public static int LoadCalls;
        public static string RequestedPath;
        public static bool ThrowOnLoad;
        public static Scene GetActiveScene() => ActiveScene;
        public static object LoadSceneAsync(string path, LoadSceneMode mode)
        {
            if (ThrowOnLoad) throw new InvalidOperationException("Simulated load failure");
            LoadCalls++; RequestedPath = path;
            return new object();
        }
        public static void Complete(string path) => sceneLoaded?.Invoke(new Scene { path = path }, LoadSceneMode.Single);
        public static void Reset() { sceneLoaded = null; LoadCalls = 0; RequestedPath = null; ThrowOnLoad = false; }
    }
}
namespace UnityEngine.UI
{
    public class Slider : UnityEngine.Object
    {
        public float minValue, maxValue, value;
        public bool wholeNumbers, interactable;
        public void SetValueWithoutNotify(float value) => this.value = value;
    }
    public class Button : UnityEngine.Object
    {
        public bool interactable;
        public readonly ClickEvent onClick = new();
        public sealed class ClickEvent
        {
            private event Action Listeners;
            public void AddListener(Action callback) => Listeners += callback;
            public void RemoveListener(Action callback) => Listeners -= callback;
            public void Invoke() => Listeners?.Invoke();
        }
    }
}
namespace TMPro
{
    public class TMP_Text : UnityEngine.Object { public string text; }
    public class TextMeshProUGUI : TMP_Text { }
}
namespace UnityEngine.EventSystems
{
    public class PointerEventData
    {
        public enum InputButton { Left, Right, Middle }
        public InputButton button;
    }
    public interface IPointerEnterHandler { void OnPointerEnter(PointerEventData data); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData data); }
    public interface IPointerDownHandler { void OnPointerDown(PointerEventData data); }
}
namespace VectorViolet.ProAppState
{
    public abstract class AppStateManager<TSelf, TState> : UnityEngine.MonoBehaviour
        where TSelf : AppStateManager<TSelf, TState> where TState : struct
    {
        public static TSelf Instance;
        protected bool IsDuplicate => Instance && !ReferenceEquals(Instance, this);
        public TState CurrentState { get; private set; }
        protected virtual TState InitialState => default;
        public event Action<TState> StateChanged;
        protected virtual void Awake()
        {
            if (Instance) return;
            Instance = this as TSelf;
            CurrentState = InitialState;
        }
        public void SetState(TState state) { CurrentState = state; StateChanged?.Invoke(state); }
        public static bool IsInState(TState state) => Instance && EqualityComparer<TState>.Default.Equals(Instance.CurrentState, state);
    }
}
namespace VectorViolet.ProPanelManager
{
    public interface IPanelService { bool IsVisible(string panelId); }
    public static class PanelService { public static IPanelService Current; }
}
namespace Sastermate.Core.App
{
    public abstract class GameStateListener : UnityEngine.MonoBehaviour
    {
        protected virtual void OnEnable()
        {
            if (!GameManager.Instance) return;
            GameManager.Instance.StateChanged += OnGameStateChanged;
            OnGameStateChanged(GameManager.Instance.CurrentState);
        }
        protected virtual void OnDisable()
        {
            if (GameManager.Instance) GameManager.Instance.StateChanged -= OnGameStateChanged;
        }
        protected abstract void OnGameStateChanged(GameState state);
    }
}
namespace Sastermate.Core.Events
{
    public static partial class EventBus
    {
        public static class Input { public static event Action OnPausePressed, OnBackPressed; }
    }
}
namespace Sastermate.Gameplay.Fire { public class BurnableObject : UnityEngine.MonoBehaviour { public bool IsBurning; } }
namespace VectorViolet.ProSaveManager
{
    public interface ISerializationService { }
    public interface IStorageService { }
    public class SaveManager { public SaveManager(ISerializationService serializer, IStorageService storage, string name) { } }
}
namespace VectorViolet.ProSaveManager.Implementations
{
    public class JsonSerializationService : VectorViolet.ProSaveManager.ISerializationService { }
    public class LocalFileStorageService : VectorViolet.ProSaveManager.IStorageService { }
}
