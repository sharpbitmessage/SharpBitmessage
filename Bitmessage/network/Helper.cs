using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace bitmessage.network
{
	public static class Helper
	{
		public static UInt64 NextUInt64(this Random rnd)
		{
			var buffer = new byte[sizeof (UInt64)];
			rnd.NextBytes(buffer);
			return BitConverter.ToUInt64(buffer, 0);
		}

		#region Unix DateTime

		public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

		public static UInt64 ToUnix(this DateTime dt)
		{
			UInt64 result = (UInt64) (dt - Epoch).TotalSeconds;
			return result;
		}

		public static DateTime FromUnix(this UInt64 unix)
		{
			return Epoch.AddSeconds(unix);
		}

		#endregion Unix DateTime

		#region VarInt VarStr VarIntList

		public static UInt64 ReadVarInt(this byte[] br, ref int pos)
		{
			byte firstByte = br[pos];
			pos++;

			if (firstByte < 253)
				return firstByte;
			if (firstByte == 253)
				return br.ReadUInt16(ref pos);
			if (firstByte == 254)
				return br.ReadUInt32(ref pos);
			if (firstByte == 255)
				return br.ReadUInt64(ref pos);
			throw new Exception("WTF");
		}

		public static void WriteVarInt(this MemoryStream ms, UInt64 data)
		{
			byte[] bytes = data.VarIntToBytes();
			ms.Write(bytes, 0, bytes.Length);
		}

		public static byte[] VarIntToBytes(this UInt64 i)
		{
			byte[] result;
			if (i < 253)
			{
				result = new byte[1];
				result[0] = (Byte) i;
			}
			else if (i <= 0xffff)
			{
				result = new byte[3];
				result[0] = 253;
				Buffer.BlockCopy(BitConverter.GetBytes((UInt16) i).ReverseIfNeed(), 0, result, 1, 2);
			}
			else if (i <= 0xffffffff)
			{
				result = new byte[5];
				result[0] = 254;
				Buffer.BlockCopy(BitConverter.GetBytes((UInt32)i).ReverseIfNeed(), 0, result, 1, 4);
			}
			else
			{
				result = new byte[9];
				result[0] = 255;
				Buffer.BlockCopy(BitConverter.GetBytes(i).ReverseIfNeed(), 0, result, 1, 8);
			}
			return result;
		}

		public static string ReadVarStr(this byte[] br, ref int pos)
		{
			int l = (int)br.ReadVarInt(ref pos);
			byte[] bytes = br.ReadBytes(ref pos, l);
			Char[] chars = Encoding.UTF8.GetChars(bytes);
			return new String(chars);
		}

		public static void WriteVarStr(this MemoryStream ms, string data)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(data);
			ms.WriteVarInt((UInt64) bytes.Length);
			ms.Write(bytes, 0, bytes.Length);
		}

		public static void ReadVarStrSubjectAndBody(this byte[] data, ref int pos, out string subject, out string body)
		{
			subject = null;
			body = null;

			int l = (int) data.ReadVarInt(ref pos);
			int startMessage = pos;

			for (int i = pos; i < pos + l; ++i)
				if (data[i] == 0x0A) // find first /n
				{
					byte[] bytes = data.ReadBytes(ref pos, i - pos);
					Char[] chars = Encoding.UTF8.GetChars(bytes);
					subject = new String(chars);

					++pos;
					bytes = data.ReadBytes(ref pos, l - (pos - startMessage));
					chars = Encoding.UTF8.GetChars(bytes);
					body = new String(chars);

					break;
				}
		}

		public static void WriteVarStr(this BinaryWriter bw, string s)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			bw.Write(((UInt64) bytes.Length).VarIntToBytes());
			bw.Write(bytes);
		}

		public static List<UInt64> ReadVarIntList(this byte[] br, ref int pos)
		{
			UInt64 l = br.ReadVarInt(ref pos);
			List<UInt64> result = new List<UInt64>((int) l);
			for (int i = 0; i < (int) l; ++i)
				result.Add(br.ReadVarInt(ref pos));
			return result;
		}

		public static void WriteVarIntList(this BinaryWriter bw, List<UInt64> list)
		{
			bw.Write(((UInt64)list.Count).VarIntToBytes());

			foreach (UInt64 item in list)
				bw.Write(item.VarIntToBytes());
		}

		#endregion VarInt VarStr VarIntList		
	}
}
