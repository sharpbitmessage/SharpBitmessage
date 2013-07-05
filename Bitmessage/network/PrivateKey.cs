using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SQLite;
using bitmessage.Crypto;
using System.Linq;

namespace bitmessage.network
{
	public class PrivateKey : Pubkey, ICanBeSent
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

		public static PrivateKey GetPrivateKey(SQLiteAsyncConnection conn, string name)
		{
			var task = conn.Table<PrivateKey>().Where(k => (k.Name == name)).FirstOrDefaultAsync();
			return task.Result;
		}

		public DateTime LastPubkeySendTime
		{
			get { return _lastPubkeySendTime; }
			set { _lastPubkeySendTime = value; }
		}

		public string Command
		{
			get { return "pubkey"; }
		}

		private byte[] _sendData;
		private DateTime _lastPubkeySendTime = Helper.Epoch;

		public byte[] SentData
		{
			get
			{
				if (_sendData == null)
				{
					MemoryStream payload = new MemoryStream(1000);

					Random rnd = new Random();
					var dt = DateTime.UtcNow.ToUnix() + (ulong)rnd.Next(600) - 300;
					payload.Write(dt);

					payload.WriteVarInt(Version);
					payload.WriteVarInt(Stream);

					payload.Write(BehaviorBitfield);

					if (SigningKey.Length != 65)
						throw new Exception("SigningKey.Length!=65");
					if (EncryptionKey.Length != 65)
						throw new Exception("EncryptionKey.Length!=65");
					payload.Write(SigningKey, 1, 64);
					payload.Write(EncryptionKey, 1, 64);

					payload.WriteVarInt(NonceTrialsPerByte);
					payload.WriteVarInt(PayloadLengthExtraBytes);

					byte[] signature = Sign(payload.ToArray());

					payload.WriteVarInt((UInt64) signature.Length);
					payload.Write(signature, 0, signature.Length);

					_sendData = ProofOfWork.AddPow(payload.ToArray());
				}
				return _sendData;
			}
		}

		#region for DB

		public PrivateKey(){}

		public new Task<int> SaveAsync(SQLiteAsyncConnection db)
		{
			return db.InsertOrReplaceAsync(this);
		}

		public new static IEnumerable<PrivateKey> GetAll(SQLiteAsyncConnection conn)
		{
			return conn.Table<PrivateKey>().ToListAsync().Result;
		}

		public static PrivateKey FirstOrDefault(SQLiteAsyncConnection conn)
		{
			var task = conn.Table<PrivateKey>().FirstOrDefaultAsync();
			return task.Result;
		}

		internal static PrivateKey Find(SQLiteAsyncConnection conn, GetPubkey getpubkey)
		{
			var task = conn.Table<PrivateKey>()
				.Where(k => k.Hash4DB == getpubkey.Hash4DB)
				.Where(k => k.Version4DB == getpubkey.Version4DB)
				.Where(k => k.Stream4DB == getpubkey.Stream4DB)
				.FirstOrDefaultAsync();
			return task.Result;
		}
		
		#endregion for DB

		internal void SendAsync(Bitmessage bitmessage)
		{
			LastPubkeySendTime = DateTime.UtcNow;
			SaveAsync(bitmessage.DB);
			new Task(() =>
				new Payload(Command, SentData).SaveAsync(bitmessage)
				).Start();
		}

		public byte[] DecryptAES256CBC4Msg(byte[] data)
		{
			//blocksize = OpenSSL.get_cipher(ciphername).get_blocksize()
			const int blocksize = 16;

			//iv = data[:blocksize]
			//i = blocksize
			int pos = 0;
			var iv = data.ReadBytes(ref pos, blocksize);

			//curve, pubkey_x, pubkey_y, i2 = ECC._decode_pubkey(data[i:])
			//i += i2
			UInt16 curve;
			byte[] pubkeyX;
			byte[] pubkeyY;
			ECC._decode_pubkey(data, out curve, out pubkeyX, out pubkeyY, ref pos);

			//ciphertext = data[i:len(data)-32]
			//i += len(ciphertext)
			var ciphertext = data.ReadBytes(ref pos, data.Length - pos - 32);

			//mac = data[i:]
			var mac = data.ReadBytes(ref pos, 32);

			//key = sha512(self.raw_get_ecdh_key(pubkey_x, pubkey_y)).digest()
			byte[] key;
			using (var sha512 = new SHA512Managed())
				key = sha512.ComputeHash(new ECC(null, null, null, null, Wif2PrivateKey(PrivEncryptionKeyWif), ECC.Secp256K1).raw_get_ecdh_key(pubkeyX, pubkeyY));

			//key_e, key_m = key[:32], key[32:]
			byte[] key_e = new byte[32];
			byte[] key_m = new byte[32];
			Buffer.BlockCopy(key, 0, key_e, 0, 32);
			Buffer.BlockCopy(key, 32, key_m, 0, 32);

			//if hmac_sha256(key_m, ciphertext) != mac:
			//	raise RuntimeError("Fail to verify data")
			if (!new HMACSHA256(key_m).ComputeHash(ciphertext).SequenceEqual(mac))
				throw new Exception("Fail to verify data");

			var ctx = new Cipher(key_e, iv, false);
			return ctx.Ciphering(ciphertext);
		}
	}	
}