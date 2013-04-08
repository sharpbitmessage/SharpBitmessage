namespace bitmessage
{
	public enum MsgType
	{
		Broadcast,
		Pubkey,
		Msg
	};

	public enum EncodingType
	{
		Ignore = 0,
		Trivial = 1,
		Simple = 2
	};

	public enum MsgStatus{New, Valid, Invalid}
}
