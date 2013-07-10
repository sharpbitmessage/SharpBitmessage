using System;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using SQLite;

namespace bitmessage.network
{
	[Table("inventory")]
	public class Payload : ICanBeSent, IComparable 
	{
		public const int MaximumAgeOfAnObjectThatIAmWillingToAccept = 216000;
		public const int LengthOfTimeToHoldOnToAllPubkeys = 2419200; //Equals 4 weeks. You could make this longer if you want but making it shorter would not be advisable because there is a very small possibility that it could keep you from obtaining a needed pubkey for a period of time.

		private byte[] _hash;
		private MsgType _msgType;
		private string _сommand;
		private DateTime _receivedTime = DateTime.UtcNow;
		private ulong _stream = 1;

		#region for DB

		[Ignore]
		public byte[] InventoryVector
		{
			get
			{
				if (_hash == null)
				{
					_hash = new byte[32];
					using (var sha512 = new SHA512Managed())
						Buffer.BlockCopy(sha512.ComputeHash( sha512.ComputeHash(SentData) ), 0, _hash, 0, 32);
				}
				return _hash;
			}
			set { _hash = value; }
		}

		[PrimaryKey]
		[MaxLength(64)]
		public string InventoryVectorHex
		{
			get { return InventoryVector.ToHex(false); }
			set { InventoryVector = value.HexToBytes(); }
		}

		public byte[] SentData { get; set; }

		[MaxLength(30)]
		public string Command
		{
			get { return _сommand; }
			set
			{
				_сommand = value;
				if (!Enum.TryParse(_сommand, true, out _msgType))
					_msgType = MsgType.NotKnown;
			}
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

		public DateTime ReceivedTime
		{
			get { return _receivedTime; }
			set { _receivedTime = value; }
		}

		public void SaveAsync(Bitmessage bm)
		{
			if (SentData == null)
				throw new Exception("Payload.SaveAsync SentData == null");
			if (String.IsNullOrEmpty(Command) || (Command.Length > 30))
				throw new Exception("Payload.SaveAsync Command incorrect =" + Command);
			if (InventoryVector == null)
				throw new Exception("Payload.SaveAsync InventoryVector==null");
			if (InventoryVector.Length != 32)
				throw new Exception("Payload.SaveAsync InventoryVector.Length != 32");

			if(bm.MemoryInventory.Insert(InventoryVector))
				bm.DB.InsertOrReplaceAsync(this);

			bm.OnNewPayload(this);
		}

		public Payload() {}

		#endregion for DB

		[Ignore]
		public MsgType MsgType
		{
			get { return _msgType; }
			set { _msgType = value; throw new NotImplementedException(); } // need change _сommand
		}
		
		public Payload(string msgType, byte[] data)
		{
			SentData = data;
			Command = msgType;
		}

		[Ignore]
		public int FirstByteAfterTime
		{
			get
			{
				int pos = 8;
				UInt32 embeddedTime32 = SentData.ReadUInt32(ref pos);
				if (embeddedTime32 == 0)
					return 16;
				return 12;
			}
		}

		[Ignore]
		public UInt64 EmbeddedTime
		{
			get
			{
				int pos = 8;
				UInt32 embeddedTime32 = SentData.ReadUInt32(ref pos);

				if (embeddedTime32==0)
				{
					pos = 8;
					UInt64 embeddedTime64 = SentData.ReadUInt64(ref pos);
					return embeddedTime64;
				}
				return embeddedTime32;
			}
		}

		//public byte[] Sha512   
		//{
		//	get
		//	{
		//		if (SentData==null)
		//			return !
		//		using (var sha512 = new SHA512Managed())
		//			return sha512.ComputeHash(SentData);
		//	}
		//}

		[Ignore]
		public int Length { get { return SentData.Length; } }

		[Ignore]
		public bool IsEmbeddedTimeValid
		{
			get
			{
                //Debug.WriteLine("embeddedTime = " + EmbeddedTime.FromUnix());

				if (EmbeddedTime > DateTime.UtcNow.ToUnix() + 10800)
					return false;

				if (MsgType == MsgType.Broadcast)
				{
					if (EmbeddedTime < DateTime.UtcNow.ToUnix() - MaximumAgeOfAnObjectThatIAmWillingToAccept)
						return false;
					return true;
				}
				if (MsgType == MsgType.Pubkey)
				{
					if (EmbeddedTime < DateTime.UtcNow.ToUnix() - LengthOfTimeToHoldOnToAllPubkeys)
						return false;
					return true;
				}
				return false;
			}
		}

		[Ignore]
		public bool IsLengthValid
		{
			get
			{
				if (MsgType == MsgType.Broadcast)
				{
					if (Length < 180)
						return false;
					return true;
				}
				if (MsgType == MsgType.Pubkey)
				{
					if ((Length < 146) || (Length > 600))
						return false;
					return true;
				}
				return false;
			}
		}

		[Ignore]
		public bool IsValid
		{
			get
			{
				if ((MsgType == MsgType.Broadcast) || (MsgType == MsgType.Pubkey))
					return (IsProofOfWorkSufficient && IsLengthValid && IsEmbeddedTimeValid);
				return true;
			}
		}

		[Ignore]
		public bool IsProofOfWorkSufficient
		{
			get
			{
				int pos;
				byte[] resultHash;
				using (var sha512 = new SHA512Managed())
				{
					var buff = new byte[8 + 512 / 8];
					Buffer.BlockCopy(SentData, 0, buff, 0, 8);

					pos = 8;
					byte[] initialHash = sha512.ComputeHash(SentData.ReadBytes(ref pos, SentData.Length - pos));
					Buffer.BlockCopy(initialHash, 0, buff, 8, initialHash.Length);
					resultHash = sha512.ComputeHash(sha512.ComputeHash(buff));
				}

				pos = 0;
				UInt64 pow = resultHash.ReadUInt64(ref pos);
				UInt64 target = ProofOfWork.Target(SentData.Length);

				//Debug.WriteLine("ProofOfWork=" + (pow < target) + " pow=" + pow + " target=" + target + " lendth=" + SentData.Length);

                return (pow < target);
			}
		}

		public delegate void EventHandler(Payload payload);

		#region IComparable

		public int CompareTo(object obj)
		{
			if (obj == null) return 1;
			var otherPayload = obj as Payload;
			if (otherPayload == null)
				throw new ArgumentException("Object is not a Payload");
			byte[] otherHash = otherPayload.InventoryVector;
			byte[] myHash = InventoryVector;
			for (int i = 0; i < 32; i++)
			{
				int compare = myHash[i].CompareTo(otherHash[i]);
				if (compare != 0) return compare;
			}
			return 0;
		}

		#endregion IComparable

		public static Payload Get(SQLiteAsyncConnection conn, string inventoryVectorHex)
		{
			var query = conn.Table<Payload>().Where(inv => inv.InventoryVectorHex == inventoryVectorHex);
			return query.FirstOrDefaultAsync().Result;
		}

		internal static void SendAsync(NodeConnection nodeConnection, string inventoryVectorHex)
		{
			SQLiteAsyncConnection conn = nodeConnection.Bitmessage.DB;
			var query = conn.Table<Payload>().Where(inv => inv.InventoryVectorHex == inventoryVectorHex);
			query.FirstOrDefaultAsync().ContinueWith(t =>
				                                         {
					                                         if (t.Result != null)
						                                         nodeConnection.Send(t.Result);
				                                         });
		}

		public override string ToString()
		{
			return "Payload " + Command + " InventoryVectorHex=" + InventoryVectorHex;
		}
	}
}