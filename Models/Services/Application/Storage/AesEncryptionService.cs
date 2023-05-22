using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace StorageMicroService.Models.Services.Application.Storage
{
    public class AesEncryptionService
    {
        private const int AES256KeySize = 256;
        private const int BufferSize = 1024 * 1024;

        public static byte[] RandomByteArray(int length)
        {

            byte[] result = new byte[length];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(result);
            }
            //using (RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider())
            //{
            //    provider.GetBytes(result);
            //}
            return result;

        }
        private async static Task ExecuteEncryptionDecription(Stream input, Stream output, CryptoStreamMode cryptoStreamMode, byte[] password, byte[] salt)
        {
            using (var key = GenerateKey(password, salt))
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = key.GetBytes(aes.KeySize / 8);
                aes.IV = key.GetBytes(aes.BlockSize / 8);
                aes.Padding = PaddingMode.ISO10126;
                aes.Mode = CipherMode.CBC;

                var pool = ArrayPool<byte>.Shared;
                var buffer = pool.Rent(BufferSize + aes.BlockSize / 8);
                var blockSize = aes.BlockSize / 8;

                int bytesRead;

                using (var csStream = new CryptoStream(output, (cryptoStreamMode == CryptoStreamMode.Write) ? aes.CreateEncryptor() : aes.CreateDecryptor(), CryptoStreamMode.Write, (cryptoStreamMode == CryptoStreamMode.Write) ? false : true))
                {

                    while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        Debug.WriteLine($"Bytes read: {bytesRead}");
                        await csStream.WriteAsync(buffer, 0, bytesRead);
                    }

                    pool.Return(buffer);
                }


            }
        }
        public static async Task EncryptStreamAsync(Stream input, Stream output, byte[] password, byte[] salt)
        {
            await ExecuteEncryptionDecription(input, output, CryptoStreamMode.Write, password, salt);
        }
        public static async Task DecryptStreamAsync(Stream input, Stream output, byte[] password, byte[] salt)
        {
            await ExecuteEncryptionDecription(input, output, CryptoStreamMode.Read, password, salt);
        }
        private static Rfc2898DeriveBytes GenerateKey(byte[] password, byte[] salt)
        {
            return new Rfc2898DeriveBytes(password, salt, 52768);
        }
        private static bool CheckPassword(byte[] password, byte[] salt, byte[] key)
        {
            using (Rfc2898DeriveBytes r = GenerateKey(password, salt))
            {
                byte[] newKey = r.GetBytes(AES256KeySize / 8);
                return newKey.SequenceEqual(key);
            }

        }


    }

}
