using System;

namespace AICompanion.Voice
{
    /// <summary>
    /// Reads a PCM or float WAV into samples for <c>AudioClip.SetData</c>.
    /// Short, garbage, and oversized chunk headers return false and never throw.
    /// A data chunk whose size is 0 or 0xFFFFFFFF (streaming WAV) runs to the end of the buffer.
    /// LIST and other chunks are skipped, including the pad byte on an odd size.
    /// </summary>
    public static class WavPcm
    {
        const int MaxChunks = 64;

        public static bool TryDecode(byte[] data, int fallbackSampleRate, out float[] samples, out int channels, out int sampleRate)
        {
            samples = null;
            channels = 0;
            sampleRate = 0;
            try
            {
                return TryDecodeCore(data, fallbackSampleRate, out samples, out channels, out sampleRate);
            }
            catch (Exception)
            {
                samples = null;
                channels = 0;
                sampleRate = 0;
                return false;
            }
        }

        static bool TryDecodeCore(byte[] data, int fallbackSampleRate, out float[] samples, out int channels, out int sampleRate)
        {
            samples = null;
            channels = 0;
            sampleRate = 0;
            if (data == null || data.Length < 12)
                return false;
            if (!FourCc(data, 0, "RIFF") || !FourCc(data, 8, "WAVE"))
                return false;

            var format = 0;
            var bits = 0;
            var dataOffset = -1;
            var dataLength = 0;
            var offset = 12;
            for (var n = 0; n < MaxChunks; n++)
            {
                if (!InRange(data, offset, 8))
                    break;
                if (!TryReadUInt(data, offset + 4, out var declared))
                    break;

                var start = offset + 8;
                var remaining = data.Length - start;
                var unknown = IsUnknownLength(start, declared);
                var fits = !unknown && declared <= (uint)remaining;
                if (FourCc(data, offset, "fmt "))
                {
                    var read = fits && declared >= 16
                        && TryReadShort(data, start, out format)
                        && TryReadShort(data, start + 2, out channels)
                        && TryReadInt(data, start + 4, out sampleRate)
                        && TryReadShort(data, start + 14, out bits);
                    if (!read && !fits)
                        break;
                }
                else if (FourCc(data, offset, "data"))
                {
                    dataOffset = start;
                    dataLength = fits ? (int)declared : remaining;
                    break;
                }
                else if (!fits)
                {
                    break;
                }

                if (!TryAdvance(start, declared, fits, data.Length, out offset))
                    break;
            }

            if (dataOffset < 0 || channels <= 0 || channels > 2 || sampleRate <= 0)
                return false;
            if (fallbackSampleRate > 0 && sampleRate < 1000)
                return false;
            if (dataOffset > data.Length)
                return false;
            if (dataLength > data.Length - dataOffset)
                dataLength = data.Length - dataOffset;

            var bytesPerFrame = BytesPerFrame(format, bits, channels);
            if (bytesPerFrame <= 0)
                return false;
            dataLength -= dataLength % bytesPerFrame;
            if (dataLength <= 0)
                return false;

            if (format == 1 && bits == 16)
                return TryPcm16(data, dataOffset, dataLength, channels, out samples);
            if (format == 1 && bits == 8)
                return TryPcm8(data, dataOffset, dataLength, channels, out samples);
            if (format == 3 && bits == 32)
                return TryFloat32(data, dataOffset, dataLength, channels, out samples);
            return false;
        }

        static int BytesPerFrame(int format, int bits, int channels)
        {
            if (channels <= 0)
                return 0;
            if (format == 1 && bits == 16)
                return channels * 2;
            if (format == 1 && bits == 8)
                return channels;
            if (format == 3 && bits == 32)
                return channels * 4;
            return 0;
        }

        static bool IsUnknownLength(int start, uint declared)
        {
            if (declared == 0u || declared == 0xFFFFFFFFu)
                return true;
            if (declared > int.MaxValue)
                return true;
            return (long)start + declared > int.MaxValue;
        }

        static bool TryAdvance(int start, uint declared, bool fits, int length, out int nextOffset)
        {
            nextOffset = start;
            if (!fits)
                return false;

            var next = (long)start + declared;
            if ((declared & 1u) != 0)
                next++;
            if (next <= start || next > length || next > int.MaxValue)
                return false;
            nextOffset = (int)next;
            return nextOffset > start;
        }

        static bool TryPcm16(byte[] data, int offset, int length, int channels, out float[] samples)
        {
            samples = null;
            var count = length / 2;
            if (count <= 0 || count % channels != 0 || !InRange(data, offset, count * 2))
                return false;

            samples = new float[count];
            for (var i = 0; i < count; i++)
            {
                if (!TryReadShort(data, offset + i * 2, out var value))
                    return false;
                samples[i] = (short)value / 32768f;
            }

            return true;
        }

        static bool TryPcm8(byte[] data, int offset, int length, int channels, out float[] samples)
        {
            samples = null;
            if (length <= 0 || length % channels != 0 || !InRange(data, offset, length))
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
            if (count <= 0 || count % channels != 0 || !InRange(data, offset, count * 4))
                return false;

            samples = new float[count];
            for (var i = 0; i < count; i++)
            {
                if (!TryReadInt(data, offset + i * 4, out var bits))
                    return false;
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
            return BitConverter.ToSingle(bytes, 0);
        }

        static bool FourCc(byte[] data, int offset, string token)
        {
            if (data == null || token == null || token.Length != 4 || !InRange(data, offset, 4))
                return false;
            for (var i = 0; i < 4; i++)
            {
                if (data[offset + i] != (byte)token[i])
                    return false;
            }

            return true;
        }

        static bool InRange(byte[] data, int offset, int count)
        {
            if (data == null || offset < 0 || count < 0)
                return false;
            return (long)offset + count <= data.Length;
        }

        static bool TryReadShort(byte[] data, int offset, out int value)
        {
            value = 0;
            if (!InRange(data, offset, 2))
                return false;
            value = data[offset] | (data[offset + 1] << 8);
            return true;
        }

        static bool TryReadInt(byte[] data, int offset, out int value)
        {
            value = 0;
            if (!TryReadUInt(data, offset, out var raw))
                return false;
            value = unchecked((int)raw);
            return true;
        }

        static bool TryReadUInt(byte[] data, int offset, out uint value)
        {
            value = 0;
            if (!InRange(data, offset, 4))
                return false;
            value = (uint)data[offset]
                | ((uint)data[offset + 1] << 8)
                | ((uint)data[offset + 2] << 16)
                | ((uint)data[offset + 3] << 24);
            return true;
        }
    }
}
