using System;
using System.Runtime.InteropServices;
using OpenSSL.Core;
using bitmessage.network;

namespace bitmessage.Crypto
{
	public static class ECDSA
	{
		const int Secp256K1 = 714;

		public static byte[] ECDSASign(this byte[] data, byte[] privKey)
		{
			byte[] pubkey = PointMult(privKey);

			byte[] pubkey_x = new byte[32];
			byte[] pubkey_y = new byte[32];

			Buffer.BlockCopy(pubkey, 1, pubkey_x, 0, 32);
			Buffer.BlockCopy(pubkey, 33, pubkey_y, 0, 32);

			IntPtr key = IntPtr.Zero;
			IntPtr priv_key = IntPtr.Zero;
			IntPtr pub_key_x = IntPtr.Zero;
			IntPtr pub_key_y = IntPtr.Zero;
			IntPtr pub_key = IntPtr.Zero;
			IntPtr md_ctx = IntPtr.Zero;
			//try:
			try
			{
				//	size = len(inputb)
				int size = data.Length;
				//	buff = OpenSSL.malloc(inputb, size)
				byte[] buff = data;
				//	digest = OpenSSL.malloc(0, 64)
				byte[] digest = new byte[64];
				//	md_ctx = OpenSSL.EVP_MD_CTX_create()
				md_ctx = Native.EVP_MD_CTX_create();
				//	dgst_len = OpenSSL.pointer(OpenSSL.c_int(0))
				uint dgst_len = 0;
				//	siglen = OpenSSL.pointer(OpenSSL.c_int(0))
				uint siglen = 0;
				//	sig = OpenSSL.malloc(0, 151)
				var sig = new byte[151];

				//	key = OpenSSL.EC_KEY_new_by_curve_name(self.curve)
				//	if key == 0:
				//		raise Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...")
				key = Native.EC_KEY_new_by_curve_name(Secp256K1);
				if (key == IntPtr.Zero)
					throw new Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...");

				//	priv_key = OpenSSL.BN_bin2bn(self.privkey, len(self.privkey), 0)
				//	pub_key_x = OpenSSL.BN_bin2bn(self.pubkey_x, len(self.pubkey_x), 0)
				//	pub_key_y = OpenSSL.BN_bin2bn(self.pubkey_y, len(self.pubkey_y), 0)

				priv_key = Native.BN_bin2bn(privKey, privKey.Length, IntPtr.Zero);
				pub_key_x = Native.BN_bin2bn(pubkey_x, pubkey_x.Length, IntPtr.Zero);
				pub_key_y = Native.BN_bin2bn(pubkey_y, pubkey_y.Length, IntPtr.Zero);

				//	if (OpenSSL.EC_KEY_set_private_key(key, priv_key)) == 0:
				//		raise Exception("[OpenSSL] EC_KEY_set_private_key FAIL ...")

				if (Native.EC_KEY_set_private_key(key, priv_key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_set_private_key FAIL ...");

				//	group = OpenSSL.EC_KEY_get0_group(key)
				IntPtr group = Native.EC_KEY_get0_group(key);

				//	pub_key = OpenSSL.EC_POINT_new(group)
				pub_key = Native.EC_POINT_new(group);

				//	if (OpenSSL.EC_POINT_set_affine_coordinates_GFp(group, pub_key,
				//													pub_key_x,
				//													pub_key_y,
				//													0)) == 0:
				//		raise Exception(
				//			"[OpenSSL] EC_POINT_set_affine_coordinates_GFp FAIL ...")
				if (Native.EC_POINT_set_affine_coordinates_GFp(group, pub_key, pub_key_x, pub_key_y, IntPtr.Zero) == 0)
					throw new Exception("[OpenSSL] EC_POINT_set_affine_coordinates_GFp FAIL ...");

				//	if (OpenSSL.EC_KEY_set_public_key(key, pub_key)) == 0:
				//		raise Exception("[OpenSSL] EC_KEY_set_public_key FAIL ...")
				if (Native.EC_KEY_set_public_key(key, pub_key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_set_public_key FAIL ...");

				//	if (OpenSSL.EC_KEY_check_key(key)) == 0:
				//		raise Exception("[OpenSSL] EC_KEY_check_key FAIL ...")
				if (Native.EC_KEY_check_key(key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_check_key FAIL ...");

				//	OpenSSL.EVP_MD_CTX_init(md_ctx)
				Native.EVP_MD_CTX_init(md_ctx);

				//	OpenSSL.EVP_DigestInit(md_ctx, OpenSSL.EVP_ecdsa())
				Native.EVP_DigestInit_ex(md_ctx, Native.EVP_ecdsa(), IntPtr.Zero);

				//	if (OpenSSL.EVP_DigestUpdate(md_ctx, buff, size)) == 0:
				//		raise Exception("[OpenSSL] EVP_DigestUpdate FAIL ...")
				if (Native.EVP_DigestUpdate(md_ctx, buff, (uint) data.Length) == 0)
					throw new Exception("[OpenSSL] EVP_DigestUpdate FAIL ...");

				//	OpenSSL.EVP_DigestFinal(md_ctx, digest, dgst_len)
				Native.EVP_DigestFinal_ex(md_ctx, digest, ref dgst_len);

				//	OpenSSL.ECDSA_sign(0, digest, dgst_len.contents, sig, siglen, key)
				Native.ECDSA_sign(0, digest, (int) dgst_len, sig, ref siglen, key);

				//	if (OpenSSL.ECDSA_verify(0, digest, dgst_len.contents, sig,
				//							 siglen.contents, key)) != 1:
				//		raise Exception("[OpenSSL] ECDSA_verify FAIL ...")
				if (Native.ECDSA_verify(0, digest, (int) dgst_len, sig, (int) siglen, key) != 1)
					throw new Exception("[OpenSSL] ECDSA_verify FAIL ...");

				if (siglen == sig.Length)
					return sig;
				else
				{
					byte[] result = new byte[siglen];
					Buffer.BlockCopy(sig, 0, result, 0, (int) siglen);
					return result;
				}
			}
			//finally:
			//	OpenSSL.EC_KEY_free(key)
			//	OpenSSL.BN_free(pub_key_x)
			//	OpenSSL.BN_free(pub_key_y)
			//	OpenSSL.BN_free(priv_key)
			//	OpenSSL.EC_POINT_free(pub_key)
			//	OpenSSL.EVP_MD_CTX_destroy(md_ctx)
			finally
			{
				Native.EC_KEY_free(key);
				Native.BN_free(pub_key_x);
				Native.BN_free(pub_key_y);
				Native.BN_free(priv_key);
				Native.EC_POINT_free(pub_key);
				Native.EVP_MD_CTX_destroy(md_ctx);
			}
		}

		public static bool ECDSAVerify(this byte[] data, byte[] publicSigningKey, byte[] signature)
		{
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
				key = Native.EC_KEY_new_by_curve_name(Secp256K1);
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

		public static byte[] PointMult(byte[] secret)
		{
			var k = Native.EC_KEY_new_by_curve_name(Secp256K1);
			var privKey = Native.BN_bin2bn(secret, 32, IntPtr.Zero);
			var group = Native.EC_KEY_get0_group(k);
			var pubKey = Native.EC_POINT_new(group);

			Native.EC_POINT_mul(group, pubKey, privKey, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
			Native.EC_KEY_set_private_key(k, privKey);
			Native.EC_KEY_set_public_key(k, pubKey);

			//Pass *out as null for required buffer length.
			int reqLen = Native.i2o_ECPublicKey(k, 0);

			Byte[] outBuf = new Byte[reqLen];
			IntPtr unmanagedOut = Marshal.AllocCoTaskMem(outBuf.Length);
			int res = Native.i2o_ECPublicKey(k, ref unmanagedOut);
			if (res == reqLen)
			{
				unmanagedOut -= res;
				Marshal.Copy(unmanagedOut, outBuf, 0, outBuf.Length);
			}
			Marshal.FreeCoTaskMem(unmanagedOut);

			Native.EC_POINT_free(pubKey);
			Native.BN_free(privKey);
			Native.EC_KEY_free(k);
			return outBuf;
		}

	}
}
