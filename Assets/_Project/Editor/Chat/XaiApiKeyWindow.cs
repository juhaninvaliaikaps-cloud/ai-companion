using System;
using AICompanion.AI;
using UnityEditor;
using UnityEngine;

namespace AICompanion.Editor.Chat
{
    /// <summary>
    /// Writes the xAI key to the per-user file. The field is cleared after a save
    /// and the key is not written to the Console.
    /// </summary>
    public sealed class XaiApiKeyWindow : EditorWindow
    {
        string _key = string.Empty;
        string _status = string.Empty;

        [MenuItem("AI Companion/Set xAI API Key")]
        static void Open()
        {
            var window = GetWindow<XaiApiKeyWindow>(true, "xAI API Key", true);
            window.minSize = new Vector2(420f, 180f);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("The key stays outside this repository, for the current user.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(ApiKeyFile.PreferredPath(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            _key = EditorGUILayout.PasswordField("API key", _key);
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.Info);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                    Save();
                if (GUILayout.Button("Remove saved key"))
                    Remove();
            }
        }

        void Save()
        {
            var key = (_key ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                _status = "Enter a key first.";
                return;
            }

            try
            {
                ApiKeyFile.SavePreferred(key);
                _key = string.Empty;
                GUIUtility.keyboardControl = 0;
                _status = "Saved on this PC.";
            }
            catch (Exception)
            {
                _status = "Could not save the API key on this PC.";
            }
        }

        void Remove()
        {
            try
            {
                ApiKeyFile.Delete(ApiKeyFile.PreferredPath());
                var persistent = ApiKeyLocations.PersistentPath(Application.persistentDataPath);
                if (!string.IsNullOrEmpty(persistent))
                    ApiKeyFile.Delete(persistent);
                _status = "Removed the saved key. An XAI_API_KEY environment variable still wins if it is set.";
            }
            catch (Exception)
            {
                _status = "Could not remove the saved key.";
            }
        }
    }
}
