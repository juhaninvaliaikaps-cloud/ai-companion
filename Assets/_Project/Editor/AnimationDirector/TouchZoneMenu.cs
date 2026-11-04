using System.IO;
using AICompanion.AnimationDirector;
using UnityEditor;
using UnityEngine;

namespace AICompanion.Editor.AnimationDirector
{
    /// <summary>
    /// Creates the default touch-zone asset in Resources, where the overlay loads it by name.
    /// </summary>
    public static class TouchZoneMenu
    {
        public const string AssetPath = "Assets/_Project/Resources/TouchZones.asset";

        [MenuItem("AI Companion/Touch/Create Default Zones")]
        public static void CreateDefault()
        {
            var set = AssetDatabase.LoadAssetAtPath<TouchZoneSet>(AssetPath);
            if (set == null)
            {
                var folder = Path.GetDirectoryName(AssetPath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                set = TouchZoneSet.CreateDefault();
                AssetDatabase.CreateAsset(set, AssetPath);
                Debug.Log("Touch zones created: " + AssetPath);
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
