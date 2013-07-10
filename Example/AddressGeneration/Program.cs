using System;
using bitmessage;
using bitmessage.network;

namespace AddressGeneration
{
	class Program
	{
		private static readonly Bitmessage _bm = new Bitmessage(false);

		static readonly string[] ListPattern = 
        {
            @"(.)\1{4,}"
			//,"god"
			//,"BM-2D7"
        };

		static void Main()
		{
			while(true)
			{
				PrivateKey pk = _bm.GeneratePrivateKey(null, false, false, false);
				foreach (string sPattern in ListPattern)
				{
					if (System.Text.RegularExpressions.Regex.IsMatch(pk.Name, sPattern))
					{
						Console.WriteLine("["+pk.Name+"]");
						Console.WriteLine("label = " + sPattern);
						Console.WriteLine("enabled = false");
						Console.WriteLine("decoy = false");
						Console.WriteLine("noncetrialsperbyte = "+pk.NonceTrialsPerByte);
						Console.WriteLine("payloadlengthextrabytes = "+pk.PayloadLengthExtraBytes);
						Console.WriteLine("privsigningkey = "+pk.PrivSigningKeyWif);
						Console.WriteLine("privencryptionkey = " + pk.PrivEncryptionKeyWif);
						Console.WriteLine("lastpubkeysendtime = 0" + pk.PrivEncryptionKeyWif);
						Console.WriteLine();

						break;
					}
				}
			}
		}
	}
}