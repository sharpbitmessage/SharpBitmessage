using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bitmessage.network
{
	public static class Helper4BytesArray
	{
		public static byte[] ReverseIfNeed(this byte[] inArray)
		{
			if (!BitConverter.IsLittleEndian)
				return inArray;

			int highCtr = inArray.Length - 1;

			for (int ctr = 0; ctr < inArray.Length / 2; ctr++)
			{
				byte temp = inArray[ctr];
				inArray[ctr] = inArray[highCtr];
				inArray[highCtr] = temp;
				highCtr -= 1;
			}
			return inArray;
		}

		public static string ToHex(this byte[] ba)
		{
			StringBuilder sb = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
				sb.AppendFormat("{0:x2}", b);
			return sb.ToString();
		}

		public static Int32 ReadInt32(this byte[] ba, ref int pos)
		{
			byte[] tmp = new byte[4];
			Buffer.BlockCopy(ba, pos, tmp, 0, 4);
			pos += 4;
			return BitConverter.ToInt32(tmp.ReverseIfNeed(), 0);
		}

		public static UInt16 ReadUInt16(this byte[] ba, ref int pos)
		{
			byte[] tmp = new byte[2];
			Buffer.BlockCopy(ba, pos, tmp, 0, 2);
			pos += 2;
			return BitConverter.ToUInt16(tmp.ReverseIfNeed(), 0);
		}

		public static UInt32 ReadUInt32(this byte[] ba, ref int pos)
		{
			byte[] tmp = new byte[4];
			Buffer.BlockCopy(ba, pos, tmp, 0, 4);
			pos += 4;
			return BitConverter.ToUInt32(tmp.ReverseIfNeed(), 0);
		}

		public static UInt64 ReadUInt64(this byte[] ba, ref int pos)
		{
			byte[] tmp = new byte[8];
			Buffer.BlockCopy(ba, pos, tmp, 0, 8);
			pos += 8;
			return BitConverter.ToUInt64(tmp.ReverseIfNeed(), 0);
		}

		public static byte[] ReadBytes(this byte[] ba, ref int pos, int count)
		{
			byte[] tmp = new byte[count];
			Buffer.BlockCopy(ba, pos, tmp, 0, count);
			pos += count;
			return tmp;
		}		
	}
}
