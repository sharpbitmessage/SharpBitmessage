using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenSSL.Core;
using bitmessage.network;

namespace bitmessage.Crypto
{
	public class ECC
	{
		public const UInt16 Secp256K1 = 714;
		public const UInt16 Sect283r1 = 730;

		private readonly UInt16 _curve;
		private byte[] _privkey;
		private byte[] _pubkey_x;
		private byte[] _pubkey_y;

		public ECC(byte[] pubkey=null, byte[] privkey=null, byte[] pubkey_x=null, byte[] pubkey_y=null, byte[] raw_privkey=null, UInt16 curve=Sect283r1)
		{
			_curve = curve;

			if ((pubkey_x != null) && (pubkey_y != null))  
				_set_keys(pubkey_x, pubkey_y, raw_privkey);
			else if (pubkey != null)
			{
				int pos = 0;
				_decode_pubkey(pubkey, out curve, out pubkey_x, out pubkey_y, ref pos);
				if (privkey != null)
				{
					UInt16 curve2;
					pos = 0;
					_decode_privkey(privkey, out curve2, out raw_privkey, ref pos);
					if (curve != curve2)
						throw new Exception("Bad ECC keys ...");
				}
				_curve = curve;
				_set_keys(pubkey_x, pubkey_y, raw_privkey);
			}
			else if (raw_privkey != null)
			{
				_privkey = raw_privkey;
			}
			else
				generate();
		}

