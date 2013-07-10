using System;
using bitmessage;
using bitmessage.network;

namespace test
{
	class Program
	{
	    private static readonly Bitmessage Bitmessage = new Bitmessage();
		static void Main()
		{
            //Bitmessage.ReceivePubkey     += NewPubkey;
            //Bitmessage.ReceiveBroadcast  += NewBroadcast;
            //Bitmessage.ReceiveMsg        += NewMsg;

            //PrivateKey pk = Bitmessage.GeneratePrivateKey("tst", true, true);
            //Console.WriteLine(pk.Name);

            //Bitmessage.SendMessage(pk.Name, "BM-2D88888iFvohJyschKVRKTJq4KCboU9sov", "111test .Net implementation https://github.com/sharpbitmessage/sharpbitmessage", "https://github.com/sharpbitmessage/sharpbitmessage work!");

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
            if (pubkey.Name == "BM-2DAV89w336ovy6BUJnfVRD5B9qipFbRgmr")
                Bitmessage.AddSubscription(pubkey);
		}

		static void NewBroadcast(Broadcast broadcast)
		{
			Console.WriteLine(broadcast);
		}
	}
}