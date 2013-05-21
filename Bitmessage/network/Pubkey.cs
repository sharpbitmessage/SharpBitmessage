using System;
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
		public UInt64 Version { get; set; }
		public UInt64 Stream { get; set; }
		public UInt32 BehaviorBitfield { get; set; }
		public byte[] SigningKey { get; set; }
		public byte[] EncryptionKey { get; set; }

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

		public Pubkey(Payload payload)
		{
			Status = Status.Invalid;

			int pos = payload.FirstByteAfterTime;

			Version = payload.Data.ReadVarInt(ref pos);

			if ((Version<=3))
			{
				Stream = payload.Data.ReadVarInt(ref pos);
				BehaviorBitfield = payload.Data.ReadUInt32(ref pos);
				SigningKey = ((byte) 4).Concatenate(payload.Data.ReadBytes(ref pos, 64));
				EncryptionKey = ((byte) 4).Concatenate(payload.Data.ReadBytes(ref pos, 64));

				if (Version==3)
				{
					NonceTrialsPerByte = payload.Data.ReadUInt64(ref pos);
					ExtraBytes = payload.Data.ReadUInt64(ref pos);
				}
				Status = Status.Valid;
			}
			// TODO CheckSing
		}

		[PrimaryKey]
		public string Name
		{
			get
			{
				if (Status != Status.Valid)
					return "Invalid";
				if (string.IsNullOrEmpty(_name))
				{
					byte[] buff = SigningKey.Concatenate(EncryptionKey);
					byte[] sha = new SHA512Managed().ComputeHash(buff);
					byte[] ripemd160 = RIPEMD160.Create().ComputeHash(sha);
					_name = Base58.EncodeAddress(Version, Stream, ripemd160);
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
	}
}
