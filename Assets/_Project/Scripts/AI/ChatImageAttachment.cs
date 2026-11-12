using System;

namespace AICompanion.AI
{
    /// <summary>
    /// JPEG bytes for a single vision request. Call <see cref="Wipe"/> as soon as the
    /// HTTP body has been built. Do not write these bytes to a log, a transcript, or a file.
    /// </summary>
    public sealed class ChatImageAttachment
    {
        public byte[] JpegBytes;
        public int Width;
        public int Height;
        public int Generation;

        public void Wipe()
        {
            if (JpegBytes != null && JpegBytes.Length > 0)
                Array.Clear(JpegBytes, 0, JpegBytes.Length);
            JpegBytes = null;
            Width = 0;
            Height = 0;
        }
    }
}
