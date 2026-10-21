using System.Text;

namespace AICompanion.AI
{
    /// <summary>
    /// Decodes UTF-8 that may split a character across download chunks.
    /// </summary>
    public sealed class Utf8ChunkDecoder
    {
        readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
        char[] _chars = new char[256];

        public string Take(byte[] data, int length)
        {
            if (data == null || length <= 0)
                return string.Empty;
            if (length > data.Length)
                length = data.Length;

            // One byte can become at most one char. GetCharCount would advance the
            // same decoder, so the following GetChars would see the chunk twice.
            if (_chars.Length < length)
                _chars = new char[length];

            var written = _decoder.GetChars(data, 0, length, _chars, 0, false);
            if (written <= 0)
                return string.Empty;
            return new string(_chars, 0, written);
        }
    }
}
