using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
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
			int l = (int) br.ReadVarInt(ref pos);
			byte[] bytes = br.ReadBytes(ref pos, l);
			Char[] chars = Encoding.ASCII.GetChars(bytes);
			return new String(chars);
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

		private static void WriteHeaderMagic(this BinaryWriter bw)
		{
			Debug.WriteLine("WriteHeaderMagic");
			bw.Write(BitConverter.GetBytes(0xE9BEB4D9).ReverseIfNeed());
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

		private static void WriteHeaderCommand(this BinaryWriter bw, String command)
		{
			if (command.Length > 12) throw new ArgumentOutOfRangeException("command", "command.Length>12 command=" + command);
			byte[] bytes = Encoding.ASCII.GetBytes(command);
			bw.Write(bytes);
			for (int i = bytes.Length; i < 12; ++i)
				bw.Write((byte) 0);
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

		private static readonly byte[] _nullPayload = new byte[] {0, 0, 0, 0, 0xCF, 0x83, 0xE1, 0x35};

		public static void WriteHeader(this BinaryWriter bw, String command, byte[] payload)
		{
			bw.WriteHeaderMagic();
			bw.WriteHeaderCommand(command);

			if ((payload == null) || (payload.Length == 0))
			{
				Debug.WriteLine("WriteHeader " + command + " payload null or zero");
				bw.Write(_nullPayload);
			}
			else
			{
				Debug.WriteLine("WriteHeader " + command + " payload.Length=" + payload.Length);

				bw.Write(BitConverter.GetBytes((UInt32) payload.Length).ReverseIfNeed());

				byte[] sha512 = new SHA512Managed().ComputeHash(payload);

				for (int i = 0; i < 4; ++i)
					bw.Write(sha512[i]);

				bw.Write(payload);
			}
		}

		#endregion Header

		public static void SendVerack(this BinaryWriter bw)
		{
			bw.WriteHeader("verack", null);
			bw.Flush();
		}

		public static void SendGetdata(this BinaryWriter bw, List<byte[]> list)
		{
			MemoryStream payloadBuff = new MemoryStream(9 + list.Count * 32);
			BinaryWriter payload = new BinaryWriter(payloadBuff);
			payload.Write(((ulong) list.Count).VarIntToBytes());
			foreach(byte[] item in list)
				payload.Write(item);

			bw.WriteHeader("getdata", payloadBuff.GetBuffer());
			bw.Flush();
		}

		public static IEnumerable<byte[]> ToInv(this byte[] br)
		{
			int pos = 0;
			int brL = br.Length;
			int count = (int)br.ReadVarInt(ref pos);
			for (int i = 0; (i < count) && (brL > pos); ++i)
				yield return br.ReadBytes(ref pos, 32);
		}

	}
}
