using System;
using System.Security.Cryptography;
using bitmessage.network;
using OpenSSL.Core;

namespace bitmessage.Crypto
{
	public class Cipher
	{
		private IntPtr ctx;
		private const int blocksize = 16;
		public Cipher(byte[] key, byte[] iv, bool doEncrypt, string ciphername="aes-256-cbc")
		{
			if (ciphername!="aes-256-cbc")
				throw new NotSupportedException(ciphername); // see blocksize

			ctx = Native.EVP_CIPHER_CTX_new();
			Native.EVP_CipherInit_ex(ctx, Native.EVP_aes_256_cbc(), IntPtr.Zero, key, iv, doEncrypt ? 1 : 0);
		}

		public byte[] Ciphering(byte[] input)
		{
			int i1;
			byte[] buffer1 = new byte[input.Length + blocksize];
			if (Native.EVP_CipherUpdate(ctx, buffer1, out i1, input, input.Length) == 0)
				throw new Exception("[OpenSSL] EVP_CipherUpdate FAIL ...");

			int i2 = 0;
			byte[] buffer2 = new byte[blocksize];
			if (Native.EVP_CipherFinal_ex(ctx, buffer2, ref i2) == 0)
				throw new Exception("[OpenSSL] EVP_CipherFinal_ex FAIL ...");

			byte[] result = new byte[i1 + i2];
			Buffer.BlockCopy(buffer1, 0, result, 0, i1);
			Buffer.BlockCopy(buffer2, 0, result, i1, i2);

			return result;
		}
	}
}
