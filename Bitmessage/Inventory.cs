using System;
using System.Collections.Generic;
using System.Linq;
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

		readonly List<Payload> _items = new List<Payload>(5000);
		public bool Exists(byte[] item)
		{
			lock (_items)
			{
				if (_items.Count == 0)
				{
					var db = _bitmessage.GetConnection();
					var task = db.Table<Payload>().ToListAsync();
					task.Wait();
					foreach (Payload payload in task.Result)
						_items.Add(payload);					
				}
				foreach (Payload payload in _items)
					if (payload.Hash.SequenceEqual(item))
						return true;
			}
			return false;
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

		internal void Insert(MsgType msgType, Payload payload)
		{
			if (!Exists(payload.Hash))
			{
				if (msgType == MsgType.Broadcast)
				{
					_items.Add(payload);
					payload.SaveAsync(_bitmessage.GetConnection());
				}
			}
		}
	}
}