using System;

namespace bitmessage.network
{
	public class Header
	{
		public string Command;
		public UInt32 Length;
		public byte[] Checksum;
	}
}
