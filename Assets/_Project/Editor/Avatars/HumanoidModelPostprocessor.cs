using AICompanion.Avatar;
using UnityEditor;
using UnityEngine;

namespace AICompanion.Editor.Avatars
{
    /// <summary>
    /// FBX under the avatar folders import as Unity Humanoid, with auto-mapping
    /// when the file has no explicit muscle map. A mesh that still is not humanoid
    /// logs a clear error instead of failing the import.
    /// </summary>
    sealed class HumanoidModelPostprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!IsAvatarPath(assetPath))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.globalScale = 1f;
            importer.useFileScale = true;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!IsAvatarPath(assetPath) || root == null)
                return;

            UpgradeMaterials(root);
            if (root.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
                return;

            var importer = (ModelImporter)assetImporter;
            var human = importer.humanDescription.human;
            if (human != null && human.Length > 0)
                return;

            var animator = root.GetComponent<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
                return;

            if (animator != null && animator.avatar != null)
            {
                Debug.LogError("Humanoid import failed for '" + assetPath + "'. " + AvatarInspection.Inspect(animator).Message);
                return;
            }

            Debug.LogWarning("Humanoid mapping for '" + assetPath + "' could not be confirmed yet. If shared animations do not retarget, set the Rig type to Humanoid and check the bone map. " + HumanoidValidation.RigHint);
        }

        static void UpgradeMaterials(GameObject root)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials;
                for (var m = 0; m < materials.Length; m++)
                {
                    var material = materials[m];
                    if (material == null || shader == null || !NeedsUrpShader(material))
                        continue;

                    var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    if (color.a <= 0f)
                        color.a = 1f;
                    material.shader = shader;
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", color);
                }
            }

            AvatarInspection.EnsureVisibleAlpha(root);
        }

        static bool NeedsUrpShader(Material material)
        {
            var shader = material.shader;
            if (shader == null)
                return true;

            var name = shader.name;
            return name == "Standard"
                || name == "Standard (Specular setup)"
                || name == "Hidden/InternalErrorShader"
                || name.StartsWith("Legacy Shaders/");
        }

        static bool IsAvatarPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.StartsWith("Assets/_Project/Avatars/")
                || path.StartsWith("Assets/_Project/Models/")
                || path.StartsWith("Assets/LocalAvatars/");
        }
    }
}
