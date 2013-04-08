//  see https://bitcointalk.org/index.php?topic=25141.0;wap2
//  see https://gist.github.com/CodesInChaos/3175971

using System;
using System.Security.Cryptography;
using bitmessage.network;

namespace bitmessage.Crypto
{
	public static class Base58
	{
		private const string B58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
		private static readonly Org.BouncyCastle.Math.BigInteger Big0 = new Org.BouncyCastle.Math.BigInteger("0");
		private static readonly Org.BouncyCastle.Math.BigInteger Big58 = new Org.BouncyCastle.Math.BigInteger("58");

		public static byte[] Base58ToByteArray(this string base58)
		{
			Org.BouncyCastle.Math.BigInteger bi2 = new Org.BouncyCastle.Math.BigInteger("0");

			foreach (char c in base58)
			{
				if (B58.IndexOf(c) != -1)
				{
					bi2 = bi2.Multiply(new Org.BouncyCastle.Math.BigInteger("58"));
					bi2 = bi2.Add(new Org.BouncyCastle.Math.BigInteger(B58.IndexOf(c).ToString()));
				}
				else
				{
					return null;
				}
			}
			byte[] bb = bi2.ToByteArrayUnsigned();

			// interpret leading '1's as leading zero bytes
			foreach (char c in base58)
			{
				if (c != '1') break;
				byte[] bbb = new byte[bb.Length + 1];
				Array.Copy(bb, 0, bbb, 1, bb.Length);
				bb = bbb;
			}

			if (bb.Length < 4) return null;

			SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();
			byte[] checksum = sha256.ComputeHash(bb, 0, bb.Length - 4);
			checksum = sha256.ComputeHash(checksum);
			for (int i = 0; i < 4; i++)
			{
				if (checksum[i] != bb[bb.Length - 4 + i]) return null;
			}

			byte[] rv = new byte[bb.Length - 4];
			Array.Copy(bb, 0, rv, 0, bb.Length - 4);
			return rv;
		}

		public static string ByteArrayToBase58(this byte[] ba)
		{
			Org.BouncyCastle.Math.BigInteger addrremain = new Org.BouncyCastle.Math.BigInteger(1, ba);

			string rv = "";

			while (addrremain.CompareTo(Big0) > 0)
			{
				int d = Convert.ToInt32(addrremain.Mod(Big58).ToString());
				addrremain = addrremain.Divide(Big58);
				rv = B58.Substring(d, 1) + rv;
			}

			// handle leading zeroes
			foreach (byte b in ba)
			{
				if (b != 0) break;
				rv = "1" + rv;

			}
			return rv;
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

		public static string EncodeAddress(UInt64 version,UInt64 stream,byte[] ripe)
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

			return "BM-" + ByteArrayToBase58Check(buff);
		}
	}
}