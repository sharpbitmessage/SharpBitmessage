using System.Collections.Generic;
using System.IO;

namespace bitmessage.network
{
	public class GetData : ICanBeSent
	{
		internal readonly MemoryInventory Inventory = new MemoryInventory();

		public GetData(IEnumerable<byte[]> inventoryVectors)
		{
			foreach (byte[] inventoryVector in inventoryVectors)
				Inventory.Insert(inventoryVector);			
		}

		public GetData(byte[] payload)
		{
			int pos = 0;
			int brL = payload.Length;
			int count = (int) payload.ReadVarInt(ref pos);
			for (int i = 0; (i < count) && (brL > pos); ++i)
				Inventory.Insert(payload.ReadBytes(ref pos, 32));
		}

		public string Сommand
		{
			get { return "getdata"; }
		}

		public byte[] SentData
		{
			get
			{
				MemoryStream result = new MemoryStream(9 + Inventory.Count * 32);
				result.WriteVarInt((ulong) Inventory.Count);
				foreach(byte[] item in Inventory)
					result.Write(item, 0, 32);
				return result.ToArray();
			}
		}
	}
}