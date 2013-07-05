using System;
using bitmessage;
using bitmessage.network;

namespace test
{
	class Program
	{
		private static readonly Bitmessage _bm = new Bitmessage();
		static void Main(string[] args)
		{
			_bm.ReceivePubkey     += NewPubkey;
			_bm.ReceiveBroadcast  += NewBroadcast;
			_bm.ReceiveMsg        += NewMsg;

			PrivateKey pk = _bm.GeneratePrivateKey("tst", true, true);
			Console.WriteLine(pk.Name);

			_bm.SendMessage(pk.Name, "BM-GuPhxCmFuJiQgCEVm2qc7mdGVrqDADBm", "2222222222222", "русский5");

			Console.ReadLine();
			//Console.WriteLine(bm.ToString());
		}

		static void NewMsg(Msg msg)
		{
			Console.WriteLine(msg);
		}

		private static void NewPubkey(Pubkey pubkey)
		{
			Console.WriteLine("Pubkey " + pubkey);
		}

		static void NewBroadcast(Broadcast broadcast)
		{
			Console.WriteLine(broadcast);
		}
	}
}