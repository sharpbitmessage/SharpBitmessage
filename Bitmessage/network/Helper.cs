﻿using System;
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
			var buffer = new byte[sizeof(UInt64)];
			rnd.NextBytes(buffer);
			return BitConverter.ToUInt64(buffer, 0);
		}

		#region Unix DateTime

		static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

		public static UInt64 ToUnix(this DateTime dt)
		{
			return (UInt64)(dt - epoch).TotalSeconds;
		}

		public static DateTime FromUnix(this Int64 unix)
		{
			return epoch.AddSeconds(unix);
		}

		#endregion Unix DateTime

		#region VarInt VarStr VarIntList

		static UInt64 ReadVarInt(this byte[] br, ref int pos)
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
		static void WriteVarInt(this BinaryWriter bw, UInt64 i)
		{
			if (i < 253)
				bw.Write((Byte) i);
			else if (i <= 0xffff)
			{
				bw.Write((Byte) 253);
				bw.Write(BitConverter.GetBytes((UInt16) i).ReverseIfNeed());
			}
			else if (i <= 0xffffffff)
			{
				bw.Write((Byte) 254);
				bw.Write(BitConverter.GetBytes((UInt32) i).ReverseIfNeed());
			}
			else
			{
				bw.Write((Byte) 255);
				bw.Write(BitConverter.GetBytes((UInt64) i).ReverseIfNeed());
			}
		}

		static string ReadVarStr(this byte[] br, ref int pos)
		{
			int l = (int)br.ReadVarInt(ref pos);
			byte[] bytes = br.ReadBytes(ref pos, l);
			Char[] chars = Encoding.ASCII.GetChars(bytes);
			return new String(chars);
		}

		static void WriteVarStr(this BinaryWriter bw,string s)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(s);
			bw.WriteVarInt((UInt16) bytes.Length);
			bw.Write(bytes);
		}

		static List<UInt64> ReadVarIntList(this byte[] br, ref int pos)
		{
			UInt64 l = br.ReadVarInt(ref pos);
			List<UInt64> result = new List<UInt64>((int) l);
			for (int i = 0; i < (int) l; ++i)
				result.Add(br.ReadVarInt(ref pos));
			return result;
		}

		static void WriteVarIntList(this BinaryWriter bw,List<UInt64> list)
		{
			bw.WriteVarInt((UInt64) list.Count);
			foreach (UInt64 item in list)
				bw.WriteVarInt(item);
		}

		#endregion VarInt VarStr VarIntList

		#region Header

		public static AutoResetEvent MaybeDisconnect = new AutoResetEvent(false);

		static void ReadHeaderMagic(this BinaryReader br)
		{
			Debug.Write("ReadHeaderMagic ");
			try
			{
				while (br.ReadByte() != 0xE9) { }
				while (br.ReadByte() != 0xBE) { }
				while (br.ReadByte() != 0xB4) { }
				while (br.ReadByte() != 0xD9) { }
				Debug.WriteLine(" - ok");
			}
			catch (Exception e)
			{
				Debug.WriteLine("Похоже, что соединение потерено, извещаю об этом поток ClientsControlLoop " + e);
				MaybeDisconnect.Set();
				throw;
			}
		}
		static void WriteHeaderMagic(this BinaryWriter bw)
		{
			Debug.WriteLine("WriteHeaderMagic");
			bw.Write(BitConverter.GetBytes((UInt32) 0xE9BEB4D9).ReverseIfNeed());
		}

		static string ReadHeaderCommand(this BinaryReader br)
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
				if (b==0) break;
				result.Append(Encoding.ASCII.GetChars(new byte[] {b}));
			}
			Debug.WriteLine("ReadHeaderCommand - " + result);
			return result.ToString();
		}
		static void WriteHeaderCommand(this BinaryWriter bw, String command)
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

		public static void WriteHeader(this BinaryWriter bw, String command, byte[] payload)
		{
			Debug.WriteLine("WriteHeader " + command + " payload.Length=" + payload.Length);

			//MemoryStream buf = new MemoryStream();
			//BinaryWriter bw = new BinaryWriter(buf);

			bw.WriteHeaderMagic();
			bw.WriteHeaderCommand(command);
			bw.Write(BitConverter.GetBytes((UInt32) payload.Length).ReverseIfNeed());

			byte[] sha512 = new SHA512Managed().ComputeHash(payload);

			for(int i=0;i<4;++i)
				bw.Write(sha512[i]);

			bw.Write(payload);

			//byte[] payloadbytes = buf.ToArray();
			//Debug.WriteLine("msg = " + payloadbytes.ToHex());
		}

		#endregion Header

		#region Version

		public static Version ReadVersion(this byte[] br)
		{
			if (br.Length < 83)
			{
				Debug.WriteLine("Version.Length<83");
				return null;
			}
			int pos = 0;
			return new Version
				       {
					       Value = br.ReadInt32(ref pos),
					       Services = br.ReadUInt64(ref pos),
					       Timestamp = br.ReadUInt64(ref pos),

					       AddrRecvService = br.ReadUInt64(ref pos),
					       AddrRecvIpPrefix = br.ReadBytes(ref pos, 12),
					       AddrRecvIp = br.ReadUInt32(ref pos),
					       AddrRecvPort = br.ReadUInt16(ref pos),

					       AddrFromService = br.ReadUInt64(ref pos),
					       AddrFromIpPrefix = br.ReadBytes(ref pos, 12),
					       AddrFromIp = br.ReadUInt32(ref pos),
					       AddrFromPort = br.ReadUInt16(ref pos),

					       Nonce = br.ReadUInt64(ref pos),
					       UserAgent = br.ReadVarStr(ref pos),
					       StreamNumbers = br.ReadVarIntList(ref pos)
				       };
		}

		public static void WriteVersion(this BinaryWriter bw, Version v)
		{
			Debug.WriteLine("Подготавляиваю payload для отправки Version");

			MemoryStream payload = new MemoryStream(500);
			BinaryWriter localBw = new BinaryWriter(payload);

			localBw.Write(BitConverter.GetBytes(v.Value).ReverseIfNeed());
			localBw.Write(BitConverter.GetBytes(v.Services).ReverseIfNeed());
			localBw.Write(BitConverter.GetBytes(v.Timestamp).ReverseIfNeed());

			localBw.Write(BitConverter.GetBytes(v.AddrRecvService).ReverseIfNeed());
			localBw.Write( v.AddrRecvIpPrefix );
			localBw.Write(BitConverter.GetBytes(v.AddrRecvIp).ReverseIfNeed());
			localBw.Write(BitConverter.GetBytes(v.AddrRecvPort).ReverseIfNeed());

			localBw.Write(BitConverter.GetBytes(v.AddrFromService).ReverseIfNeed());
			localBw.Write(v.AddrRecvIpPrefix);
			localBw.Write(BitConverter.GetBytes(v.AddrFromIp).ReverseIfNeed());
			localBw.Write(BitConverter.GetBytes(v.AddrFromPort).ReverseIfNeed());

			localBw.Write(v.Nonce);

			localBw.WriteVarStr(v.UserAgent);
			localBw.WriteVarIntList(v.StreamNumbers);

			byte[] payloadbytes = payload.ToArray();

			Debug.WriteLine("version = " + payloadbytes.ToHex());

			bw.WriteHeader("version", payloadbytes);
			bw.Flush();
		}

		#endregion 	Version
	}
}