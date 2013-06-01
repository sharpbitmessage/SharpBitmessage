using System;
using System.IO;
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
		private EncodingType _encodingType = EncodingType.Simple;

		public Broadcast()
		{
		}

		public Broadcast(Payload payload)
		{
			Status = Status.Invalid;

			int pos = payload.FirstByteAfterTime;

			Version = payload.SentData.ReadVarInt(ref pos);
			if (Version != 1) return;

			Key = new Pubkey(
				payload.SentData.ReadVarInt(ref pos),
				payload.SentData.ReadVarInt(ref pos),
				payload.SentData.ReadUInt32(ref pos),
				((byte) 4).Concatenate(payload.SentData.ReadBytes(ref pos, 64)),
				((byte) 4).Concatenate(payload.SentData.ReadBytes(ref pos, 64))
				);

			if (!Key.Hash.SequenceEqual(payload.SentData.ReadBytes(ref pos, 20)))
				throw new Exception("Key.InventoryVector varification error");

			EncodingType = (EncodingType) payload.SentData.ReadVarInt(ref pos);
			payload.SentData.ReadVarStrSubjectAndBody(ref pos, out _subject, out _body);

			int posOfEndMsg = pos;
			UInt64 signatureLength = payload.SentData.ReadVarInt(ref pos);
			Signature = payload.SentData.ReadBytes(ref pos, (int) signatureLength);

			byte[] data = new byte[posOfEndMsg - 12];
			Buffer.BlockCopy(payload.SentData, 12, data, 0, posOfEndMsg - 12);
			if (data.ECDSAVerify(Key.SigningKey, Signature))
				Status = Status.Valid;
			Status = Status.Valid; // TODO Bug in PyBitmessage  !!!
		}

		public Payload Payload()
		{
			MemoryStream data = new MemoryStream(1000+Subject.Length+Body.Length); // TODO realy 1000?
			Random rnd = new Random();
			var dt = DateTime.UtcNow.ToUnix() + (ulong)rnd.Next(600) - 300;
			data.Write(dt);
			data.WriteVarInt(Version);
			data.WriteVarInt(Key.Version);
			data.WriteVarInt(Key.Stream);
			data.Write(Key.BehaviorBitfield);
			data.Write(Key.SigningKey, 1, Key.SigningKey.Length - 1);
			data.Write(Key.EncryptionKey, 1, Key.EncryptionKey.Length - 1);			
			if (Key.Hash.Length != 20) throw new Exception("error AddressHash length");
			data.Write(Key.Hash, 0, Key.Hash.Length);
			data.Write((byte) EncodingType);
			data.WriteVarStr(Subject + "/n" + Body);

			if (!(Key is PrivateKey))
				throw new Exception("Broadcast don't contain private key");
			byte[] signature = (Key as PrivateKey).Sign(data.ToArray());

			data.WriteVarInt((UInt64)signature.Length);
			data.Write(signature, 0, signature.Length);

			Payload result = new Payload("broadcast", network.Payload.AddProofOfWork(data.ToArray()));

			return result;
		}

		public UInt64 Version { get; set; }

		public Pubkey Key { get; set; }
	
		public EncodingType EncodingType
		{
			get { return _encodingType; }
			set { _encodingType = value; }
		}

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

		public delegate void EventHandler(Broadcast broadcast);

		public override string ToString()
		{
			return "Broadcast from " + Address();
		}

		public string Address()
		{
			if (Status == Status.Valid)
				return Key.Name;
			return "Message don't valid. Version=" + Version;
		}

		public void SaveAsync(SQLiteAsyncConnection db) { db.InsertAsync(this); }
	}
}
