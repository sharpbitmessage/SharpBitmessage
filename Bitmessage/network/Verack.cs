namespace bitmessage.network
{
	public class Verack : ICanBeSent
	{
		public string Сommand
		{
			get { return "verack"; }
		}

		public byte[] SentData
		{
			get { return null; }
		}
	}
}
