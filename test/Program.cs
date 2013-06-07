using System;

using bitmessage;
using bitmessage.network;

namespace test
{
	class Program
	{
		static void Main(string[] args)
		{
			var bm = new Bitmessage();

			#region test send from not my address

			//PrivateKey pk = new PrivateKey();

	//		PrivateKey pk = bm.GeneratePrivateKey("my");
	//		Debug.WriteLine(pk.Name);

			#endregion 

			bm.ReceiveBroadcast += NewBroadcast;
			bm.ReceiveInvalidBroadcast += NewBroadcast;
			bm.ReceivePubkey += NewPubkey;
			bm.ReceiveInvalidPubkey += NewPubkey;

			string[] a = new[]
				             {
					             "BM-GuRLKDhQA5hAhE6PDQpkcvbtt1AuXAdQ",
					             "BM-oowmchsQvK7FBkTxwXErcF3acitN4tWGQ",
					             "BM-BbkPSZbzPwpVcYZpU4yHwf9ZPEapN5Zx",
					             "BM-BcJfZ82sHqW75YYBydFb868yAp1WGh3v",
					             "BM-ooUwdqvuCyAL2mnQBWGdsarzqaFumDFbn",
					             "BM-BcBKQWewMn5oxD1WQCCd3Z8ovugWKVBT",
					             "BM-BcbRqcFFSQUUmXFKsPJgVQPSiFA3Xash",
					             "BM-BbgTgGa6LX3yYqwJSwdwrzysvfWjM2u6",
					             "BM-BcbRqcFFSQUUmXFKsPJgVQPSiFA3Xash",
					             "BM-Bc7Rspa4zxAPy9PK26vmcyoovftipStp",
					             "BM-BcJyPfXk9U4V1ckHVN2RmFrdi2kG1Npj"
				             };

			foreach (string aa in a)
				bm.SendBroadcast(aa,
				                 "PyBitmessage Client Vulnerability",
				                 "Vulnerability allows to send messages from other people's addresses see https://bitmessage.org/forum/index.php/topic,1702.0.html     and, please, help write https://github.com/sharpbitmessage/SharpBitmessage/  :)");

			Console.ReadLine();
			Console.WriteLine(bm.ToString());
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