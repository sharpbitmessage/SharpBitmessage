using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SQLite;
using bitmessage.Crypto;

namespace bitmessage.network
{
	public class Pubkey
	{
		public const UInt64 NetworkDefaultProofOfWorkNonceTrialsPerByte = 320;//#The amount of work that should be performed (and demanded) per byte of the payload. Double this number to double the work.
		public const UInt64 NetworkDefaultPayloadLengthExtraBytes = 14000;// #To make sending short messages a little more difficult, this value is added to the payload length for use in calculating the proof of work target.

		private string _name;
		private ulong _nonceTrialsPerByte;
		private ulong _extraBytes;
		private ulong _version = 3;
		private ulong _stream = 1;
		private uint _behaviorBitfield = 1;
		private Status _status = Status.Valid;

		[Ignore]
		public Status Status
		{
			get { return _status; }
			set { _status = value; }
		}

		public string Label { get; set; }

		[Ignore]
		public UInt64 Version
		{
			get { return _version; }
			set { _version = value; }
		}

		[MaxLength(20)]
		public string Version4DB
		{
			get { return Version.ToString(CultureInfo.InvariantCulture); }
			set { Version = UInt64.Parse(value); }
		}

		[Ignore]
		public UInt64 Stream
		{
			get { return _stream; }
			set { _stream = value; }
		}

		[MaxLength(20)]
		public string Stream4DB
		{
			get { return Stream.ToString(CultureInfo.InvariantCulture); }
			set { Stream = UInt64.Parse(value); }
		}

		public UInt32 BehaviorBitfield
		{
			get { return _behaviorBitfield; }
			set { _behaviorBitfield = value; }
		}

		public byte[] SigningKey { get; set; }
		public byte[] EncryptionKey { get; set; }

		[Ignore]
		public UInt64 NonceTrialsPerByte
		{
			get
			{
				return _nonceTrialsPerByte < NetworkDefaultProofOfWorkNonceTrialsPerByte
					       ? NetworkDefaultProofOfWorkNonceTrialsPerByte
					       : _nonceTrialsPerByte;
			}
			set { _nonceTrialsPerByte = value; }
		}
		[MaxLength(20)]
		public string NonceTrialsPerByte4DB
		{
			get { return NonceTrialsPerByte.ToString(CultureInfo.InvariantCulture); }
			set { NonceTrialsPerByte = UInt64.Parse(value); }
		}

		[Ignore]
		public UInt64 PayloadLengthExtraBytes
		{
			get
			{
				return _extraBytes < NetworkDefaultPayloadLengthExtraBytes
						   ? NetworkDefaultPayloadLengthExtraBytes
						   : _extraBytes;
			}
			set { _extraBytes = value; }
		}
		[MaxLength(20)]
		public string PayloadLengthExtraBytes4DB
		{
			get { return PayloadLengthExtraBytes.ToString(CultureInfo.InvariantCulture); }
			set { PayloadLengthExtraBytes = UInt64.Parse(value); }
		}

		public Pubkey() { }

		public Pubkey(byte[] data, ref int pos, bool checkSign = false)
		{
			Status = Status.Invalid;
			try
			{
				int timeStartPos = pos - 8;
				if (pos == 12) timeStartPos = 8;// now, time have 4 bite length, but i wait 8

				Version = data.ReadVarInt(ref pos);
				Stream = data.ReadVarInt(ref pos);
				BehaviorBitfield = data.ReadUInt32(ref pos);
				SigningKey = ((byte)4).Concatenate(data.ReadBytes(ref pos, 64));
				EncryptionKey = ((byte)4).Concatenate(data.ReadBytes(ref pos, 64));

				if (Version < 3)
				{
					Status = Status.Valid;
					return;
				}
				NonceTrialsPerByte = data.ReadVarInt(ref pos);
				PayloadLengthExtraBytes = data.ReadVarInt(ref pos);

				if (!checkSign)
				{
					Status = Status.Valid;
					return;
				}

				if (timeStartPos >= 0)
				{
					var forCheck = new byte[pos - timeStartPos];
					Buffer.BlockCopy(data, timeStartPos, forCheck, 0, forCheck.Length);

					var signLen = (int)data.ReadVarInt(ref pos);
					var sign = data.ReadBytes(ref pos, signLen);

					if (forCheck.ECDSAVerify(SigningKey, sign))
						Status = Status.Valid;
				}
			}
			catch
			{
				Status = Status.Invalid;
			} 
		}

