using System;
using System.Collections.Generic;

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
	}
}
