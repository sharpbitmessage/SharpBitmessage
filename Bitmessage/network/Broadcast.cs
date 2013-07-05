using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using bitmessage.Crypto;
using System.Linq;

namespace bitmessage.network
{
	public class Broadcast
	{
		private readonly Bitmessage _bm;
		private string _subject;
		private string _body;
		private EncodingType _encodingType = EncodingType.Simple;
		private ulong _version = 2;
		private ulong _stream = 1;

		public Broadcast() {}

		public Broadcast(Bitmessage bm)
		{
			_bm = bm;
		}

		public Broadcast(Bitmessage bm, Payload payload)
		{
			Status = Status.Invalid;
			try
			{
				int pos = payload.FirstByteAfterTime;
				Version = payload.SentData.ReadVarInt(ref pos);

				if (Version == 2)
				{
					_inventoryVector = payload.InventoryVector;

					Stream = payload.SentData.ReadVarInt(ref pos);
					byte[] encrypted = payload.SentData.ReadBytes(ref pos, payload.Length - pos);
					byte[] decryptedData = null;
					Pubkey encryptionKey = null;
					foreach (Pubkey subscriptionKey in bm.Subscriptions(Stream4DB))
					{
						if (subscriptionKey.Stream != _stream) continue;
						try
						{
							decryptedData = subscriptionKey.DecryptAES256CBC4Broadcast(encrypted);
							encryptionKey = subscriptionKey;
						}
							// ReSharper disable EmptyGeneralCatchClause
						catch
						{
						} // ReSharper restore EmptyGeneralCatchClause

						if (decryptedData != null)
							break;
					}

					if ((decryptedData == null) || (encryptionKey == null))
					{
						Status = Status.Encrypted;
						return;
					}

					if (encryptionKey.SubscriptionIndex < int.MaxValue)
					{
						encryptionKey.SubscriptionIndex += 1;
						encryptionKey.SaveAsync(bm.DB).Wait();
					}

					pos = 0;
					/*var signedBroadcastVersion = */
					decryptedData.ReadVarInt(ref pos);
					Pubkey keyFromMsg = new Pubkey(decryptedData, ref pos);
					if (!encryptionKey.Hash.SequenceEqual(keyFromMsg.Hash))
						return;
					Key = encryptionKey.Name;
					EncodingType = (EncodingType) decryptedData.ReadVarInt(ref pos);
					decryptedData.ReadVarStrSubjectAndBody(ref pos, out _subject, out _body);

					int posOfEndMsg = pos;
					UInt64 signatureLength = decryptedData.ReadVarInt(ref pos);
					byte[] signature = decryptedData.ReadBytes(ref pos, (int) signatureLength);

					byte[] data = new byte[posOfEndMsg];
					Buffer.BlockCopy(decryptedData, 0, data, 0, posOfEndMsg);

					if (data.ECDSAVerify(encryptionKey.SigningKey, signature))
						Status = Status.Valid;
				}
			}
			catch
			{
				Status = Status.Invalid;
			}
		}

		private byte[] _inventoryVector;

		[PrimaryKey]
		[MaxLength(64)]
		public string InventoryVectorHex
		{
			get 
			{ 				
				return _inventoryVector != null ? _inventoryVector.ToHex(false)  : GetPayload().InventoryVector.ToHex(false);
			}
			set
			{
				_inventoryVector = value.HexToBytes();
				_payload = Payload.Get(_bm.DB, value);
			}
		}

		private Payload _payload;
		private object _lock4GetPayload = new object();

		private Payload GetPayload()
		{
			lock(_lock4GetPayload)
			if (_payload == null)
			{
				if (_version == 2)
				{
					PrivateKey privkey = PrivateKey.GetPrivateKey(_bm.DB, Key);
					if (privkey == null) throw new Exception("PrivateKey not found");

					MemoryStream payload = new MemoryStream(1000 + Subject.Length + Body.Length); // TODO realy 1000?
					Random rnd = new Random();
					ulong dt = DateTime.UtcNow.ToUnix() + (ulong)rnd.Next(600) - 300;

					payload.Write(dt);
					payload.WriteVarInt(Version);
					payload.WriteVarInt(Stream);

					MemoryStream dataToEncrypt = new MemoryStream(1000 + Subject.Length + Body.Length); // TODO realy 1000?
					dataToEncrypt.WriteVarInt(Version);

					byte[] publicAddress = privkey.GetPayload4Broadcast();
					dataToEncrypt.Write(publicAddress, 0, publicAddress.Length);
					
					Byte encodingType = (byte)EncodingType;
					dataToEncrypt.Write(encodingType);
					dataToEncrypt.WriteVarStr("Subject:" + Subject + "\nBody:" + Body);

					byte[] signature = privkey.Sign(dataToEncrypt.ToArray());

					dataToEncrypt.WriteVarInt((UInt64)signature.Length);
					dataToEncrypt.Write(signature, 0, signature.Length);

					var privEncryptionKey = privkey.Sha512VersionStreamHashFirst32();
					var pubEncryptionKey = ECDSA.PointMult(privEncryptionKey);

					byte[] bytesToEncrypt = dataToEncrypt.ToArray();
					byte[] encrypt = ECDSA.Encrypt(bytesToEncrypt, pubEncryptionKey);

					payload.Write(encrypt, 0, encrypt.Length);

					_payload = new Payload("broadcast", ProofOfWork.AddPow(payload.ToArray()));
				}
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

		private DateTime _receivedTime = DateTime.UtcNow;
	
		public DateTime ReceivedTime
		{
			get { return _receivedTime; }
			set { _receivedTime = value; }
		}

		public Status Status { get; set; }

		public delegate void EventHandler(Broadcast broadcast);

		public override string ToString()
		{
			return "Broadcast from " + Address() + " InventoryVector " + InventoryVectorHex;
		}

		public string Address()
		{
			if (Status == Status.Valid)
				return Key;
			return "Message don't valid. Version=" + Version;
		}

		public Task<int> SaveAsync(SQLiteAsyncConnection db)
		{
			return Status==Status.Valid ? db.InsertOrReplaceAsync(this) : null;
		}

		internal void Send()
		{
			Payload p = GetPayload();
			p.SaveAsync(_bm);
		}
	}
}