		void generate()
		{
			IntPtr pub_key_x = IntPtr.Zero;
			IntPtr pub_key_y = IntPtr.Zero;
			IntPtr key = IntPtr.Zero;
			try
			{
				pub_key_x = Native.BN_new();
				pub_key_y = Native.BN_new();

				key = Native.EC_KEY_new_by_curve_name(_curve);
				if (key == IntPtr.Zero)
					throw new Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...");
				if (Native.EC_KEY_generate_key(key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_generate_key FAIL ...");
				if (Native.EC_KEY_check_key(key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_check_key FAIL ...");
				var priv_key = Native.EC_KEY_get0_private_key(key);

				var group = Native.EC_KEY_get0_group(key);
				var pub_key = Native.EC_KEY_get0_public_key(key);

				if (Native.EC_POINT_get_affine_coordinates_GFp(group, pub_key, pub_key_x, pub_key_y, IntPtr.Zero) == 0)
					throw new Exception("[OpenSSL] EC_POINT_get_affine_coordinates_GFp FAIL ...");

				_privkey = new byte[(Native.BN_num_bits(priv_key) + 7)/8];
				_pubkey_x = new byte[(Native.BN_num_bits(pub_key_x) + 7) / 8];
				_pubkey_y = new byte[(Native.BN_num_bits(pub_key_y) + 7) / 8];

				Native.BN_bn2bin(priv_key, _privkey);
				Native.BN_bn2bin(pub_key_x, _pubkey_x);
				Native.BN_bn2bin(pub_key_y, _pubkey_y);

				raw_check_key(_privkey, _pubkey_x, _pubkey_y);
			}
			finally
			{
				Native.EC_KEY_free(key);
				Native.BN_free(pub_key_x);
				Native.BN_free(pub_key_y);
			}
		}

		int raw_check_key(byte[] privkey, byte[] pubkey_x, byte[] pubkey_y, int curve_par=0)
		{
			int curve = curve_par==0 ? _curve : curve_par;
			
			IntPtr priv_key = IntPtr.Zero;
			IntPtr key = IntPtr.Zero;
			IntPtr pub_key_x = IntPtr.Zero;
			IntPtr pub_key_y = IntPtr.Zero;
			IntPtr pub_key = IntPtr.Zero;
			try
			{
				key = Native.EC_KEY_new_by_curve_name(curve);
				if (key == IntPtr.Zero)
					throw new Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...");
				if (privkey != null)
					priv_key = Native.BN_bin2bn(privkey, privkey.Length, IntPtr.Zero);
				pub_key_x = Native.BN_bin2bn(pubkey_x, pubkey_x.Length, IntPtr.Zero);
				pub_key_y = Native.BN_bin2bn(pubkey_y, pubkey_y.Length, IntPtr.Zero);

				if (privkey != null)
					if (Native.EC_KEY_set_private_key(key, priv_key) == 0)
						throw new Exception("[OpenSSL] EC_KEY_set_private_key FAIL ...");

				var group = Native.EC_KEY_get0_group(key);
				pub_key = Native.EC_POINT_new(group);

				if (Native.EC_POINT_set_affine_coordinates_GFp(group, pub_key, pub_key_x, pub_key_y, IntPtr.Zero) == 0)
					throw new Exception("[OpenSSL] EC_POINT_set_affine_coordinates_GFp FAIL ...");
				if (Native.EC_KEY_set_public_key(key, pub_key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_set_public_key FAIL ...");
				if (Native.EC_KEY_check_key(key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_check_key FAIL ...");
				return 0;
			}
			finally
			{
				Native.EC_KEY_free(key);
				Native.BN_free(pub_key_x);
				Native.BN_free(pub_key_y);
				Native.EC_POINT_free(pub_key);
				if (privkey!=null)
					Native.BN_free(priv_key);
			}
		}

		//	def raw_get_ecdh_key(self, pubkey_x, pubkey_y):

		public static void _decode_pubkey(byte[] pubkey, out UInt16 curve, out byte[] pubkeyX, out byte[] pubkeyY, ref int pos)
		{
			//i = 0			
			//curve = unpack('!H', pubkey[i:i + 2])[0]
			//i += 2
			curve = pubkey.ReadUInt16(ref pos);
			//tmplen = unpack('!H', pubkey[i:i + 2])[0]
			//i += 2
			UInt32 tmplen = pubkey.ReadUInt16(ref pos);
			//pubkey_x = pubkey[i:i + tmplen]
			//i += tmplen
			pubkeyX = pubkey.ReadBytes(ref pos, (int)tmplen);
			//tmplen = unpack('!H', pubkey[i:i + 2])[0]
			//i += 2
			tmplen = pubkey.ReadUInt16(ref pos);
			//pubkey_y = pubkey[i:i + tmplen]
			//i += tmplen
			pubkeyY = pubkey.ReadBytes(ref pos, (int)tmplen);
			//return curve, pubkey_x, pubkey_y, i
		}

		public static void _decode_privkey(byte[] privkey, out UInt16 curve, out byte[] privkeyOut, ref int pos)
		{
			curve = privkey.ReadUInt16(ref pos);
			UInt32 tmplen = privkey.ReadUInt16(ref pos);
			privkeyOut = privkey.ReadBytes(ref pos, (int)tmplen);
		}

		void _set_keys(byte[] pubkey_x, byte[] pubkey_y, byte[] privkey)
		{
			if (raw_check_key(privkey, pubkey_x, pubkey_y) < 0)
			{
				_pubkey_x = null;
				_pubkey_y = null;
				_privkey = null;
				throw new Exception("Bad ECC keys ...");
			}
			else
			{
				_pubkey_x = pubkey_x;
				_pubkey_y = pubkey_y;
				_privkey = privkey;
			}
		}

		public byte[] raw_get_ecdh_key(byte[] pubkey_x, byte[] pubkey_y)
		{
			IntPtr other_key = IntPtr.Zero;
			IntPtr other_pub_key_x = IntPtr.Zero;
			IntPtr other_pub_key_y = IntPtr.Zero;
			IntPtr other_pub_key = IntPtr.Zero;
			IntPtr own_key = IntPtr.Zero;
			IntPtr own_priv_key = IntPtr.Zero;

			//try:
			try
			{
				//	ecdh_keybuffer = OpenSSL.malloc(0, 32)
				var ecdh_keybuffer = new byte[32];

				//	other_key = OpenSSL.EC_KEY_new_by_curve_name(self.curve)
				//	if other_key == 0:
				//		raise Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...")
				other_key = Native.EC_KEY_new_by_curve_name(_curve);
				if (other_key == IntPtr.Zero)
					throw new Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...");

				//	other_pub_key_x = OpenSSL.BN_bin2bn(pubkey_x, len(pubkey_x), 0)
				//	other_pub_key_y = OpenSSL.BN_bin2bn(pubkey_y, len(pubkey_y), 0)
				other_pub_key_x = Native.BN_bin2bn(pubkey_x, pubkey_x.Length, IntPtr.Zero);
				other_pub_key_y = Native.BN_bin2bn(pubkey_y, pubkey_y.Length, IntPtr.Zero);

				//	other_group = OpenSSL.EC_KEY_get0_group(other_key)
				//	other_pub_key = OpenSSL.EC_POINT_new(other_group)
				IntPtr other_group = Native.EC_KEY_get0_group(other_key);
				other_pub_key = Native.EC_POINT_new(other_group);

				//	if (OpenSSL.EC_POINT_set_affine_coordinates_GFp(other_group,
				//													other_pub_key,
				//													other_pub_key_x,
				//													other_pub_key_y,
				//													0)) == 0:
				//		raise Exception(
				//			"[OpenSSL] EC_POINT_set_affine_coordinates_GFp FAIL ...")
				if (Native.EC_POINT_set_affine_coordinates_GFp(other_group,
				                                               other_pub_key,
				                                               other_pub_key_x,
				                                               other_pub_key_y,
				                                               IntPtr.Zero) == 0)
					throw new Exception(
						"[OpenSSL] EC_POINT_set_affine_coordinates_GFp FAIL ...");

				//	if (OpenSSL.EC_KEY_set_public_key(other_key, other_pub_key)) == 0:
				//		raise Exception("[OpenSSL] EC_KEY_set_public_key FAIL ...")
				//	if (OpenSSL.EC_KEY_check_key(other_key)) == 0:
				//		raise Exception("[OpenSSL] EC_KEY_check_key FAIL ...")

				if (Native.EC_KEY_set_public_key(other_key, other_pub_key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_set_public_key FAIL ...");
				if (Native.EC_KEY_check_key(other_key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_check_key FAIL ...");



				//	own_key = OpenSSL.EC_KEY_new_by_curve_name(self.curve)
				//	if own_key == 0:
				//		raise Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...")
				//	own_priv_key = OpenSSL.BN_bin2bn(
				//		self.privkey, len(self.privkey), 0)

				own_key = Native.EC_KEY_new_by_curve_name(_curve);
				if (own_key == IntPtr.Zero)
					throw new Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...");

				// from reloadBroadcastSendersForWhichImWatching:            privEncryptionKey = hashlib.sha512(encodeVarint(addressVersionNumber)+encodeVarint(streamNumber)+hash).digest()[:32]

				//pubkey = a.changebase(a.privtopub(privkey),16,256,minlen=65)[1:]
				//pubkey_bin = '\x02\xca\x00 '+pubkey[:32]+'\x00 '+pubkey[32:]
				//cryptor = pyelliptic.ECC(curve='secp256k1',privkey=privkey_bin,pubkey=pubkey_bin)
				// from makeCryptor:   cryptor = pyelliptic.ECC(curve='secp256k1',privkey=privkey_bin,pubkey=pubkey_bin)
				own_priv_key = Native.BN_bin2bn(_privkey, _privkey.Length, IntPtr.Zero);

				//	if (OpenSSL.EC_KEY_set_private_key(own_key, own_priv_key)) == 0:
				//		raise Exception("[OpenSSL] EC_KEY_set_private_key FAIL ...")
				if (Native.EC_KEY_set_private_key(own_key, own_priv_key) == 0)
					throw new Exception("[OpenSSL] EC_KEY_set_private_key FAIL ...");

				//	OpenSSL.ECDH_set_method(own_key, OpenSSL.ECDH_OpenSSL())
				//	ecdh_keylen = OpenSSL.ECDH_compute_key(
				//		ecdh_keybuffer, 32, other_pub_key, own_key, 0)

				Native.ECDH_set_method(own_key, Native.ECDH_OpenSSL());
				var ecdh_keylen = Native.ECDH_compute_key(ecdh_keybuffer, 32, other_pub_key, own_key, null);

				if (ecdh_keylen != 32)
					throw new Exception("[OpenSSL] ECDH keylen FAIL ...");

				return ecdh_keybuffer;
			}
			finally
			{
				//finally:
				//	OpenSSL.EC_KEY_free(other_key)
				//	OpenSSL.BN_free(other_pub_key_x)
				//	OpenSSL.BN_free(other_pub_key_y)
				//	OpenSSL.EC_POINT_free(other_pub_key)
				//	OpenSSL.EC_KEY_free(own_key)
				//	OpenSSL.BN_free(own_priv_key)
				Native.EC_KEY_free(other_key);
				Native.BN_free(other_pub_key_x);
				Native.BN_free(other_pub_key_y);
				Native.EC_POINT_free(other_pub_key);
				Native.EC_KEY_free(own_key);
				Native.BN_free(own_priv_key);
			}
		}

		public byte[] get_pubkey()
		{
			//curve(2) + len_of_pubkeyX(2) + pubkeyX + len_of_pubkeyY + pubkeyY
			MemoryStream result = new MemoryStream(70);

			byte[] tmp = BitConverter.GetBytes(_curve);
			tmp.ReverseIfNeed();
			if (tmp.Length!=2) throw new Exception("Length of curve != 2");
			result.Write(tmp, 0, tmp.Length);

			tmp = BitConverter.GetBytes(_pubkey_x.Length);
			tmp.ReverseIfNeed(); // 
			result.Write(tmp, 0, 2);  // TODO проверить, что tmp != x00x00

			result.Write(_pubkey_x, 0, _pubkey_x.Length);

			tmp = BitConverter.GetBytes(_pubkey_y.Length);
			tmp.ReverseIfNeed(); // 
			result.Write(tmp, 0, 2);  // TODO проверить, что tmp != x00x00

			result.Write(_pubkey_y, 0, _pubkey_y.Length);

			return result.ToArray();
		}

		public byte[] get_privkey()
		{
			//curve(2) + len_of_privkey(2) + privkey
			MemoryStream result = new MemoryStream(70);

			byte[] tmp = BitConverter.GetBytes(_curve);
			tmp.ReverseIfNeed();
			if (tmp.Length != 2) throw new Exception("Length of curve != 2");
			result.Write(tmp, 0, tmp.Length);

			tmp = BitConverter.GetBytes(_privkey.Length);
			tmp.ReverseIfNeed(); // 
			result.Write(tmp, 0, 2); // TODO проверить, что tmp != x00x00

			result.Write(_privkey, 0, _privkey.Length);

			return result.ToArray();
		}
	}
		
	public static class ECDSA
	{
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
				key = Native.EC_KEY_new_by_curve_name(ECC.Secp256K1);
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
				pub_key = Native.EC_POINT_new(@group);

				//	if (OpenSSL.EC_POINT_set_affine_coordinates_GFp(group, pub_key,
				//													pub_key_x,
				//													pub_key_y,
				//													0)) == 0:
				//		raise Exception(
				//			"[OpenSSL] EC_POINT_set_affine_coordinates_GFp FAIL ...")
				if (Native.EC_POINT_set_affine_coordinates_GFp(@group, pub_key, pub_key_x, pub_key_y, IntPtr.Zero) == 0)
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
				key = Native.EC_KEY_new_by_curve_name(ECC.Secp256K1);
				if (key == IntPtr.Zero)
					throw new Exception("[OpenSSL] EC_KEY_new_by_curve_name FAIL ...");

				bnPubkeyX = Native.BN_bin2bn(pubkeyX, pubkeyX.Length, IntPtr.Zero);
				bnPubkeyY = Native.BN_bin2bn(pubkeyY, pubkeyY.Length, IntPtr.Zero);

				IntPtr group = Native.EC_KEY_get0_group(key);
				pubKey = Native.EC_POINT_new(@group);

				if (Native.EC_POINT_set_affine_coordinates_GFp(@group, pubKey, bnPubkeyX, bnPubkeyY, IntPtr.Zero) == 0)
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
			var k = Native.EC_KEY_new_by_curve_name(ECC.Secp256K1);
			var privKey = Native.BN_bin2bn(secret, 32, IntPtr.Zero);
			var group = Native.EC_KEY_get0_group(k);
			var pubKey = Native.EC_POINT_new(@group);

			Native.EC_POINT_mul(@group, pubKey, privKey, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
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

		internal static byte[] Encrypt(byte[] data, byte[] pubkey)
		{
			// pyelliptic.ECC(curve='secp256k1').encrypt(msg,hexToPubkey(hexPubkey))
			// curve, pubkey_x, pubkey_y, i = ECC._decode_pubkey(pubkey)
			UInt16 curve = ECC.Secp256K1;
			string ciphername = "aes-256-cbc";
			var ephem = new ECC(null, null, null, null, null, curve);

			byte[] pubkey_x = new byte[32];
			byte[] pubkey_y = new byte[32];
			Buffer.BlockCopy(pubkey, 1, pubkey_x, 0, 32);
			Buffer.BlockCopy(pubkey, 33, pubkey_y, 0, 32);

			//key = sha512(ephem.raw_get_ecdh_key(pubkey_x, pubkey_y)).digest()
			byte[] key;
			using (var sha512 = new SHA512Managed())
				key = sha512.ComputeHash(ephem.raw_get_ecdh_key(pubkey_x, pubkey_y));

			byte[] key_e = new byte[32];
			byte[] key_m = new byte[32];
			Buffer.BlockCopy(key, 0, key_e, 0, 32);
			Buffer.BlockCopy(key, 32, key_m, 0, 32);

			pubkey = ephem.get_pubkey();
			byte[] iv = new byte[get_blocksize("aes-256-cbc")];
			using (var rnd = new RNGCryptoServiceProvider())
				rnd.GetBytes(iv);

			var ctx = new Cipher(key_e, iv, true, ciphername);
			var ciphertext = ctx.Ciphering(data);
			var mac = new HMACSHA256(key_m).ComputeHash(ciphertext);

			byte[] result = new byte[iv.Length + pubkey.Length + ciphertext.Length + mac.Length];

			Buffer.BlockCopy(iv,         0, result, 0,                                             iv.Length        );
			Buffer.BlockCopy(pubkey,     0, result, iv.Length,                                     pubkey.Length)    ;
			Buffer.BlockCopy(ciphertext, 0, result, iv.Length + pubkey.Length,                     ciphertext.Length);
			Buffer.BlockCopy(mac,        0, result, iv.Length + pubkey.Length + ciphertext.Length, mac.Length       );

			return result;
		}

		private static int get_blocksize(string ciphername)
		{
			if (ciphername == "aes-256-cbc")
				return 16;
			throw new NotImplementedException();
		}
	}
}
