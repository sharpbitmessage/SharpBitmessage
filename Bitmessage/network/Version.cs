using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace bitmessage.network
{
	public class Version
	{
		private static readonly byte[] IpPrefix = new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF};
		public static readonly UInt64 EightBytesOfRandomDataUsedToDetectConnectionsToSelf = new Random().NextUInt64();

		public int Value = 1;
		public UInt64 Services = 1;
		public UInt64 Timestamp = DateTime.UtcNow.ToUnix();

		public UInt64 AddrRecvService = 1;
		public byte[] AddrRecvIpPrefix = IpPrefix;
		public UInt32 AddrRecvIp = 0x7F000001;// 127.0.0.1
		public UInt16 AddrRecvPort = 8444;

		public UInt64 AddrFromService = 1;
		public byte[] AddrFromIpPrefix = IpPrefix;
		public UInt32 AddrFromIp = 0x7F000001;// 127.0.0.1
		public UInt16 AddrFromPort = 8444;

		public UInt64 Nonce = EightBytesOfRandomDataUsedToDetectConnectionsToSelf;

		public string UserAgent = "BM-oppN7TymwFbhRMpEwZPdNP4JtKLexTiJb";
		public List<UInt64> StreamNumbers = new List<UInt64>(new UInt64[] {1});

		public Version(){}

		public Version(Payload payload)
		{
			if (payload.Length < 83)
				throw new Exception("Version.Length<83 Length=" + payload.Length);

			int pos = 0;

			Value = payload.Data.ReadInt32(ref pos);
			Services = payload.Data.ReadUInt64(ref pos);
			Timestamp = payload.Data.ReadUInt64(ref pos);

			AddrRecvService = payload.Data.ReadUInt64(ref pos);
			AddrRecvIpPrefix = payload.Data.ReadBytes(ref pos, 12);
			AddrRecvIp = payload.Data.ReadUInt32(ref pos);
			AddrRecvPort = payload.Data.ReadUInt16(ref pos);

			AddrFromService = payload.Data.ReadUInt64(ref pos);
			AddrFromIpPrefix = payload.Data.ReadBytes(ref pos, 12);
			AddrFromIp = payload.Data.ReadUInt32(ref pos);
			AddrFromPort = payload.Data.ReadUInt16(ref pos);

			Nonce = payload.Data.ReadUInt64(ref pos);
			UserAgent = payload.Data.ReadVarStr(ref pos);
			StreamNumbers = payload.Data.ReadVarIntList(ref pos);
		}

		public void Send(BinaryWriter bw)
		{
			Debug.WriteLine("Подготавляиваю payload для отправки Version");

			MemoryStream payload = new MemoryStream(500);
			BinaryWriter localBw = new BinaryWriter(payload);

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

			byte[] payloadbytes = payload.ToArray();

			Debug.WriteLine("version = " + payloadbytes.ToHex());

			bw.WriteHeader("version", payloadbytes);
			bw.Flush();
		}
	}
}