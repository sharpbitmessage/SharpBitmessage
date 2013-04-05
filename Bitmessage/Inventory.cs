using System;
using System.Linq;
using SQLite;
using System.Collections.Generic;

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

		public void Save(Inv inv)
		{
			if(inv.Hash.Length!=32) throw new Exception("inv.Hash.Length!=32");
			lock (_items)
			{
				if (Get(inv.Hash) == null)
				{
					_items.Add(inv);
					inv.SaveAsync(_bitmessage.GetConnection());
				}
				else
				{
					inv.SaveAsync(_bitmessage.GetConnection());
				}
			}
		}
	}

	[Table("inventory")]
	public class Inv
	{
		[PrimaryKey]
		public byte[] Hash { get; set; }
		public int ObjectType { get; set; }
		public int StreamNumber { get; set; }
		public byte[] Payload { get; set; }
		public int ReceivedTime { get; set; }

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