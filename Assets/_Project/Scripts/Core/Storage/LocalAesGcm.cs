using System;
using System.Security.Cryptography;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// AES-256-GCM with a 96-bit nonce and a 16-byte tag (NIST SP 800-38D).
    /// Unity's API compatibility level is .NET Standard 2.0, which has no
    /// <c>AesGcm</c> type. The block cipher is <see cref="Aes"/>, so the same
    /// code runs on Mono and IL2CPP without an extra package.
    /// </summary>
    public static class LocalAesGcm
    {
        public const string Magic = "LEIAENC1";
        public const int KeyBytes = 32;
        public const int NonceBytes = 12;
        public const int TagBytes = 16;
        public const int HeaderBytes = 8 + 1 + NonceBytes + TagBytes;

        public static byte[] Seal(byte[] key, byte[] nonce, byte[] aad, byte[] plaintext)
        {
            if (key == null || key.Length != KeyBytes)
                throw new ArgumentException("A 256-bit key is required.", "key");

            if (nonce == null)
            {
                nonce = new byte[NonceBytes];
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(nonce);
            }

            if (nonce.Length != NonceBytes)
                throw new ArgumentException("A 96-bit nonce is required.", "nonce");

            if (plaintext == null)
                plaintext = new byte[0];
            if (aad == null)
                aad = new byte[0];

            var hashKey = new byte[16];
            EncryptBlock(key, hashKey, 0, hashKey, 0);

            var j0 = new byte[16];
            Buffer.BlockCopy(nonce, 0, j0, 0, NonceBytes);
            j0[15] = 1;

            var counter = (byte[])j0.Clone();
            Increment32(counter);
            var cipher = Gctr(key, counter, plaintext);
            var tag = new byte[TagBytes];
            var digest = Ghash(hashKey, aad, cipher);
            EncryptBlock(key, j0, 0, tag, 0);
            Xor(tag, digest);

            var envelope = new byte[HeaderBytes + cipher.Length];
            var magic = Encoding.ASCII.GetBytes(Magic);
            Buffer.BlockCopy(magic, 0, envelope, 0, 8);
            envelope[8] = 1;
            Buffer.BlockCopy(nonce, 0, envelope, 9, NonceBytes);
            Buffer.BlockCopy(tag, 0, envelope, 21, TagBytes);
            if (cipher.Length > 0)
                Buffer.BlockCopy(cipher, 0, envelope, HeaderBytes, cipher.Length);
            return envelope;
        }

        public static bool TryOpen(byte[] key, byte[] aad, byte[] envelope, out byte[] plaintext)
        {
            plaintext = null;
            if (key == null || key.Length != KeyBytes || envelope == null || envelope.Length < HeaderBytes)
                return false;
            if (!IsMagic(envelope) || envelope[8] != 1)
                return false;

            if (aad == null)
                aad = new byte[0];

            var nonce = new byte[NonceBytes];
            var tag = new byte[TagBytes];
            Buffer.BlockCopy(envelope, 9, nonce, 0, NonceBytes);
            Buffer.BlockCopy(envelope, 21, tag, 0, TagBytes);
            var cipher = new byte[envelope.Length - HeaderBytes];
            if (cipher.Length > 0)
                Buffer.BlockCopy(envelope, HeaderBytes, cipher, 0, cipher.Length);

            var hashKey = new byte[16];
            EncryptBlock(key, hashKey, 0, hashKey, 0);

            var j0 = new byte[16];
            Buffer.BlockCopy(nonce, 0, j0, 0, NonceBytes);
            j0[15] = 1;

            var expected = new byte[TagBytes];
            EncryptBlock(key, j0, 0, expected, 0);
            Xor(expected, Ghash(hashKey, aad, cipher));
            if (!FixedEquals(expected, tag))
                return false;

            var counter = (byte[])j0.Clone();
            Increment32(counter);
            plaintext = Gctr(key, counter, cipher);
            return true;
        }

        public static bool IsMagic(byte[] envelope)
        {
            if (envelope == null || envelope.Length < 8)
                return false;
            var magic = Encoding.ASCII.GetBytes(Magic);
            for (var i = 0; i < 8; i++)
            {
                if (envelope[i] != magic[i])
                    return false;
            }

            return true;
        }

        static byte[] Gctr(byte[] key, byte[] counter, byte[] input)
        {
            if (input == null || input.Length == 0)
                return new byte[0];

            var output = new byte[input.Length];
            var block = (byte[])counter.Clone();
            var stream = new byte[16];
            var offset = 0;
            while (offset < input.Length)
            {
                EncryptBlock(key, block, 0, stream, 0);
                var count = Math.Min(16, input.Length - offset);
                for (var i = 0; i < count; i++)
                    output[offset + i] = (byte)(input[offset + i] ^ stream[i]);
                Increment32(block);
                offset += count;
            }

            return output;
        }

        static byte[] Ghash(byte[] hashKey, byte[] aad, byte[] cipher)
        {
            var y = new byte[16];
            Absorb(y, hashKey, aad);
            Absorb(y, hashKey, cipher);
            var length = new byte[16];
            WriteU64((ulong)(aad == null ? 0 : aad.Length) * 8UL, length, 0);
            WriteU64((ulong)(cipher == null ? 0 : cipher.Length) * 8UL, length, 8);
            Xor(y, length);
            var product = new byte[16];
            Multiply(y, hashKey, product);
            return product;
        }

        static void Absorb(byte[] y, byte[] hashKey, byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            var block = new byte[16];
            var product = new byte[16];
            var offset = 0;
            while (offset < data.Length)
            {
                Array.Clear(block, 0, 16);
                var count = Math.Min(16, data.Length - offset);
                Buffer.BlockCopy(data, offset, block, 0, count);
                Xor(y, block);
                Multiply(y, hashKey, product);
                Buffer.BlockCopy(product, 0, y, 0, 16);
                offset += count;
            }
        }

        /// <summary>GF(2^128) multiply, NIST SP 800-38D algorithm 1. <paramref name="y"/> is the hash subkey.</summary>
        static void Multiply(byte[] x, byte[] y, byte[] z)
        {
            var v = new byte[16];
            Buffer.BlockCopy(x, 0, v, 0, 16);
            Array.Clear(z, 0, 16);
            for (var i = 0; i < 128; i++)
            {
                var bit = (y[i >> 3] >> (7 - (i & 7))) & 1;
                if (bit == 1)
                    Xor(z, v);

                var lsb = v[15] & 1;
                for (var j = 15; j > 0; j--)
                    v[j] = (byte)((v[j] >> 1) | ((v[j - 1] & 1) << 7));
                v[0] = (byte)(v[0] >> 1);
                if (lsb == 1)
                    v[0] ^= 0xE1;
            }
        }

        static void Increment32(byte[] counter)
        {
            for (var i = 15; i >= 12; i--)
            {
                counter[i]++;
                if (counter[i] != 0)
                    return;
            }
        }

        static void EncryptBlock(byte[] key, byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = key;
                using (var encryptor = aes.CreateEncryptor())
                {
                    var block = encryptor.TransformFinalBlock(input, inputOffset, 16);
                    Buffer.BlockCopy(block, 0, output, outputOffset, 16);
                }
            }
        }

        static void WriteU64(ulong value, byte[] destination, int offset)
        {
            for (var i = 7; i >= 0; i--)
            {
                destination[offset + i] = (byte)(value & 0xFF);
                value >>= 8;
            }
        }

        static void Xor(byte[] left, byte[] right)
        {
            for (var i = 0; i < left.Length; i++)
                left[i] ^= right[i];
        }

        static bool FixedEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            var diff = 0;
            for (var i = 0; i < left.Length; i++)
                diff |= left[i] ^ right[i];
            return diff == 0;
        }
    }
}
