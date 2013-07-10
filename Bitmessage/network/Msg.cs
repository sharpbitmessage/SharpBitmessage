using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using bitmessage.Crypto;

namespace bitmessage.network
{
	public class Msg
	{
		private readonly Bitmessage _bm;
		private string _subject;
		private string _body;
		private EncodingType _encodingType = EncodingType.Simple;
		private ulong _version = 1;
		private ulong _stream = 1;

		public Msg() {}

		public Msg(Bitmessage bm)
		{
			_bm = bm;
		}

		public Msg(Bitmessage bm, Payload payload)
		{
			Status = Status.Invalid;
			try
			{
				int pos = payload.FirstByteAfterTime;
				_inventoryVector = payload.InventoryVector;

				Stream = payload.SentData.ReadVarInt(ref pos);

				byte[] encrypted = payload.SentData.ReadBytes(ref pos, payload.Length - pos);

				// TODO Check ask data

				byte[] decryptedData = null;
				PrivateKey myEncryptionKey = null;

				foreach (PrivateKey myKey in bm.ListMyAddresses())
				{
					if (myKey.Stream != _stream) continue;
					try
					{
						decryptedData = myKey.DecryptAES256CBC4Msg(encrypted);
						myEncryptionKey = myKey;
					}
						// ReSharper disable EmptyGeneralCatchClause
					catch
					{
					} // ReSharper restore EmptyGeneralCatchClause

					if (decryptedData != null)
						break;
				}

				if ((decryptedData == null) || (myEncryptionKey == null))
				{
					Status = Status.Encrypted;
					return;
				}

				pos = 0;

				Version = decryptedData.ReadVarInt(ref pos);
				var senderKey = new Pubkey(decryptedData, ref pos);

				if (!decryptedData.ReadBytes(ref pos, 20).SequenceEqual(myEncryptionKey.Hash))
					//print 'The original sender of this message did not send it to you. Someone is attempting a Surreptitious Forwarding Attack.'
					//print 'See: http://world.std.com/~dtd/sign_encrypt/sign_encrypt7.html'
					//print 'your toRipe:', toRipe.encode('hex')
					//print 'embedded destination toRipe:', decryptedData[readPosition:readPosition + 20].encode('hex')
					return;

				KeyTo = myEncryptionKey.Name;
				KeyFrom = senderKey.Name;

				EncodingType = (EncodingType) decryptedData.ReadVarInt(ref pos);
				decryptedData.ReadVarStrSubjectAndBody(ref pos, out _subject, out _body);

				UInt64 askDataLength = decryptedData.ReadVarInt(ref pos);
				_askData = decryptedData.ReadBytes(ref pos, (int) askDataLength);

				int posOfEndMsg = pos;
				UInt64 signatureLength = decryptedData.ReadVarInt(ref pos);
				byte[] signature = decryptedData.ReadBytes(ref pos, (int) signatureLength);

				var data = new byte[posOfEndMsg];
				Buffer.BlockCopy(decryptedData, 0, data, 0, posOfEndMsg);

				if (data.ECDSAVerify(senderKey.SigningKey, signature))
				{
					Status = Status.Valid;
					senderKey.SaveAsync(bm.DB);
				}
			}
			catch
			{
				Status = Status.Invalid;
			}
		}

		private byte[] _askData;

		private bool isAckDataValid(byte[] ackData)
		{
			if (ackData == null)
				return false;
			if (ackData.Length < 24)
				//'The length of ackData is unreasonably short. Not sending ackData.'
				return false;
			int pos = 0;

			byte[] magic = ackData.ReadBytes(ref pos, 4);
			if (! magic.SequenceEqual(Header.Magic))
				//print'Ackdata magic bytes were wrong. Not sending ackData.'
				return false;

			string command = Header.ReadHeaderCommand(ackData.ReadBytes(ref pos, 12));
			if ((command!="getpubkey")
				&&(command!="pubkey")
				&&(command!="msg")
				&&(command!="broadcast"))
				return false;
				
			int ackDataPayloadLength  = Header.ReadLength( ackData.ReadBytes(ref pos, 4)  );
			if (ackData.Length-24 != ackDataPayloadLength)
//			'ackData payload length doesn\'t match the payload length specified in the header. Not sending ackdata.'
				return false;

			// TODO Check checksumm and time ?

			return true;
		}

