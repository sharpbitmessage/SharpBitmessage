namespace bitmessage.network
{
	public class Pong : ICanBeSent
	{
		public string Command
		{
			get { return "pong"; }
		}

		public byte[] SentData
		{
			get { return null; }
		}
	}
}
