using System;
using System.Globalization;
using System.Security.Cryptography;
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

		public Status Status { get; set; }

		public string Label { get; set; }

		[Ignore]
		public UInt64 Version { get; set; }
		public string Version4DB
		{
			get { return Version.ToString(CultureInfo.InvariantCulture); }
			set { Version = UInt64.Parse(value); }
		}

		[Ignore]
		public UInt64 Stream { get; set; }
		public string Stream4DB
		{
			get { return Stream.ToString(CultureInfo.InvariantCulture); }
			set { Stream = UInt64.Parse(value); }
		}

		public UInt32 BehaviorBitfield { get; set; }
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
		public string NonceTrialsPerByte4DB
		{
			get { return NonceTrialsPerByte.ToString(CultureInfo.InvariantCulture); }
			set { NonceTrialsPerByte = UInt64.Parse(value); }
		}

		[Ignore]
		public UInt64 ExtraBytes
		{
			get
			{
				return _extraBytes < NetworkDefaultPayloadLengthExtraBytes
						   ? NetworkDefaultPayloadLengthExtraBytes
						   : _extraBytes;
			}
			set { _extraBytes = value; }
		}
		public string ExtraBytes4DB
		{
			get { return ExtraBytes.ToString(CultureInfo.InvariantCulture); }
			set { ExtraBytes = UInt64.Parse(value); }
		}

		public Pubkey(Payload payload)
		{
			Status = Status.Invalid;

			int pos = payload.FirstByteAfterTime;

			Version = payload.SentData.ReadVarInt(ref pos);

			if ((Version<=3))
			{
				Stream = payload.SentData.ReadVarInt(ref pos);
				BehaviorBitfield = payload.SentData.ReadUInt32(ref pos);
				SigningKey = ((byte) 4).Concatenate(payload.SentData.ReadBytes(ref pos, 64));
				EncryptionKey = ((byte) 4).Concatenate(payload.SentData.ReadBytes(ref pos, 64));

				if (Version==3)
				{
					NonceTrialsPerByte = payload.SentData.ReadUInt64(ref pos);
					ExtraBytes = payload.SentData.ReadUInt64(ref pos);
				}
				Status = Status.Valid;
			}
			// TODO CheckSing
		}

		public Pubkey(
					UInt64 addressVersion,
					UInt64 streamNumber,
					UInt32 behaviorBitfield,
					byte[] publicSigningKey,
					byte[] publicEncryptionKey
					)
		{
			Version = addressVersion;
			Stream = streamNumber;
			BehaviorBitfield = behaviorBitfield;
			SigningKey = publicSigningKey;
			EncryptionKey = publicEncryptionKey;
		}

		[PrimaryKey]
		public string Name
		{
			get
			{
				if (Status != Status.Valid)
					return "Invalid";
				if (String.IsNullOrEmpty(_name))
					_name = EncodeAddress(Version, Stream, Hash);
				return _name;
			}
			protected set { _name = value; }
		}

		public delegate void EventHandler(Pubkey pubkey);

		public override string ToString()
		{
			return Name;
		}

		#region for DB

		public Pubkey() { }

		public static Pubkey GetPubkey(SQLiteAsyncConnection conn, string name)
		{
			var task = conn.Table<Pubkey>().Where(k => (k.Name == name)).FirstOrDefaultAsync();
			task.Wait();
			return task.Result;
		}

		public virtual void SaveAsync(SQLiteAsyncConnection db) { db.InsertAsync(this); }

		#endregion for DB
		
		[Ignore]
		public byte[] Hash
		{
			get
			{
				byte[] buff = SigningKey.Concatenate(EncryptionKey);
				byte[] sha = new SHA512Managed().ComputeHash(buff);
				return RIPEMD160.Create().ComputeHash(sha);
			}
		}

		private static string EncodeAddress(UInt64 version, UInt64 stream, byte[] ripe)
		{
			byte[] v = version.VarIntToBytes();
			byte[] s = stream.VarIntToBytes();

			int repeOffset = 0;

			if (version >= 2)
				if ((ripe[0] == 0) && (ripe[1] == 0))
					repeOffset = 2;
				else if (ripe[0] == 0)
					repeOffset = 1;

			byte[] buff = new byte[v.Length + s.Length + ripe.Length - repeOffset];
			Buffer.BlockCopy(v, 0, buff, 0, v.Length);
			Buffer.BlockCopy(s, 0, buff, v.Length, s.Length);
			Buffer.BlockCopy(ripe, repeOffset, buff, v.Length + s.Length, ripe.Length - repeOffset);

			string result = "BM-" + Base58.ByteArrayToBase58Check(buff);
			return result;
		}
	}
}