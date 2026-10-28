using UnityEngine;
using UnityEngine.Rendering;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Two soft quads parented to the head bone. Used when the mesh has no blush shape.
    /// Offsets are in head-local space: +Z is treated as the face direction.
    /// </summary>
    public static class BlushPlacement
    {
        public static readonly Vector3 LeftLocal = new Vector3(-0.038f, 0.025f, 0.09f);
        public static readonly Vector3 RightLocal = new Vector3(0.038f, 0.025f, 0.09f);
        public const float Size = 0.055f;
    }

    public sealed class BlushOverlay
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static Mesh _quad;

        Transform _left;
        Transform _right;
        Material _material;
        bool _visible;

        public bool Ready
        {
            get { return _material != null && _left != null && _right != null; }
        }

        public static string FormatLog(bool enabled, bool hasShape, bool hasHead, bool materialReady)
        {
            if (!enabled)
                return "Blush: off";
            if (hasShape)
                return "Blush: blendshape";
            if (!hasHead)
                return "Blush: no head bone, overlay skipped";
            if (!materialReady)
                return "Blush: no material, overlay skipped";
            return "Blush: procedural overlay";
        }

        public bool Attach(Transform head)
        {
            Release();
            if (head == null || !BlushSettings.Enabled)
                return false;

            _material = CreateMaterial();
            if (_material == null)
                return false;

            var mesh = Quad();
            _left = CreateCheek("CheekBlushL", head, BlushPlacement.LeftLocal, mesh, _material);
            _right = CreateCheek("CheekBlushR", head, BlushPlacement.RightLocal, mesh, _material);
            _visible = true;
            SetIntensity(0f);
            return Ready;
        }

        public void SetIntensity(float amount)
        {
            if (!Ready)
                return;

            if (!BlushSettings.Enabled || float.IsNaN(amount) || amount < 0f)
                amount = 0f;
            else if (amount > 1f)
                amount = 1f;

            _material.SetFloat(IntensityId, amount);
            var visible = amount > 0.001f;
            if (visible == _visible)
                return;

            _visible = visible;
            _left.gameObject.SetActive(visible);
            _right.gameObject.SetActive(visible);
        }

        public void Release()
        {
            DestroyObject(_left);
            DestroyObject(_right);
            DestroyObject(_material);
            _left = null;
            _right = null;
            _material = null;
            _visible = false;
        }

        static Material CreateMaterial()
        {
            var template = Resources.Load<Material>("CheekBlush");
            Material material = null;
            if (template != null)
                material = new Material(template);
            else
            {
                var shader = Shader.Find("AICompanion/CheekBlush");
                if (shader != null)
                    material = new Material(shader);
            }

            if (material == null)
                return null;

            material.name = "CheekBlushInstance";
            material.hideFlags = HideFlags.HideAndDontSave;
            material.SetTexture(BaseMapId, BlushTexture.Shared);
            material.SetFloat(IntensityId, 0f);
            return material;
        }

        static Transform CreateCheek(string name, Transform head, Vector3 localPosition, Mesh mesh, Material material)
        {
            var cheek = new GameObject(name);
            cheek.transform.SetParent(head, false);
            cheek.transform.localPosition = localPosition;
            cheek.transform.localRotation = Quaternion.identity;
            cheek.transform.localScale = new Vector3(BlushPlacement.Size, BlushPlacement.Size, 1f);

            var filter = cheek.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = cheek.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return cheek.transform;
        }

        static Mesh Quad()
        {
            if (_quad != null)
                return _quad;

            var mesh = new Mesh { name = "CheekBlushQuad" };
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            _quad = mesh;
            return mesh;
        }

        static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
