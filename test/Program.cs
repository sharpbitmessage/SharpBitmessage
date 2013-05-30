using System;
using System.Threading;
using bitmessage;
using bitmessage.Crypto;
using bitmessage.network;

namespace test
{
	class Program
	{
		static void Main(string[] args)
		{
			//Broadcast.Test();
			var bm = new Bitmessage();

			#region test send from not my address

			//PrivateKey pk = new PrivateKey();

			PrivateKey pk = new PrivateKey("my");
			pk.SaveAsync(bm.GetConnection());
			Thread.Sleep(1000);
			bm.SendBroadcast(pk.Name, "PyBitmessage Client Vulnerability", "Vulnerability allows to send messages to other people's addresses");

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