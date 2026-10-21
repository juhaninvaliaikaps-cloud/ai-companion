namespace AICompanion.Voice
{
    /// <summary>
    /// Reads a PCM or float WAV into samples for <c>AudioClip.SetData</c>.
    /// A body that is not a WAV is left unplayed.
    /// </summary>
    public static class WavPcm
    {
        public static bool TryDecode(byte[] data, int fallbackSampleRate, out float[] samples, out int channels, out int sampleRate)
        {
            samples = null;
            channels = 0;
            sampleRate = 0;
            if (data == null || data.Length < 12)
                return false;
            if (!FourCc(data, 0, "RIFF"))
                return false;
            if (!FourCc(data, 8, "WAVE"))
                return false;

            var format = 0;
            var bits = 0;
            var dataOffset = -1;
            var dataLength = 0;
            var offset = 12;
            while (offset + 8 <= data.Length)
            {
                var size = ReadInt(data, offset + 4);
                if (size < 0 || offset + 8 + size > data.Length)
                    break;

                var start = offset + 8;
                if (FourCc(data, offset, "fmt ") && size >= 16)
                {
                    format = ReadShort(data, start);
                    channels = ReadShort(data, start + 2);
                    sampleRate = ReadInt(data, start + 4);
                    bits = ReadShort(data, start + 14);
                }
                else if (FourCc(data, offset, "data"))
                {
                    dataOffset = start;
                    dataLength = size;
                }

                offset = start + size + (size & 1);
            }

            if (dataOffset < 0 || channels <= 0 || channels > 2 || sampleRate <= 0)
                return false;
            if (fallbackSampleRate > 0 && sampleRate < 1000)
                return false;

            if (format == 1 && bits == 16)
                return TryPcm16(data, dataOffset, dataLength, channels, out samples);
            if (format == 1 && bits == 8)
                return TryPcm8(data, dataOffset, dataLength, channels, out samples);
            if (format == 3 && bits == 32)
                return TryFloat32(data, dataOffset, dataLength, channels, out samples);
            return false;
        }

        static bool TryPcm16(byte[] data, int offset, int length, int channels, out float[] samples)
        {
            samples = null;
            var count = length / 2;
            if (count <= 0 || count % channels != 0 || offset + count * 2 > data.Length)
                return false;

            samples = new float[count];
            for (var i = 0; i < count; i++)
            {
                var lo = data[offset + i * 2];
                var hi = (sbyte)data[offset + i * 2 + 1];
                var value = (short)(lo | (hi << 8));
                samples[i] = value / 32768f;
            }

            return true;
        }

        static bool TryPcm8(byte[] data, int offset, int length, int channels, out float[] samples)
        {
            samples = null;
            if (length <= 0 || length % channels != 0 || offset + length > data.Length)
                return false;

            samples = new float[length];
            for (var i = 0; i < length; i++)
                samples[i] = (data[offset + i] - 128) / 128f;
            return true;
        }

        static bool TryFloat32(byte[] data, int offset, int length, int channels, out float[] samples)
        {
            samples = null;
            var count = length / 4;
            if (count <= 0 || count % channels != 0 || offset + count * 4 > data.Length)
                return false;

            samples = new float[count];
            for (var i = 0; i < count; i++)
            {
                var bits = ReadInt(data, offset + i * 4);
                samples[i] = IntBitsToFloat(bits);
            }

            return true;
        }

        static float IntBitsToFloat(int bits)
        {
            var bytes = new byte[4];
            bytes[0] = (byte)bits;
            bytes[1] = (byte)(bits >> 8);
            bytes[2] = (byte)(bits >> 16);
            bytes[3] = (byte)(bits >> 24);
            return System.BitConverter.ToSingle(bytes, 0);
        }

        static bool FourCc(byte[] data, int offset, string token)
        {
            if (offset < 0 || offset + 4 > data.Length)
                return false;
            for (var i = 0; i < 4; i++)
            {
                if (data[offset + i] != (byte)token[i])
                    return false;
            }

            return true;
        }

        static int ReadShort(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }

        static int ReadInt(byte[] data, int offset)
        {
            return data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24);
        }
    }
}
