using System;
using System.Globalization;
using System.IO;
using SQLite;
using bitmessage.Crypto;
using bitmessage.network;
using System.Linq;

namespace bitmessage
{
	public class Broadcast
	{
		private readonly Bitmessage _bm;
		private string _subject;
		private string _body;
		private EncodingType _encodingType = EncodingType.Simple;
		private ulong _version = 1;

		public Broadcast() {}

		public Broadcast(Bitmessage bm)
		{
			_bm = bm;
		}

		public Broadcast(Bitmessage bm, Payload payload)
		{
			Status = Status.Invalid;

			int pos = payload.FirstByteAfterTime;

			Version = payload.SentData.ReadVarInt(ref pos);
			if (Version != 1) return;

			Pubkey pubKey = new Pubkey(
				payload.SentData.ReadVarInt(ref pos),
				payload.SentData.ReadVarInt(ref pos),
				payload.SentData.ReadUInt32(ref pos),
				((byte) 4).Concatenate(payload.SentData.ReadBytes(ref pos, 64)),
				((byte) 4).Concatenate(payload.SentData.ReadBytes(ref pos, 64))
				);

			if (!pubKey.Hash.SequenceEqual(payload.SentData.ReadBytes(ref pos, 20)))
				throw new Exception("Key.InventoryVector varification error");

			pubKey.SaveAsync(bm.DB);
			Key = pubKey.Name;

			EncodingType = (EncodingType) payload.SentData.ReadVarInt(ref pos);
			payload.SentData.ReadVarStrSubjectAndBody(ref pos, out _subject, out _body);

			int posOfEndMsg = pos;
			UInt64 signatureLength = payload.SentData.ReadVarInt(ref pos);
			Signature = payload.SentData.ReadBytes(ref pos, (int) signatureLength);

			byte[] data = new byte[posOfEndMsg - 12];
			Buffer.BlockCopy(payload.SentData, 12, data, 0, posOfEndMsg - 12);

			if (data.ECDSAVerify(pubKey.SigningKey, Signature))
				Status = Status.Valid;

			Status = Status.Valid; // TODO Bug in PyBitmessage  !!!
		}

		[PrimaryKey]
		[MaxLength(64)]
		public string InventoryVectorHex
		{
			get { return GetPayload().InventoryVector.ToHex(false); }
			set { _payload = Payload.Get(_bm.DB, value); }
		}

		private Payload _payload;

		private Payload GetPayload()
		{
			if (_payload == null)
			{
				PrivateKey privkey = PrivateKey.GetPrivateKey(_bm.DB, Key);
				Pubkey pubkey = privkey;
				if (privkey == null)
				{
					privkey = PrivateKey.FirstOrDefault(_bm.DB);
					pubkey  = Pubkey.GetPubkey(_bm.DB, Key);

					if (privkey==null)
					{
						privkey = new PrivateKey("my");
					}
					if (pubkey == null)
						pubkey = privkey;
				}

				MemoryStream data = new MemoryStream(1000 + Subject.Length + Body.Length); // TODO realy 1000?
				Random rnd = new Random();
				var dt = DateTime.UtcNow.ToUnix() + (ulong) rnd.Next(600) - 300;
				data.Write(dt);
				data.WriteVarInt(Version);

				data.WriteVarInt(pubkey.Version);
				data.WriteVarInt(pubkey.Stream);
				data.Write(pubkey.BehaviorBitfield);
				data.Write(pubkey.SigningKey, 1, pubkey.SigningKey.Length - 1);
				data.Write(pubkey.EncryptionKey, 1, pubkey.EncryptionKey.Length - 1);
				if (pubkey.Hash.Length != 20) throw new Exception("error AddressHash length");
				data.Write(pubkey.Hash, 0, pubkey.Hash.Length);

				Byte encodingType = (byte) EncodingType;
				data.Write(encodingType);
				data.WriteVarStr(Subject + "\n" + Body);

				byte[] signature = privkey.Sign(data.ToArray());

				data.WriteVarInt((UInt64) signature.Length);
				data.Write(signature, 0, signature.Length);

				_payload = new Payload("broadcast", Payload.AddProofOfWork(data.ToArray()));
			}
			return _payload;
		}

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

		public string Key { get; set; }
	
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
				return Key;
			return "Message don't valid. Version=" + Version;
		}

		public void SaveAsync(SQLiteAsyncConnection db)
		{
			db.InsertAsync(this);
		}

		internal void Send()
		{
			GetPayload().SaveAsync(_bm);
		}
	}
}
