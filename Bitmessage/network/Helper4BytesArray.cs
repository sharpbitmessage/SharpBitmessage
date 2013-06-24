using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace bitmessage.network
{
	public static class Helper4BytesArray
	{
		public static byte[] ReverseIfNeed(this byte[] inArray)
		{
			if (!BitConverter.IsLittleEndian)
				return inArray;

			Array.Reverse(inArray);
			return inArray;
		}

		public static string ToHex(this byte[] ba, bool addPrefix = true)   
		{
			StringBuilder sb = new StringBuilder(2 + ba.Length*2);
			if (addPrefix) sb.Append("0x");
			foreach (byte b in ba)
				sb.AppendFormat("{0:x2}", b);
			return sb.ToString();
		}
		
		public static byte[] HexToBytes(this string hexString)
		{
			if (hexString.Length % 2 != 0)
				throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "The binary key cannot have an odd number of digits: {0}", hexString));

			int startFrom = hexString.StartsWith("0x") ? 2 : 0;

			byte[] hexAsBytes = new byte[(hexString.Length - startFrom)/2];
			for (int index = startFrom; index < hexAsBytes.Length; index++)
			{
				string byteValue = hexString.Substring(index * 2, 2);
				hexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			}

			return hexAsBytes;
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

		public static void Write(this MemoryStream ms, UInt64 data)
		{
			byte[] tmp = BitConverter.GetBytes(data);
			tmp.ReverseIfNeed();
			ms.Write(tmp, 0, tmp.Length);
		}

		public static void Write(this MemoryStream ms, UInt32 data)
		{
			byte[] tmp = BitConverter.GetBytes(data);
			tmp.ReverseIfNeed();
			ms.Write(tmp, 0, tmp.Length);
		}

		public static void Write(this MemoryStream ms, byte data)
		{
			byte[] tmp = new[] {data};
			ms.Write(tmp, 0, tmp.Length);
		}

		public static byte[] ReadBytes(this byte[] ba, ref int pos, int count)
		{
			byte[] tmp = new byte[count];
			Buffer.BlockCopy(ba, pos, tmp, 0, count);
			pos += count;
			return tmp;
		}

		public static byte[] Concatenate(this byte[] a, byte[] b)
		{
			byte[] buff = new byte[a.Length + b.Length];
			Buffer.BlockCopy(a, 0, buff, 0, a.Length);
			Buffer.BlockCopy(b, 0, buff, a.Length, b.Length);
			return buff;
		}

		public static byte[] Concatenate(this byte a, byte[] b)
		{
			byte[] buff = new byte[1 + b.Length];
			buff[0] = a;
			Buffer.BlockCopy(b, 0, buff, 1, b.Length);
			return buff;
		}
	}

	public class ByteArrayComparer : IEqualityComparer<byte[]>
	{
		public bool Equals(byte[] left, byte[] right)
		{
			if (left == null || right == null)
			{
				return left == right;
			}
			return left.SequenceEqual(right);
		}
		public int GetHashCode(byte[] key)
		{
			if (key == null)
				throw new ArgumentNullException("key");
			return key.Sum(b => b);
		}
	}
}