    using System;
    using System.Text;
    using Org.BouncyCastle.Asn1;
    using Org.BouncyCastle.Asn1.Sec;
    using Org.BouncyCastle.Asn1.X9;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Crypto.Signers;
    using Org.BouncyCastle.Utilities.Encoders;

    namespace ConsoleApplication2
    {
	internal class Program
	{
		//private static readonly byte[] _privkey  = "02ca0020215c5516f277ac6246cbbaad81cd848328bf9bf11e98959e2b991191a71ad81a".HexToBytes();
		private static readonly byte[] _pubkey =
			Hex.Decode(
				"02ca0020012e0e59b564c025b15a587da5d33d3599df5e04deca47c783eaed25ebe5af46002032e00af993efc71a2c033a45187918f5b3c03e0e7bb539cecdc0aaa237717db1");

		private static readonly byte[] _signature =
			Hex.Decode(
				"30450221008538ac52dbe2b67148e99f23ad78b4c6c4939a26d789ece590c6f1e44a271454022027d4a09e5e74bb3445019a557bd2202154d2510a4df939b9f4645b311255ee37");

		private static readonly byte[] _hello = Encoding.ASCII.GetBytes("hello");

		private static byte[] ConvertKeyFormat(byte[] k)
		{
			// convert key to 04012e0e59b564c025b15a587da5d33d3599df5e04deca47c783eaed25ebe5af4632e00af993efc71a2c033a45187918f5b3c03e0e7bb539cecdc0aaa237717db1
			byte[] result = new byte[k.Length-4+1-2];
			result[0] = 4;
			Buffer.BlockCopy(k,4     ,result,1 ,32);
			Buffer.BlockCopy(k,4+32+2,result,33,32);
			return result;
		}

		private static void Main()
		{
			var signer = new ECDsaSigner();

			X9ECParameters secp256K1 = SecNamedCurves.GetByName("secp256k1");
			ECDomainParameters ecParams = new ECDomainParameters(secp256K1.Curve, secp256K1.G, secp256K1.N, secp256K1.H);
			ECPublicKeyParameters param = new ECPublicKeyParameters(ecParams.Curve.DecodePoint(ConvertKeyFormat(_pubkey)), ecParams);

			signer.Init(false, param);

			DerSequence seq = (DerSequence)(new Asn1InputStream(_signature)).ReadObject();
			DerInteger r = (DerInteger)seq[0];
			DerInteger s = (DerInteger)seq[1];

			Console.WriteLine(signer.VerifySignature(_hello, r.Value, s.Value));
		}
	}
    }