		[PrimaryKey]
		public string Name
		{
			get
			{
				//if (Status != Status.Valid)
				//	return "Invalid";
				if (String.IsNullOrEmpty(_name))
				{
					byte[] v = Version.VarIntToBytes();
					byte[] s = Stream.VarIntToBytes();

					byte[] ripe = Hash;

					int repeOffset = 0;
					if (Version >= 2)
					{
						if (ripe.Length != 20)
							throw new Exception("Programming error in encodeAddress: The length of a given ripe hash was not 20.");
						if ((ripe[0] == 0) && (ripe[1] == 0))
							repeOffset = 2;
						else if (ripe[0] == 0)
							repeOffset = 1;
					}

					var buff = new byte[v.Length + s.Length + ripe.Length - repeOffset];
					Buffer.BlockCopy(v,    0,          buff, 0,                   v.Length);
					Buffer.BlockCopy(s,    0,          buff, v.Length,            s.Length);
					Buffer.BlockCopy(ripe, repeOffset, buff, v.Length + s.Length, ripe.Length - repeOffset);

					_name = "BM-" + buff.ByteArrayToBase58Check();
				}
				return _name;
			}
			protected set { _name = value; }
		}

		public delegate void EventHandler(Pubkey pubkey);

		public override string ToString()
		{
			return Name;
		}

		/// <summary>
		/// if > 0 then subscription is active
		/// </summary>
		public int SubscriptionIndex { get; set; }

		#region for DB

		public static Pubkey Find(SQLiteAsyncConnection conn, string name)
		{
			var task = conn.Table<Pubkey>().Where(k => (k.Name == name)).FirstOrDefaultAsync();
			return task.Result;
		}

		public virtual Task<int> SaveAsync(SQLiteAsyncConnection db)
		{
			return Status == Status.Valid ? db.InsertOrReplaceAsync(this) : null;
		}

		#endregion for DB

		private byte[] _hash;

		[Ignore]
		public byte[] Hash
		{
			get
			{
				if (_hash == null)
				{
					byte[] buff = SigningKey.Concatenate(EncryptionKey);
					byte[] sha = new SHA512Managed().ComputeHash(buff);
					_hash = RIPEMD160.Create().ComputeHash(sha);
				}
				return _hash;
			}
			set { _hash = value; }
		}

		[MaxLength(40)]
		public string Hash4DB
		{
			get { return Hash.ToHex(false); }
			set { Hash = value.HexToBytes(); }
		}

		public byte[] DecryptAes256Cbc4Broadcast(byte[] data)
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
				key = sha512.ComputeHash(new ECC(null, null, null, null, Sha512VersionStreamHashFirst32(),ECC.Secp256K1).raw_get_ecdh_key(pubkeyX, pubkeyY));

			//key_e, key_m = key[:32], key[32:]
// ReSharper disable InconsistentNaming
			var key_e = new byte[32];
			var key_m = new byte[32];
// ReSharper restore InconsistentNaming
            Buffer.BlockCopy(key, 0, key_e, 0, 32);
			Buffer.BlockCopy(key, 32, key_m, 0, 32);

			//if hmac_sha256(key_m, ciphertext) != mac:
			//	raise RuntimeError("Fail to verify data")
			if (!new HMACSHA256(key_m).ComputeHash(ciphertext).SequenceEqual(mac))
				throw new Exception("Fail to verify data");

			var ctx = new Cipher(key_e, iv, false);
			return ctx.Ciphering(ciphertext);
		}

		public byte[] Sha512VersionStreamHashFirst32()
		{
			var result = new byte[32];
			using (var sha512 = new SHA512Managed())
				Buffer.BlockCopy(
					sha512.ComputeHash(Version.VarIntToBytes().Concatenate(Stream.VarIntToBytes()).Concatenate(Hash)), 0,
					result, 0, 32);
			return result;
		}

		public static IEnumerable<Pubkey> GetAll(SQLiteAsyncConnection conn)
		{
			return conn.Table<Pubkey>().ToListAsync().Result;
		}

		internal byte[] GetPayload4Broadcast()
		{
			var payload = new MemoryStream(500);

            payload.WriteVarInt(Version);
            payload.WriteVarInt(Stream);
			payload.Write(BehaviorBitfield);
             
			payload.Write(SigningKey,1,SigningKey.Length-1);
			payload.Write(EncryptionKey,1,EncryptionKey.Length-1);

			if (Version>=3)
			{
				payload.WriteVarInt(NonceTrialsPerByte);
				payload.WriteVarInt(NetworkDefaultPayloadLengthExtraBytes);
			}

			return payload.ToArray();
		}
	}
}