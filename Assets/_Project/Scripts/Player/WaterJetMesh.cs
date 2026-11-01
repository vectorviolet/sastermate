using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sastermate.Player
{
    /// <summary>A narrow tube following the simulated water; all buffers are allocated once.</summary>
    internal sealed class WaterJetMesh : IDisposable
    {
        private const int Sides = 8;
        private const int RingSize = Sides + 1;
        private const int MaxPoints = WaterJetSimulation.Capacity + 2;
        private readonly Vector3[] _vertices = new Vector3[MaxPoints * RingSize];
        private readonly Vector3[] _normals = new Vector3[MaxPoints * RingSize];
        private readonly Vector4[] _tangents = new Vector4[MaxPoints * RingSize];
        private readonly Vector2[] _uv = new Vector2[MaxPoints * RingSize];
        private readonly Color[] _colors = new Color[MaxPoints * RingSize];
        private readonly int[] _indices = new int[(MaxPoints - 1) * Sides * 6];
        private readonly GameObject _object;
        private readonly Mesh _mesh;
        private readonly MeshRenderer _renderer;

        public WaterJetMesh(Transform parent, Material material, Color tint, float depthFade)
        {
            _object = new GameObject("Hose Jet");
            _object.transform.SetParent(parent, false);
            _object.layer = parent.gameObject.layer;
            _mesh = new Mesh { name = "Simulated hose jet" };
            _mesh.MarkDynamic();
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _object.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color01", tint);
            properties.SetFloat("_DepthFade", depthFade);
            properties.SetFloat("_ColorStrength", 1f);
            properties.SetFloat("_OpacityMult", 1.5f);
            _renderer.SetPropertyBlock(properties);
            for (var i = 0; i < _colors.Length; i++) _colors[i] = Color.white;
            Hide();
        }

        public void Hide() => _renderer.enabled = false;

        public void Draw(Vector3[] points, int count, float radius, float maximumJoinDistance)
        {
            if (count < 2) { Hide(); return; }
            var transform = _object.transform;
            var normalMatrix = transform.localToWorldMatrix.transpose;
            var totalLength = 0f;
            for (var i = 1; i < count; i++) totalLength += Vector3.Distance(points[i - 1], points[i]);
            var distance = 0f;
            var indices = 0;
            for (var i = 0; i < count; i++)
            {
                if (i > 0) distance += Vector3.Distance(points[i - 1], points[i]);
                var tangent = (points[Mathf.Min(count - 1, i + 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.forward;
                var reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
                var right = Vector3.Cross(tangent, reference).normalized;
                var up = Vector3.Cross(right, tangent).normalized;
                var thickness = radius * (1f + 0.08f * Mathf.Sin(i * 1.7f + Time.time * 25f));
                for (var side = 0; side <= Sides; side++)
                {
                    var angle = side * (Mathf.PI * 2f / Sides);
                    var normal = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    var index = i * RingSize + side;
                    _vertices[index] = transform.InverseTransformPoint(points[i] + normal * thickness);
                    _normals[index] = normalMatrix.MultiplyVector(normal).normalized;
                    var around = transform.InverseTransformVector(-right * Mathf.Sin(angle) + up * Mathf.Cos(angle)).normalized;
                    _tangents[index] = new Vector4(around.x, around.y, around.z, 1f);
                    _uv[index] = new Vector2((float)side / Sides, 1f - distance / Mathf.Max(0.001f, totalLength));
                }
                if (i == 0 || Vector3.Distance(points[i - 1], points[i]) > maximumJoinDistance) continue;
                for (var side = 0; side < Sides; side++)
                {
                    var a = (i - 1) * RingSize + side;
                    var b = i * RingSize + side;
                    _indices[indices++] = a; _indices[indices++] = b; _indices[indices++] = a + 1;
                    _indices[indices++] = a + 1; _indices[indices++] = b; _indices[indices++] = b + 1;
                }
            }
            _mesh.Clear(false);
            var vertices = count * RingSize;
            _mesh.SetVertices(_vertices, 0, vertices);
            _mesh.SetNormals(_normals, 0, vertices);
            _mesh.SetTangents(_tangents, 0, vertices);
            _mesh.SetUVs(0, _uv, 0, vertices);
            _mesh.SetColors(_colors, 0, vertices);
            _mesh.SetIndices(_indices, 0, indices, MeshTopology.Triangles, 0, true);
            _renderer.enabled = indices > 0;
        }

        public void Dispose()
        {
            if (_object) UnityEngine.Object.Destroy(_object);
            if (_mesh) UnityEngine.Object.Destroy(_mesh);
        }
    }
}
