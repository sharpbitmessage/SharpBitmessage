using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace bitmessage.Crypto
{
	public static class Base58
	{
		private static readonly char[] B58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz".ToCharArray();
		private static readonly BigInteger Big58 = new BigInteger(58);

		public static byte[] Base58ToByteArray(string base58)
		{
			BigInteger bi2 = new BigInteger(0);
			string b58 =new string(B58);

			foreach (char c in base58)
			{
				if (b58.IndexOf(c) != -1)
				{
					bi2 = BigInteger.Multiply(bi2, Big58);
					bi2 = BigInteger.Add(bi2, b58.IndexOf(c));
				}				
				else
					return null;
			}

			byte[] bb = bi2.ToByteArray();
			if (bb[bb.Length-1]==0)
			{
				byte[] withoutZero = new byte[bb.Length-1];
				Buffer.BlockCopy(bb, 0, withoutZero, 0, bb.Length - 1);
				bb = withoutZero;
			}
			Array.Reverse(bb);
			return bb;
		}

		public static byte[] Base58ToByteArrayCheck(string base58)
		{
			byte[] bytes = Base58ToByteArray(base58);
			byte[] result = new byte[bytes.Length-4];
			Buffer.BlockCopy(bytes, 0, result, 0, result.Length); // TODO Check hash
			return result;
		}

		public static string ByteArrayToBase58(this byte[] ba)
		{
			byte[] tmp = ba;
			Array.Reverse(tmp);
			byte[] positiveBa;
			if (tmp[tmp.Length - 1] >= 0x80)
			{
				positiveBa = new byte[ba.Length + 1];
				Array.Copy(tmp, positiveBa, tmp.Length);
			}
			else positiveBa = tmp;

			BigInteger addrremain = new BigInteger(positiveBa);
			if (addrremain<0) throw new Exception("Negative? I wont positive");

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
			byte[] baWithChecksum = new byte[ba.Length + 4];
			Array.Copy(ba, baWithChecksum, ba.Length);

			byte[] thehash;
			using (var sha512 = new SHA512CryptoServiceProvider())
				thehash = sha512.ComputeHash( sha512.ComputeHash(ba) );

			for (int i = 0; i < 4; i++) baWithChecksum[ba.Length + i] = thehash[i];
			return ByteArrayToBase58(baWithChecksum);
		}
	}
}