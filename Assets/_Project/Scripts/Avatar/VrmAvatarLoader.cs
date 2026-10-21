using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UniVRM10;

namespace AICompanion.Avatar
{
    static class VrmAvatarLoader
    {
        public static Task<Vrm10Instance> LoadAsync(string path, CancellationToken cancellationToken)
        {
            return Vrm10.LoadPathAsync(
                path,
                canLoadVrm0X: true,
                showMeshes: true,
                ct: cancellationToken);
        }

        public static void CollectPresetShapes(Vrm10Instance instance, List<KeyValuePair<ExpressionPreset, string>> into)
        {
            if (instance == null || instance.Vrm == null || instance.Vrm.Expression == null || into == null)
                return;

            foreach (var pair in instance.Vrm.Expression.Clips)
            {
                var preset = ToLocal(pair.Preset);
                if (preset == ExpressionPreset.None || pair.Clip == null || pair.Clip.MorphTargetBindings == null)
                    continue;

                var bindings = pair.Clip.MorphTargetBindings;
                for (var i = 0; i < bindings.Length; i++)
                {
                    var shape = BlendShapeName(instance.transform, bindings[i]);
                    if (!string.IsNullOrEmpty(shape))
                        into.Add(new KeyValuePair<ExpressionPreset, string>(preset, shape));
                }
            }
        }

        static ExpressionPreset ToLocal(UniVRM10.ExpressionPreset preset)
        {
            switch (preset)
            {
                case UniVRM10.ExpressionPreset.aa: return ExpressionPreset.Aa;
                case UniVRM10.ExpressionPreset.ih: return ExpressionPreset.Ih;
                case UniVRM10.ExpressionPreset.ou: return ExpressionPreset.Ou;
                case UniVRM10.ExpressionPreset.ee: return ExpressionPreset.Ee;
                case UniVRM10.ExpressionPreset.oh: return ExpressionPreset.Oh;
                case UniVRM10.ExpressionPreset.blink:
                case UniVRM10.ExpressionPreset.blinkLeft:
                case UniVRM10.ExpressionPreset.blinkRight:
                    return ExpressionPreset.Blink;
                case UniVRM10.ExpressionPreset.happy: return ExpressionPreset.Joy;
                case UniVRM10.ExpressionPreset.angry: return ExpressionPreset.Angry;
                case UniVRM10.ExpressionPreset.sad: return ExpressionPreset.Sorrow;
                case UniVRM10.ExpressionPreset.relaxed: return ExpressionPreset.Fun;
                default: return ExpressionPreset.None;
            }
        }

        static string BlendShapeName(Transform root, MorphTargetBinding binding)
        {
            if (root == null || binding.Index < 0)
                return null;

            var target = string.IsNullOrEmpty(binding.RelativePath) ? root : root.Find(binding.RelativePath);
            if (target == null)
                return null;

            var renderer = target.GetComponent<SkinnedMeshRenderer>();
            var mesh = renderer != null ? renderer.sharedMesh : null;
            if (mesh == null || binding.Index >= mesh.blendShapeCount)
                return null;

            return mesh.GetBlendShapeName(binding.Index);
        }
    }
}
