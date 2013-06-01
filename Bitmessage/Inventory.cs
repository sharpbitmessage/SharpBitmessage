using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using bitmessage.network;

namespace bitmessage
{
	internal class MemoryInventory : IEnumerable<byte[]>
	{
		private readonly List<byte[]> _items = new List<byte[]>(3000);
		public bool Exists(byte[] hash)
		{
			lock (_items)
				return _items.Exists(bytes => bytes.SequenceEqual(hash));
		}

		public void Insert(byte[] hash)
		{
			if (hash.Length != 32)
				throw new ArgumentException("hash.Length!=32");
			lock (_items)
				if (!Exists(hash))
					_items.Add(hash);
		}

		public int Count
		{
			get { return _items.Count; }
		}

		public IEnumerator<byte[]> GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	//internal class Inventory
	//{
	//	private readonly Bitmessage _bitmessage;
	//	public Inventory (Bitmessage bitmessage)
	//	{
	//		_bitmessage = bitmessage;
	//	}

	//	readonly List<Payload> _items = new List<Payload>(5000);
	//	public bool Exists(byte[] item)
	//	{
	//		lock (_items)
	//		{
	//			if (_items.Count == 0)
	//			{
	//				var db = _bitmessage.GetConnection();
	//				var task = db.Table<Payload>().ToListAsync();
	//				task.Wait();
	//				foreach (Payload payload in task.Result)
	//					_items.Add(payload);					
	//			}
	//			foreach (Payload payload in _items)
	//				if (payload.InventoryVector.SequenceEqual(item))
	//					return true;
	//		}
	//		return false;
	//	}

	//	internal void Insert(MsgType msgType, Payload payload)
	//	{
	//		if (!Exists(payload.InventoryVector))
	//			if (msgType == MsgType.Broadcast)
	//			{
	//				_items.Add(payload);
	//				payload.SaveAsync(_bitmessage);
	//			}
	//	}
	//}
}