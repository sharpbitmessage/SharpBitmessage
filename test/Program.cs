using System;
using System.Diagnostics;
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
			bm.ReceiveBroadcast += NewBroadcast;
			bm.ReceiveInvalidBroadcast += NewBroadcast;
			bm.ReceivePubkey += NewPubkey;
			bm.ReceiveInvalidPubkey += NewPubkey;
			Console.ReadLine();
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