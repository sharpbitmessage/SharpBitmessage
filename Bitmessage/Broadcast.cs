using System;
using System.Security.Cryptography;
using System.Text;
using bitmessage.Crypto;
using bitmessage.network;
using System.Linq;

namespace bitmessage
{
	public class Broadcast
	{
		public Broadcast()
		{
		}

		public Broadcast(Payload payload)
		{
			Status = Status.Invalid;

			int pos = payload.FirstByteAfterTime;

			Version = payload.Data.ReadVarInt(ref pos);
			if (Version != 1) return;

			AddressVersion = payload.Data.ReadVarInt(ref pos);
			StreamNumber = payload.Data.ReadVarInt(ref pos);
			BehaviorBitfield = payload.Data.ReadUInt32(ref pos);
			PublicSigningKey = ((byte)4).Concatenate(payload.Data.ReadBytes(ref pos, 64));
			PublicEncryptionKey = ((byte)4).Concatenate(payload.Data.ReadBytes(ref pos, 64));
			AddressHash = payload.Data.ReadBytes(ref pos, 20);
			EncodingType = (EncodingType)payload.Data.ReadVarInt(ref pos);
			Msg = payload.Data.ReadVarStr(ref pos);

			int posOfEndMsg = pos;
			UInt64 signatureLength = payload.Data.ReadVarInt(ref pos);
			Signature = payload.Data.ReadBytes(ref pos, (int)signatureLength);

			if (AddressIsValid)
			{
				byte[] data = new byte[posOfEndMsg - 12];
				Buffer.BlockCopy(payload.Data, 12, data, 0, posOfEndMsg - 12);
				if (data.ECDSAVerify(PublicSigningKey, Signature))
					Status = Status.Valid;
				Status = Status.Valid; // TODO Bug in PyBitmessage  !!!
			}
		}

		public UInt64 Version { get; set; }
		public UInt64 AddressVersion { get; set; }
		public UInt64 StreamNumber { get; set; }
		public UInt32 BehaviorBitfield { get; set; }
		public byte[] PublicSigningKey { get; set; }
		public byte[] PublicEncryptionKey { get; set; }
		public byte[] AddressHash { get; set; }         // TODO Сохранять класс Pubkey
		public EncodingType EncodingType { get; set; }
		public string Msg { get; set; }
		public byte[] Signature { get; set; }
		public DateTime ReceivedTime { get; set; }

		public Status Status { get; set; }

		private bool AddressIsValid
		{
			get
			{
				byte[] buff = PublicSigningKey.Concatenate(PublicEncryptionKey);
				byte[] sha = new SHA512Managed().ComputeHash(buff);
				byte[] ripemd160 = RIPEMD160.Create().ComputeHash(sha);

				if (!ripemd160.SequenceEqual(AddressHash))
					return false;

				return true;
			}
		}

		public delegate void EventHandler(Broadcast broadcast);

		public override string ToString()
		{
			return "Broadcast from " + Address();
		}

		public string Address()
		{
			if (Status == Status.Valid)
				return Base58.EncodeAddress(AddressVersion, StreamNumber, AddressHash);
			else
				return "Message don't valid. Version=" + Version;
		}
	}
}
