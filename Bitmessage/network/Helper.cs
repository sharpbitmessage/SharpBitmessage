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

		private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

		public static UInt64 ToUnix(this DateTime dt)
		{
			return (UInt64) (dt - Epoch).TotalSeconds;
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
			Char[] chars = Encoding.ASCII.GetChars(bytes);
			return new String(chars);
		}

		public static void WriteVarStr(this MemoryStream ms, string data)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(data);
			ms.WriteVarInt((UInt64) bytes.Length);
			ms.Write(bytes, 0, bytes.Length);
		}

		public static void ReadVarStrSubjectAndBody(this byte[] br, ref int pos, out string subject, out string body)
		{
			subject = null;
			body = null;

			int l = (int)br.ReadVarInt(ref pos);
			
			for(int i = pos;i<pos+l;++i)
				if (br[i] == 0x0A) // find first /n
				{
					byte[] bytes = br.ReadBytes(ref pos, i - pos);
					Char[] chars = Encoding.ASCII.GetChars(bytes);
					subject = new String(chars);

					++i;
					bytes = br.ReadBytes(ref i, l - (i - pos) - 1);
					chars = Encoding.ASCII.GetChars(bytes);
					body = new String(chars);
				}
			pos += l;
		}
		
		public static void WriteVarStr(this BinaryWriter bw, string s)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(s);
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

		#region Header

		public static AutoResetEvent MaybeDisconnect = new AutoResetEvent(false);

		private static void ReadHeaderMagic(this BinaryReader br)
		{
			Debug.Write("ReadHeaderMagic ");
			try
			{
				while (br.ReadByte() != 0xE9)
				{
				}
				while (br.ReadByte() != 0xBE)
				{
				}
				while (br.ReadByte() != 0xB4)
				{
				}
				while (br.ReadByte() != 0xD9)
				{
				}
				Debug.WriteLine(" - ok");
			}
			catch (Exception e)
			{
				Debug.WriteLine("Похоже, что соединение потерено, извещаю об этом поток ClientsControlLoop " + e);
				MaybeDisconnect.Set();
				throw;
			}
		}

		private static string ReadHeaderCommand(this BinaryReader br)
		{
			Debug.Write("ReadHeaderCommand - ");

			byte[] bytes;
			try
			{
				bytes = br.ReadBytes(12);
			}
			catch (Exception e)
			{
				Debug.WriteLine("Похоже, что соединение потерено, извещаю об этом поток ClientsControlLoop " + e);
				MaybeDisconnect.Set();
				throw;
			}

			StringBuilder result = new StringBuilder(12);
			foreach (byte b in bytes)
			{
				if (b == 0) break;
				result.Append(Encoding.ASCII.GetChars(new[] {b}));
			}
			Debug.WriteLine("ReadHeaderCommand - " + result);
			return result.ToString();
		}

		public static Header ReadHeader(this BinaryReader br)
		{
			br.ReadHeaderMagic();
			return new Header
				       {
					       Command = br.ReadHeaderCommand(),
					       Length = BitConverter.ToUInt32(br.ReadBytes(4).ReverseIfNeed(), 0),
					       Checksum = br.ReadBytes(4)
				       };
		}

		public static void Send(this BinaryWriter bw, ICanBeSent message)
		{
			try
			{
				lock (bw)
				{
					bw.Write(message.Magic());
					bw.Write(message.СommandBytes());
					bw.Write(message.Length());
					bw.Write(message.Checksum());
					bw.Write(message.SentData);
					bw.Flush();
				}
			}
			catch
			{
			}
		}

		#endregion Header
	}
}
