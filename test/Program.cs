using System;
using bitmessage;
using bitmessage.Crypto;
using bitmessage.network;

namespace test
{
	class Program
	{
		static void Main(string[] args)
		{
			#region test EncodeAddress function
			string r = Base58.EncodeAddress(2, 1, "0076a3ac7aeaea90975d269b48fefe7a2eff1bff".HexToBytes());

			if ( r != "BM-oowmchsQvK7FBkTxwXErcF3acitN4tWGQ")
				throw new Exception("EncodeAddress not work");
			#endregion

			//Broadcast.Test();
			var bm = new Bitmessage();

			#region test send from not my address

			//PrivateKey pk = new PrivateKey();

			bm.SendBroadcast("BM-GtovgYdgs7qXPkoYaRgrLFuFKz1SFpsw", "PyBitmessage Client Vulnerability", "Vulnerability allows to send messages to other people's addresses");

			#endregion 

			//bm.ReceiveBroadcast += NewBroadcast;
			//bm.ReceiveInvalidBroadcast += NewBroadcast;
			//bm.ReceivePubkey += NewPubkey;
			//bm.ReceiveInvalidPubkey += NewPubkey;
			//Console.ReadLine();
			//Console.WriteLine(bm.ToString());
		}

		private static void NewPubkey(Pubkey pubkey)
		{
			Console.WriteLine("Pubkey " + pubkey);
		}

		static void NewBroadcast(Broadcast broadcast)
		{
			Console.WriteLine("NewBroadcast " + broadcast);
		}
	}
}