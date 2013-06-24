using System;

namespace bitmessage
{
	public enum MsgType
	{
		NotKnown = 0,
		Version,
		Inv,
		Getpubkey,
		Pubkey,
		Addr,
		Broadcast,
		Msg
	};

	public static class EnumHelper
	{
		public static T FromString<T>(string value)
		{
			return (T)Enum.Parse(typeof(T), value, true);
		}
	}

	public enum EncodingType
	{
		Ignore = 0,
		Trivial = 1,
		Simple = 2
	};

	public enum Status { New, Valid, Invalid, Encrypted}
}
