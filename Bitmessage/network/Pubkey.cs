using System;
using System.Security.Cryptography;
using bitmessage.Crypto;

namespace bitmessage.network
{
	public class Pubkey
	{
		public readonly Status Status;
		public readonly UInt64 Version;
		public readonly UInt64 Stream;
		public readonly UInt32 BehaviorBitfield;
		public readonly byte[] SigningKey;
		public readonly byte[] EncryptionKey;
		public readonly UInt64 NonceTrialsPerByte;
		public readonly UInt64 ExtraBytes;
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

		private string _name;
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
		}

		public delegate void EventHandler(Pubkey pubkey);

		public override string ToString()
		{
			return Name;
		}
	}
}
