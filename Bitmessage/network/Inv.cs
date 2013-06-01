using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bitmessage.network
{
	public class Inv : ICanBeSent
	{
		internal MemoryInventory Inventory = new MemoryInventory();

		public Inv(byte[] payload)
		{
			if (payload.Length == 32)
				Inventory.Insert(payload);
			else
			{
				int pos = 0;
				int brL = payload.Length;
				int count = (int) payload.ReadVarInt(ref pos);
				for (int i = 0; (i < count) && (brL > pos); ++i)
					Inventory.Insert(payload.ReadBytes(ref pos, 32));
			}
		}

		public string Сommand
		{
			get { return "inv"; }
		}

		public byte[] SentData
		{
			get
			{
				MemoryStream payloadBuff = new MemoryStream(10 + 32*Inventory.Count);
				payloadBuff.WriteVarInt((ulong) Inventory.Count);
				foreach (byte[] inventoryVector in Inventory)
					payloadBuff.Write(inventoryVector, 0, 32);
				
				return payloadBuff.GetBuffer();
			}
		}
	}
}

