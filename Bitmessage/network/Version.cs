using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace bitmessage.network
{
	public class Version : ICanBeSent
	{
		private static readonly byte[] IpPrefix = new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF};
		public static readonly UInt64 EightBytesOfRandomDataUsedToDetectConnectionsToSelf = new Random().NextUInt64();

		public readonly int Value = 1;
		public readonly UInt64 Services = 1;
		public readonly UInt64 Timestamp = DateTime.UtcNow.ToUnix();

		public readonly UInt64 AddrRecvService = 1;
		public readonly byte[] AddrRecvIpPrefix = IpPrefix;
		public readonly UInt32 AddrRecvIp = 0x7F000001; // 127.0.0.1
		public readonly UInt16 AddrRecvPort = 8444;

		public readonly UInt64 AddrFromService = 1;
		public readonly byte[] AddrFromIpPrefix = IpPrefix;
		public readonly UInt32 AddrFromIp = 0x7F000001; // 127.0.0.1
		public readonly UInt16 AddrFromPort = 8444;

		public readonly UInt64 Nonce = EightBytesOfRandomDataUsedToDetectConnectionsToSelf;

		public readonly string UserAgent = "BM-oppN7TymwFbhRMpEwZPdNP4JtKLexTiJb";
		public readonly List<UInt64> StreamNumbers = new List<UInt64>(new UInt64[] { 1 });

		public Version(){}

		public Version(Payload payload)
		{
			if (payload.Length < 83)
				throw new Exception("Version.Length<83 Length=" + payload.Length);

			int pos = 0;

			Value = payload.SentData.ReadInt32(ref pos);
			Services = payload.SentData.ReadUInt64(ref pos);
			Timestamp = payload.SentData.ReadUInt64(ref pos);

			AddrRecvService = payload.SentData.ReadUInt64(ref pos);
			AddrRecvIpPrefix = payload.SentData.ReadBytes(ref pos, 12);
			AddrRecvIp = payload.SentData.ReadUInt32(ref pos);
			AddrRecvPort = payload.SentData.ReadUInt16(ref pos);

			AddrFromService = payload.SentData.ReadUInt64(ref pos);
			AddrFromIpPrefix = payload.SentData.ReadBytes(ref pos, 12);
			AddrFromIp = payload.SentData.ReadUInt32(ref pos);
			AddrFromPort = payload.SentData.ReadUInt16(ref pos);

			Nonce = payload.SentData.ReadUInt64(ref pos);
			UserAgent = payload.SentData.ReadVarStr(ref pos);
			StreamNumbers = payload.SentData.ReadVarIntList(ref pos);
		}

		public string Command
		{
			get { return "version"; }
		}

		private byte[] _sendData;

		public byte[] SentData
		{
			get
			{
				if (_sendData == null)
				{
					Debug.WriteLine("Подготавляиваю data для отправки Version");

					MemoryStream payload = new MemoryStream(500);
					BinaryWriter localBw = new BinaryWriter(payload); // TODO избавится от  localBw

					localBw.Write(BitConverter.GetBytes(Value).ReverseIfNeed());
					localBw.Write(BitConverter.GetBytes(Services).ReverseIfNeed());
					localBw.Write(BitConverter.GetBytes(Timestamp).ReverseIfNeed());

					localBw.Write(BitConverter.GetBytes(AddrRecvService).ReverseIfNeed());
					localBw.Write(AddrRecvIpPrefix);
					localBw.Write(BitConverter.GetBytes(AddrRecvIp).ReverseIfNeed());
					localBw.Write(BitConverter.GetBytes(AddrRecvPort).ReverseIfNeed());

					localBw.Write(BitConverter.GetBytes(AddrFromService).ReverseIfNeed());
					localBw.Write(AddrRecvIpPrefix);
					localBw.Write(BitConverter.GetBytes(AddrFromIp).ReverseIfNeed());
					localBw.Write(BitConverter.GetBytes(AddrFromPort).ReverseIfNeed());

					localBw.Write(Nonce);

					localBw.WriteVarStr(UserAgent);
					localBw.WriteVarIntList(StreamNumbers);

					_sendData = payload.ToArray();

					Debug.WriteLine("version = " + _sendData.ToHex());
				}
				return _sendData;
			}
		}
	}
}