		public Payload PayloadOfAskData()
		{
				if (_askData==null)
				{
					var payload = new MemoryStream();

					var rnd = new Random();
					ulong dt = DateTime.UtcNow.ToUnix() + (ulong)rnd.Next(600) - 300;

					payload.Write(dt);
					payload.WriteVarInt(Stream);

					var rndMsg = new byte[12 + rnd.Next(500)];
					rnd.NextBytes(rndMsg);

					payload.Write(rndMsg, 0, rndMsg.Length);

					_askData = ProofOfWork.AddPow(payload.ToArray());
				}
			return new Payload("msg", _askData);
		}
		
		public string AskDataHex
		{
			get
			{
				return _askData.ToHex(false);
			}
			set
			{
				_askData = value.HexToBytes();
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
		private readonly object _lock4GetPayload = new object();

		private Payload GetPayload()
		{
			lock (_lock4GetPayload)
				if (_payload == null)
				{
					PrivateKey myPrivkeyFrom = PrivateKey.GetPrivateKey(_bm.DB, KeyFrom);
					if (myPrivkeyFrom == null) throw new Exception("PrivateKey not found");

					Pubkey pubkeyTo = Pubkey.Find(_bm.DB, KeyTo); // TODO Получать ключ, если его ещё нет
					if (pubkeyTo == null) throw new Exception("Pubkey not found");

					var payload = new MemoryStream(1000 + Subject.Length + Body.Length); // TODO realy 1000?
					var rnd = new Random();
					ulong dt = DateTime.UtcNow.ToUnix() + (ulong) rnd.Next(600) - 300;

					payload.Write(dt);
					payload.WriteVarInt(Stream);

					var dataToEncrypt = new MemoryStream(1000 + Subject.Length + Body.Length); // TODO realy 1000?
					dataToEncrypt.WriteVarInt(Version);

					byte[] publicAddress = myPrivkeyFrom.GetPayload4Broadcast();
					dataToEncrypt.Write(publicAddress, 0, publicAddress.Length);

					dataToEncrypt.Write(pubkeyTo.Hash, 0, 20);

					var encodingType = (byte) EncodingType;
					dataToEncrypt.Write(encodingType);
					dataToEncrypt.WriteVarStr("Subject:" + Subject + "\nBody:" + Body);

					byte[] askMsg = PayloadOfAskData().GetFullMsg();
					dataToEncrypt.WriteVarInt((UInt64)askMsg.Length);
					dataToEncrypt.Write(askMsg, 0, askMsg.Length);

					byte[] signature = myPrivkeyFrom.Sign(dataToEncrypt.ToArray());

                    //Debug.WriteLine("data=" + dataToEncrypt.ToArray().ToHex());
                    //Debug.WriteLine("SigningKey=" + myPrivkeyFrom.SigningKey.ToHex());
                    //Debug.WriteLine("signature=" + signature.ToHex());
						
					dataToEncrypt.WriteVarInt((UInt64)signature.Length);
					dataToEncrypt.Write(signature, 0, signature.Length);

					byte[] bytesToEncrypt = dataToEncrypt.ToArray();
					byte[] encrypt = ECDSA.Encrypt(bytesToEncrypt, pubkeyTo.EncryptionKey);

					payload.Write(encrypt, 0, encrypt.Length);

					_payload = new Payload("msg", ProofOfWork.AddPow(payload.ToArray()));
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

		public string KeyFrom { get; set; }
		public string KeyTo   { get; set; }
	
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

		public delegate void EventHandler(Msg msg);

		public override string ToString()
		{
			if (Status == Status.Valid)
				return "Msg InventoryVector=" + InventoryVectorHex + " from " + KeyFrom;
			return "Msg InventoryVector=" + InventoryVectorHex;
		}

		public Task<int> SaveAsync(SQLiteAsyncConnection db)
		{
			return Status == Status.Valid ? db.InsertOrReplaceAsync(this) : null;
		}

		internal void Send()
		{
			Payload p = GetPayload();
			p.SaveAsync(_bm);
		}
	}
}
