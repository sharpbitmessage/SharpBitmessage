using System.Globalization;

namespace bitmessage.network
{
	public class GetPubkey
	{
		public readonly ulong Version;
		public readonly ulong Stream;
		public readonly byte[] PubKeyHash;

		public string Hash4DB
		{
			get { return PubKeyHash.ToHex(false); }
		}
		public string Version4DB
		{
			get { return Version.ToString(CultureInfo.InvariantCulture); }
		}
		public string Stream4DB
		{
			get { return Stream.ToString(CultureInfo.InvariantCulture); }
		}
		
		public GetPubkey(Payload payload)
		{
			int pos = payload.FirstByteAfterTime;
			Version = payload.SentData.ReadVarInt(ref pos);
			Stream = payload.SentData.ReadVarInt(ref pos);
			PubKeyHash = payload.SentData.ReadBytes(ref pos, 20);
		}
	}
}
