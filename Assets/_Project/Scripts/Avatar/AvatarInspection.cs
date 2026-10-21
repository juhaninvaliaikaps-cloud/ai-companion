using System;
using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Unity-side checks. The pure rules live in <see cref="HumanoidValidation"/>
    /// and <see cref="ExpressionPresetMap"/>.
    /// </summary>
    public static class AvatarInspection
    {
        public static HumanoidReport Inspect(Animator animator)
        {
            if (animator == null)
                return HumanoidValidation.ValidateBoneNames(null);

            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                var byName = HumanoidValidation.ValidateBoneNames(BoneNames(animator));
                if (!byName.IsValid)
                    return byName;

                return HumanoidReport.Unmapped();
            }

            var missing = new List<string>();
            var required = HumanoidValidation.RequiredBoneNames;
            for (var i = 0; i < required.Length; i++)
            {
                if (!Enum.TryParse(required[i], out HumanBodyBones bone) || animator.GetBoneTransform(bone) == null)
                    missing.Add(required[i]);
            }

            return missing.Count == 0 ? HumanoidReport.Valid() : HumanoidReport.Missing(missing);
        }

        public static List<string> BlendShapeNames(GameObject root)
        {
            var names = new List<string>();
            if (root == null)
                return names;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var mesh = renderers[i].sharedMesh;
                if (mesh == null)
                    continue;

                var count = mesh.blendShapeCount;
                for (var shape = 0; shape < count; shape++)
                {
                    var name = mesh.GetBlendShapeName(shape);
                    if (string.IsNullOrEmpty(name) || !seen.Add(name))
                        continue;
                    names.Add(name);
                }
            }

            return names;
        }

        public static void EnsureVisibleAlpha(GameObject root)
        {
            if (root == null)
                return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials;
                for (var m = 0; m < materials.Length; m++)
                    LiftZeroAlpha(materials[m]);
            }
        }

        static void LiftZeroAlpha(Material material)
        {
            if (material == null)
                return;

            Lift(material, "_BaseColor");
            Lift(material, "_Color");
        }

        static void Lift(Material material, string property)
        {
            if (!material.HasProperty(property))
                return;

            var color = material.GetColor(property);
            if (color.a > 0f)
                return;

            color.a = 1f;
            material.SetColor(property, color);
        }

        static IEnumerable<string> BoneNames(Animator animator)
        {
            if (animator == null)
                yield break;

            var transforms = animator.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
                yield return transforms[i].name;
        }
    }
}
