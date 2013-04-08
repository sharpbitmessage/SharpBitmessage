using System;
using System.Linq;
using SQLite;
using System.Collections.Generic;
using bitmessage.network;

namespace bitmessage
{
	internal class Inventory
	{
		private readonly Bitmessage _bitmessage;
		public Inventory (Bitmessage bitmessage)
		{
			_bitmessage = bitmessage;
		}

		readonly List<Inv> _items = new List<Inv>(5000);
		public Inv Get(byte[] item)
		{
			lock (_items)
			{
				if (_items.Count == 0)
				{
					var db = _bitmessage.GetConnection();
					var task = db.Table<Inv>().ToListAsync();
					task.Wait();
					foreach (Inv inv in task.Result)
						_items.Add(inv);					
				}
				foreach (Inv inv in _items)
				{
					if (inv.HaveHash(item))
						return inv;
				}
			}
			return null;
		}

		//public void Save(Inv inv)
		//{
		//	if(inv.Hash.Length!=32) throw new Exception("inv.Hash.Length!=32");
		//	lock (_items)
		//	{
		//		if (Get(inv.Hash) == null)
		//		{
		//			_items.Add(inv);
		//			inv.SaveAsync(_bitmessage.GetConnection());
		//		}
		//		else
		//		{
		//			inv.SaveAsync(_bitmessage.GetConnection());
		//		}
		//	}
		//}

		internal byte[] Insert(MsgType msgType, byte[] payload)
		{
			byte[] invHash = payload.CalculateInventoryHash();
			Inv inv = Get(invHash);
			if (inv == null)
			{
				inv = new Inv
					      {
						      Hash = invHash,
						      ObjectType = msgType,
						      Payload = payload,
						      StreamNumber = 1,
						      ReceivedTime = DateTime.UtcNow.ToUnix(),
							  EmbeddedTime = payload.GetEmbeddedTime()
					      };

				if (msgType == MsgType.Broadcast)
				{
					Broadcast broadcast = new Broadcast(inv);


					_items.Add(inv);
					inv.SaveAsync(_bitmessage.GetConnection());
				}
			}

			return inv.Hash;
		}
	}

	[Table("inventory")]
	public class Inv
	{
		[PrimaryKey]
		public byte[] Hash { get; set; }
		public MsgType ObjectType { get; set; }
		public int StreamNumber { get; set; }
		public byte[] Payload { get; set; }
		public ulong ReceivedTime { get; set; }
		public ulong EmbeddedTime { get; set; }

		public void SaveAsync(SQLiteAsyncConnection db)
		{
			db.InsertAsync(this);
		}

		public bool HaveHash(byte[] h)
		{
			if ((Hash == null) || (h == null)) return false;
			return !h.Where((t, i) => t != Hash[i]).Any();
		}
	}
}