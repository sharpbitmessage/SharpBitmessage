using System;
using System.IO;
using System.Security.Cryptography;
using SQLite;
using bitmessage.Crypto;
using bitmessage.network;
using System.Linq;

namespace bitmessage
{
	public class Broadcast
	{
		private string _subject;
		private string _body;

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
			payload.Data.ReadVarStrSubjectAndBody(ref pos, out _subject, out _body);
			
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

		public Payload Payload(SQLiteAsyncConnection db)
		{
			MemoryStream data = new MemoryStream(1000+Subject.Length+Body.Length); // TODO realy 1000?
			Random rnd = new Random();
			var dt = DateTime.UtcNow.ToUnix() + (ulong)rnd.Next(600) - 300;
			data.Write(dt);
			data.WriteVarInt(1);
			data.WriteVarInt(AddressVersion);
			data.WriteVarInt(StreamNumber);
			data.Write(BehaviorBitfield);
			data.Write(PublicSigningKey, 1, PublicSigningKey.Length - 1);
			data.Write(PublicEncryptionKey, 1, PublicSigningKey.Length - 1);
			UpdataAddressHash();
			if (AddressHash.Length != 20) throw new Exception("error AddressHash length");
			data.Write(AddressHash, 0, AddressHash.Length);
			data.Write(2);
			data.WriteVarStr(Subject + "/n" + Body);

			throw new NotImplementedException();
			//byte[] signature = data.ToArray().Sign(PrivateKey.GetPrivateKey(KeyName)); 

			//data.WriteVarInt((UInt64) signature.Length);
			//data.Write(signature, 0, signature.Length);

			//Payload result = new Payload("broadcast", data.ToArray());
			//result.Proof;
			//result.SaveAsync(db); // ??? и разослать

			//return result;
		}

		public UInt64 Version { get; set; }
	
		public UInt64 AddressVersion { get; set; }
		public UInt64 StreamNumber { get; set; }
		public UInt32 BehaviorBitfield { get; set; }
		public byte[] PublicSigningKey { get; set; }
		public byte[] PublicEncryptionKey { get; set; }
		public byte[] AddressHash { get; set; }         // TODO Сохранять класс Pubkey

		public EncodingType EncodingType { get; set; }
		public string Subject
		{
			get { return _subject; }
			set { _subject = value; }
		}
		public string Body
		{
			get { return _body; }
			set { _body = value; }
		}
		public byte[] Signature { get; set; }
		public DateTime ReceivedTime { get; set; }

		public Status Status { get; set; }

		private void UpdataAddressHash()
		{
			byte[] buff = PublicSigningKey.Concatenate(PublicEncryptionKey);
			byte[] sha = new SHA512Managed().ComputeHash(buff);
			AddressHash = RIPEMD160.Create().ComputeHash(sha);
		}

		private bool AddressIsValid
		{
			get
			{
				byte[] oldAddressHash = AddressHash;
				UpdataAddressHash();
				if (oldAddressHash.SequenceEqual(AddressHash))
					return true;

				AddressHash = oldAddressHash;
				return false;
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
			return "Message don't valid. Version=" + Version;
		}
	}
}
