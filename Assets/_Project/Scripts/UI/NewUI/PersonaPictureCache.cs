using System.Collections.Generic;
using AICompanion.Settings;
using UnityEngine.UI;
using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-154: her picture as a texture, loaded once per persona and kept until a picture is
    /// saved or deleted (<see cref="PersonaPictureStore.Revision"/>). The chat header, the
    /// wheel's profile panel and the persona grid share it, and they ask only on a persona
    /// switch or a rebuild, never per frame.
    /// </summary>
    public static class PersonaPictureCache
    {
        sealed class Entry
        {
            public Texture2D Texture;
            public int Revision;
        }

        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

        /// <summary>Her picture, or null when she has none or it can't be read.</summary>
        public static Texture2D Get(string personasFolder, PersonaDefinition persona)
        {
            if (persona == null || !persona.HasPicture || string.IsNullOrEmpty(persona.Id))
                return null;
            var key = PersonaIds.Normalize(persona.Id);
            Entry entry;
            if (Entries.TryGetValue(key, out entry) && entry.Revision == PersonaPictureStore.Revision && entry.Texture != null)
                return entry.Texture;

            if (entry != null && entry.Texture != null)
                Object.Destroy(entry.Texture);
            var texture = PersonaPictureImage.Load(PersonaPictureStore.Load(personasFolder, key));
            if (texture != null)
                texture.hideFlags = HideFlags.DontSave;
            Entries[key] = new Entry { Texture = texture, Revision = PersonaPictureStore.Revision };
            return texture;
        }

        /// <summary>A round or square frame with her picture in it, for headers and cards.</summary>
        public static RawImage Frame(Transform parent, string name, HappiTheme theme, out Image frame)
        {
            frame = HappiUiKit.Panel(parent, name, theme.PanelInset, false);
            var mask = frame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var image = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            image.transform.SetParent(frame.transform, false);
            HappiUiKit.Stretch(image.rectTransform, 1f, 1f, 1f, 1f);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Shows a picture in a frame made by <see cref="Frame"/>, round or square.</summary>
        public static void Show(Image frame, RawImage image, Texture texture, bool round)
        {
            if (frame == null || image == null)
                return;
            frame.gameObject.SetActive(texture != null);
            if (texture == null)
                return;
            frame.sprite = round ? HappiUiKit.Circle() : HappiUiKit.Rounded();
            frame.type = round ? Image.Type.Simple : Image.Type.Sliced;
            image.texture = texture;
            image.color = Color.white;
        }
    }
}
