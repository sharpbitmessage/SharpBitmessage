using System;
using System.Runtime.InteropServices;

namespace bitmessage.network
{
	[StructLayout(LayoutKind.Sequential, Size = 34), Serializable]
	public struct NetworkAddress
	{
		public UInt32 Time;
		public UInt32 Stream;
		public UInt64 Services;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public Char[] Ip;//char[16] IPv6 address. The original client only supports IPv4 and only reads the last 4 bytes to get the IPv4 address. However, the IPv4 address is written into the message as a 16 byte IPv4-mapped IPv6 address (12 bytes 00 00 00 00 00 00 00 00 00 00 FF FF, followed by the 4 bytes of the IPv4 address).
		public UInt16 Port;
	}
}
