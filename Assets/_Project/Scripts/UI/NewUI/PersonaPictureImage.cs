using System;
using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>JUH-150: the centre square of a picture, for the crop.</summary>
    public static class PictureCrop
    {
        public const int Size = 512;

        public static void Square(int width, int height, out int x, out int y, out int side)
        {
            side = Math.Max(0, Math.Min(width, height));
            x = Math.Max(0, (width - side) / 2);
            y = Math.Max(0, (height - side) / 2);
        }
    }

    /// <summary>
    /// JUH-150: turns a picked picture into her persona picture: the centre square, scaled to
    /// 512 × 512, as PNG. The round or square shape is only how it is shown.
    /// </summary>
    public static class PersonaPictureImage
    {
        public const long MaxFileBytes = 20L * 1024 * 1024;

        public static bool TryPrepare(byte[] fileBytes, out byte[] png, out string error)
        {
            png = null;
            error = string.Empty;
            if (fileBytes == null || fileBytes.Length == 0)
            {
                error = "That file is empty.";
                return false;
            }

            if (fileBytes.LongLength > MaxFileBytes)
            {
                error = "That picture is larger than 20 MB.";
                return false;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            RenderTexture target = null;
            Texture2D result = null;
            var previous = RenderTexture.active;
            try
            {
                if (!ImageConversion.LoadImage(source, fileBytes, false))
                {
                    error = "That file isn't a PNG or JPEG picture.";
                    return false;
                }

                int x;
                int y;
                int side;
                PictureCrop.Square(source.width, source.height, out x, out y, out side);
                if (side <= 0)
                {
                    error = "That picture has no size.";
                    return false;
                }

                target = RenderTexture.GetTemporary(PictureCrop.Size, PictureCrop.Size, 0, RenderTextureFormat.ARGB32);
                var scale = new Vector2((float)side / source.width, (float)side / source.height);
                var offset = new Vector2((float)x / source.width, (float)y / source.height);
                Graphics.Blit(source, target, scale, offset);
                RenderTexture.active = target;
                result = new Texture2D(PictureCrop.Size, PictureCrop.Size, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0, 0, PictureCrop.Size, PictureCrop.Size), 0, 0);
                result.Apply(false, false);
                png = ImageConversion.EncodeToPNG(result);
                return png != null && png.Length > 0;
            }
            catch (Exception ex)
            {
                error = "Couldn't read that picture (" + ex.GetType().Name + ").";
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null)
                    RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(source);
                if (result != null)
                    UnityEngine.Object.Destroy(result);
            }
        }

        /// <summary>A texture for showing a stored picture, or null.</summary>
        public static Texture2D Load(byte[] png)
        {
            if (png == null || png.Length == 0)
                return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (ImageConversion.LoadImage(texture, png, false))
                return texture;
            UnityEngine.Object.Destroy(texture);
            return null;
        }
    }
}
