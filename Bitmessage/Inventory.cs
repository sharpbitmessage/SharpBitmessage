using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using bitmessage.network;

namespace bitmessage
{
	public class MemoryInventory : IEnumerable<byte[]>
	{
		private readonly List<byte[]> _items;

		public MemoryInventory(int capacity = 3000)
		{
			_items = new List<byte[]>(capacity);
		}

		public MemoryInventory(SQLiteAsyncConnection conn)
		{
			Task<int> task = conn.Table<Payload>().CountAsync();
			_items = new List<byte[]>(Math.Max(task.Result, 3000));

			var inventory = conn.Table<Payload>().ToListAsync();

			foreach (Payload payload in inventory.Result)
				_items.Add(payload.InventoryVector);
		}

		public bool Exists(byte[] hash)
		{
			lock (_items)
				return _items.Exists(bytes => bytes.SequenceEqual(hash));
		}

		public bool Insert(byte[] hash)
		{
			if (hash.Length != 32)
				throw new ArgumentException("hash.Length!=32");
			lock (_items)
				if (!Exists(hash))
				{
					_items.Add(hash);
					return true;
				}
			return false;
		}

		public int Count
		{
			get { return _items.Count; }
		}

		#region IEnumerator

		public IEnumerator<byte[]> GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion IEnumerator
	}
}