using System;
using Org.BouncyCastle.Asn1.X9;



namespace bitmessage.Crypto
{
	public class ECC
	{

		public ECC()
		{
			X9ECParameters x = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");

		}

		public void PubToAddress(byte[] pubKey)
		{
			byte[] hex2 = new byte[21];
			Buffer.BlockCopy(pubKey, 0, hex2, 1, 20);

		}
	}
}
