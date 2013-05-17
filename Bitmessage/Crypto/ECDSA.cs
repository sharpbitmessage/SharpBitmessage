using System;
using System.Diagnostics.Contracts;
using OpenSSL.Core;

namespace bitmessage.Crypto
{
	public static class ECDSA
	{
		public static bool ECDSAVerify(this byte[] data, byte[] publicSigningKey, byte[] signature)
		{
			Contract.Requires(publicSigningKey.Length == 65);
			
			byte[] pubkeyX = new byte[32];
			byte[] pubkeyY = new byte[32];

			Buffer.BlockCopy(publicSigningKey, 1, pubkeyX, 0, 32);
			Buffer.BlockCopy(publicSigningKey, 33, pubkeyY, 0, 32);

			IntPtr key = IntPtr.Zero;
			IntPtr bnPubkeyX = IntPtr.Zero;
			IntPtr bnPubkeyY = IntPtr.Zero;
			IntPtr pubKey = IntPtr.Zero;
			IntPtr mdCtx = IntPtr.Zero;
			try
			{
				key = Native.EC_KEY_new_by_curve_name(714);
				if (key == IntPtr.Zero)
					throw new Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...");

				bnPubkeyX = Native.BN_bin2bn(pubkeyX, pubkeyX.Length, IntPtr.Zero);
				bnPubkeyY = Native.BN_bin2bn(pubkeyY, pubkeyY.Length, IntPtr.Zero);

				IntPtr group = Native.EC_KEY_get0_group(key);
				pubKey = Native.EC_POINT_new(group);

				if (Native.EC_POINT_set_affine_coordinates_GFp(group, pubKey, bnPubkeyX, bnPubkeyY, IntPtr.Zero) == 0)
					throw new Exception("[OpenSSL] EC_POINT_set_affine_coordinates_GFp FAIL ...");

				if (Native.EC_KEY_set_public_key(key, pubKey) == 0)
					throw new Exception("[OpenSSL] EC_KEY_set_public_key FAIL ...");

				if (Native.EC_KEY_check_key(key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_check_key FAIL ...");

				mdCtx = Native.EVP_MD_CTX_create();
				byte[] digest = new byte[64];
				uint dgstLen = 0;

				Native.EVP_MD_CTX_init(mdCtx);
				Native.EVP_DigestInit_ex(mdCtx, Native.EVP_ecdsa(), IntPtr.Zero);
				if (Native.EVP_DigestUpdate(mdCtx, data, (uint)data.Length) == 0)
					throw new Exception("[OpenSSL] EVP_DigestUpdate FAIL ...");
				Native.EVP_DigestFinal_ex(mdCtx, digest, ref dgstLen);

				var res = Native.ECDSA_verify(0, digest, (int) dgstLen, signature, signature.Length, key);
				return 1 == res;
			}
			finally
			{
				Native.EC_KEY_free(key);
				Native.BN_free(bnPubkeyX);
				Native.BN_free(bnPubkeyY);
				Native.EC_POINT_free(pubKey);
				Native.EVP_MD_CTX_destroy(mdCtx);
			}
		}
	}
}
