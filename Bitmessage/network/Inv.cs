using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace bitmessage.network
{
	public class Inv : ICanBeSent, IEnumerable<byte[]>
	{
		private readonly MemoryInventory _inventory;

		public Inv(byte[] payload)
		{
			if (payload.Length<32)
				throw new Exception("incorrect length payload for new Inv");

			if (payload.Length == 32)
			{
				_inventory = new MemoryInventory(1);
				_inventory.Insert(payload);
			}
			else
			{
				int pos = 0;
				int brL = payload.Length;
				int count = (int)payload.ReadVarInt(ref pos);
				_inventory = new MemoryInventory(count);
				for (int i = 0; (i < count) && (brL > pos); ++i)
					_inventory.Insert(payload.ReadBytes(ref pos, 32));
			}
		}

		public Inv(MemoryInventory inventory)
		{
			_inventory = inventory;
		}

		public int Count { get { return _inventory.Count; } }

		public string Command
		{
			get { return "inv"; }
		}

		private byte[] _sendData;
		public byte[] SentData
		{
			get
			{
				if (_sendData == null)
				{
					MemoryStream payloadBuff = new MemoryStream(10 + 32*_inventory.Count);
					payloadBuff.WriteVarInt((ulong)_inventory.Count);
					foreach (byte[] inventoryVector in _inventory)
						payloadBuff.Write(inventoryVector, 0, 32);
					_sendData = payloadBuff.ToArray();
				}
				return _sendData;
			}
		}

		#region IEnumerator

		public IEnumerator<byte[]> GetEnumerator()
		{
			return _inventory.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion IEnumerator

	}
}

