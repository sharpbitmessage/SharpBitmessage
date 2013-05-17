using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using bitmessage.network;

namespace bitmessage.Crypto
{
	public static class Base58
	{
		private static readonly char[] B58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz".ToCharArray();
		private static readonly BigInteger Big58 = new BigInteger(58);

		public static string ByteArrayToBase58(this byte[] ba)
		{
			byte[] positiveBa;
			//if (ba[ba.Length - 1] > 0x80)
			//{
			//	positiveBa = new byte[ba.Length + 1];
			//	Array.Copy(ba, positiveBa, ba.Length);
			//}
			//else
			positiveBa = ba;
			Array.Reverse(positiveBa);

			BigInteger addrremain = new BigInteger(positiveBa);

			StringBuilder rv = new StringBuilder(100);

			while (addrremain.CompareTo(BigInteger.Zero) > 0)
			{
				var remainder = addrremain % 58;
				addrremain    = addrremain / 58;
				rv.Insert(0, B58[(int) remainder]);
			}

			// handle leading zeroes
			foreach (byte b in ba)
			{
				if (b != 0) break;
				rv = rv.Insert(0, '1');
			}
			string result = rv.ToString();
			return result;
		}

		public static string ByteArrayToBase58Check(this byte[] ba)
		{
			byte[] bb = new byte[ba.Length + 4];
			Array.Copy(ba, bb, ba.Length);
			SHA512CryptoServiceProvider sha512 = new SHA512CryptoServiceProvider();
			byte[] thehash = sha512.ComputeHash(ba);
			thehash = sha512.ComputeHash(thehash);
			for (int i = 0; i < 4; i++) bb[ba.Length + i] = thehash[i];
			return ByteArrayToBase58(bb);
		}

		public static string EncodeAddress(UInt64 version, UInt64 stream, byte[] ripe)
		{
			byte[] v = version.VarIntToBytes();
			byte[] s = stream.VarIntToBytes();

			int repeOffset = 0;

			if (version >= 2)
				if ((ripe[0] == 0) && (ripe[1] == 0))
					repeOffset = 2;
				else if (ripe[0] == 0)
					repeOffset = 1;

			byte[] buff = new byte[v.Length + s.Length + ripe.Length - repeOffset];
			Buffer.BlockCopy(v, 0, buff, 0, v.Length);
			Buffer.BlockCopy(s, 0, buff, v.Length, s.Length);
			Buffer.BlockCopy(ripe, repeOffset, buff, v.Length + s.Length, ripe.Length - repeOffset);

			string result = "BM-" + ByteArrayToBase58Check(buff);
			return result;
		}
	}
}