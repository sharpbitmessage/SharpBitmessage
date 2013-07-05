using System;
using bitmessage;
using bitmessage.network;

namespace ReadAllBroadcast
{
	class Program
	{
		private static readonly Bitmessage _bm = new Bitmessage();

		static void Main(string[] args)
		{
			_bm.ReceivePubkey += NewPubkey;
			_bm.ReceiveBroadcast += NewBroadcast;
			_bm.ReceiveInvalidBroadcast += _bm_ReceiveInvalidBroadcast;

			Console.ReadLine();
		}

		static void _bm_ReceiveInvalidBroadcast(Broadcast broadcast)
		{
			Console.WriteLine("Invalid broadcast " + broadcast.Status);
		}

		private static void NewPubkey(Pubkey pubkey)
		{
			_bm.AddSubscription(pubkey);
			Console.WriteLine("Pubkey " + pubkey);
		}

		static void NewBroadcast(Broadcast broadcast)
		{
			Console.WriteLine("NewBroadcast " + broadcast);
		}

	}
}
