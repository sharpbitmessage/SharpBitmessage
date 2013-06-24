using System;
using System.Threading;
using bitmessage;
using bitmessage.network;

namespace test
{
	class Program
	{
		private static readonly Bitmessage _bm = new Bitmessage();
		static void Main(string[] args)
		{
			_bm.ReceivePubkey += NewPubkey;
			_bm.ReceiveBroadcast += NewBroadcast;
			_bm.ReceiveInvalidBroadcast += _bm_ReceiveInvalidBroadcast;

			PrivateKey pk = _bm.GeneratePrivateKey("tst");
			Thread.Sleep(1000);
			//_bm.SendBroadcast(pk.Name, "sub", "body");

			Console.ReadLine();
			//Console.WriteLine(bm.ToString());
		}

		static void _bm_ReceiveInvalidBroadcast(Broadcast broadcast)
		{
			Console.WriteLine("Invalid broadcast " + broadcast.Status);
		}

		private static void NewPubkey(Pubkey pubkey)
		{
			//if (pubkey.Name == "BM-2DAV89w336ovy6BUJnfVRD5B9qipFbRgmr")
				_bm.AddSubscription(pubkey);
			Console.WriteLine("Pubkey " + pubkey);
		}

		static void NewBroadcast(Broadcast broadcast)
		{
			Console.WriteLine("NewBroadcast " + broadcast);
		}
	}
}