using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Security.Cryptography;
using SQLite;
using bitmessage.Crypto;
using System.Linq;

namespace bitmessage.network
{
	public class PrivateKey : Pubkey
	{
		public string PrivSigningKeyWif { get; set; }
		public string PrivEncryptionKeyWif { get; set; }

		public PrivateKey(string label, bool eighteenByteRipe = false)
		{
			var startTime = DateTime.Now;
			Label = label;

			RNGCryptoServiceProvider rnd = new RNGCryptoServiceProvider();

			byte[] potentialPrivSigningKey = new byte[32];
			rnd.GetBytes(potentialPrivSigningKey);
			SigningKey = ECDSA.PointMult(potentialPrivSigningKey);

			int numberOfAddressesWeHadToMakeBeforeWeFoundOneWithTheCorrectRipePrefix = 0;

			byte[] ripemd160;
			byte[] potentialPrivEncryptionKey = new byte[32];

			while (true)
			{
				numberOfAddressesWeHadToMakeBeforeWeFoundOneWithTheCorrectRipePrefix += 1;
				rnd.GetBytes(potentialPrivEncryptionKey);

				EncryptionKey = ECDSA.PointMult(potentialPrivEncryptionKey);

				byte[] buff = SigningKey.Concatenate(EncryptionKey);
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

			// NonceTrialsPerByte используется значение по умолчанию
			// payloadLengthExtraBytes используется значение по умолчанию
			PrivSigningKeyWif = PrivateKey2Wif(potentialPrivSigningKey);
			PrivEncryptionKeyWif = PrivateKey2Wif(potentialPrivEncryptionKey);
			
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

		/// <summary>
		/// if input         5Kek19qnxAqFsLXKToVMWcbpQryxzwaqtHLnQ9WwrZR8yC8aBck
		/// then hex result  f1b868e74dd6dd9d13a6bce594e62baf71fa367fc7747bdf019380adde153253
		/// </summary>
		/// <param name="wif"></param>
		/// <returns></returns>
		private static byte[] Wif2PrivateKey(string wif)
		{
			byte[] bytes = Base58.Base58ToByteArray(wif);
			Byte[] privKeyAnd80 = new byte[bytes.Length - 4];
			Buffer.BlockCopy(bytes, 0, privKeyAnd80, 0, privKeyAnd80.Length);

			byte[] hash = new byte[4];
			using (var sha256 = new SHA256Managed())
				Buffer.BlockCopy( 
					sha256.ComputeHash(sha256.ComputeHash(privKeyAnd80)), 0, 
					hash, 0, 4);

			byte[] checkSum = new byte[4];
			Buffer.BlockCopy(bytes, privKeyAnd80.Length, checkSum, 0, 4);

			if (checkSum.SequenceEqual(hash) && (privKeyAnd80[0] == 0x80))
			{
				byte[] result = new byte[privKeyAnd80.Length - 1];
				Buffer.BlockCopy(privKeyAnd80, 1, result, 0, result.Length);
				return result;
			}
			return null;
		}

		public byte[] Sign(byte[] data)
		{
			return ECDSA.ECDSASign(data, Wif2PrivateKey(PrivSigningKeyWif));
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