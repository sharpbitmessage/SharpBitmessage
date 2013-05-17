using System;
using System.Runtime.InteropServices;

namespace bitmessage.network
{
	public class NetworkAddress
	{
		public UInt32 Time;
		public UInt32 Stream;
		public UInt64 Services;
		public Char[] Ip;
		public UInt16 Port;

		public NetworkAddress(Payload payload)
		{
		}
	}
}
