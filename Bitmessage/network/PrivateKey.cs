using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenSSL.Core;
using SQLite;
using bitmessage.Crypto;

namespace bitmessage.network
{
	public class PrivateKey : Pubkey
	{
		private readonly string _privSigningKeyWif;
		private readonly string _privEncryptionKeyWif;

		public PrivateKey(string label, bool eighteenByteRipe = false)
		{
			var startTime = DateTime.Now;
			Label = label;

			RNGCryptoServiceProvider rnd = new RNGCryptoServiceProvider();

			byte[] potentialPrivSigningKey = new byte[32];
			rnd.GetBytes(potentialPrivSigningKey);
			byte[] potentialPubSigningKey = pointMult(potentialPrivSigningKey);

			int numberOfAddressesWeHadToMakeBeforeWeFoundOneWithTheCorrectRipePrefix = 0;

			byte[] ripemd160;
			byte[] potentialPrivEncryptionKey = new byte[32];

			while (true)
			{
				numberOfAddressesWeHadToMakeBeforeWeFoundOneWithTheCorrectRipePrefix += 1;
				rnd.GetBytes(potentialPrivEncryptionKey);
				
				byte[] potentialPubEncryptionKey = pointMult(potentialPrivEncryptionKey);

				byte[] buff = potentialPubSigningKey.Concatenate(potentialPubEncryptionKey);
				byte[] sha = new SHA512Managed().ComputeHash(buff);
				ripemd160 = RIPEMD160.Create().ComputeHash(sha);

				if (eighteenByteRipe)
				{
					if ((ripemd160[0] == 0) && (ripemd160[1] == 0))
						break;
				}
				else
				{
					if (ripemd160[0] == 0)
						break;
				}
			}		
			Debug.WriteLine("Generated address with ripe digest:" + ripemd160.ToHex() );
			Debug.WriteLine("Address generator calculated" + numberOfAddressesWeHadToMakeBeforeWeFoundOneWithTheCorrectRipePrefix +
			                " addresses at " +
			                numberOfAddressesWeHadToMakeBeforeWeFoundOneWithTheCorrectRipePrefix/(DateTime.Now - startTime).TotalSeconds +
			                " addresses per second before finding one with the correct ripe-prefix.");
			Name = Base58.EncodeAddress(3, 1, ripemd160);

			// NonceTrialsPerByte используется значение по умолчанию
			// payloadLengthExtraBytes используется значение по умолчанию
			_privSigningKeyWif = PrivateKey2Wif(potentialPrivSigningKey);
			_privEncryptionKeyWif = PrivateKey2Wif(potentialPrivEncryptionKey);
			
			Status = Status.Valid;
		}

		private static string PrivateKey2Wif(byte[] privateKey)
		{
			//#An excellent way for us to store our keys is in Wallet Import Format. Let us convert now.
			//#https://en.bitcoin.it/wiki/Wallet_import_format
			var privKeyAnd80 = ((byte)0x80).Concatenate(privateKey);
			byte[] hash;
			using (var sha256 = new SHA256Managed())
				hash = sha256.ComputeHash(sha256.ComputeHash(privKeyAnd80));
			var checksum = new byte[4];
			Array.Copy(hash, checksum, 4);
			return privKeyAnd80.Concatenate(checksum).ByteArrayToBase58();
		}

		const int Secp256K1 = 714;
		private byte[] pointMult(byte[] secret)
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
			return outBuf; ;
		}

		#region for DB

		public PrivateKey(){}

		public static PrivateKey GetPrivateKey(SQLiteAsyncConnection conn, string name)
		{
			var task = conn.Table<PrivateKey>().Where(k => (k.Name == name)).FirstOrDefaultAsync();
			task.Wait();
			return task.Result;
		}

		public new void SaveAsync(SQLiteAsyncConnection db)
		{
			db.InsertAsync(this);
		}

		public static IEnumerable<PrivateKey> GetAll(SQLiteAsyncConnection conn)
		{
			var task = conn.Table<PrivateKey>().OrderBy(k => k.Name).ToListAsync();
			task.Wait();
			return task.Result;
		}

		public static PrivateKey FirstOrDefault(SQLiteAsyncConnection conn)
		{
			var task = conn.Table<PrivateKey>().FirstOrDefaultAsync();
			task.Wait();
			return task.Result;
		}
		
		#endregion for DB
	}	
}