using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace bitmessage.network
{
	public static class Helper4BytesArray
	{
		public static byte[] ReverseIfNeed(this byte[] inArray)
		{
			if (!BitConverter.IsLittleEndian)
				return inArray;

			int highCtr = inArray.Length - 1;

			for (int ctr = 0; ctr < inArray.Length / 2; ctr++)
			{
				byte temp = inArray[ctr];
				inArray[ctr] = inArray[highCtr];
				inArray[highCtr] = temp;
				highCtr -= 1;
			}
			return inArray;
		}

		public static string ToHex(this byte[] ba)
		{
			StringBuilder sb = new StringBuilder(2 + ba.Length*2);
			sb.Append("0x");
			foreach (byte b in ba)
				sb.AppendFormat("{0:x2}", b);
			return sb.ToString();
		}

		public static Int32 ReadInt32(this byte[] ba, ref int pos)
		{
			byte[] tmp = new byte[4];
			Buffer.BlockCopy(ba, pos, tmp, 0, 4);
			pos += 4;
			return BitConverter.ToInt32(tmp.ReverseIfNeed(), 0);
		}

		public static UInt16 ReadUInt16(this byte[] ba, ref int pos)
		{
			byte[] tmp = new byte[2];
			Buffer.BlockCopy(ba, pos, tmp, 0, 2);
			pos += 2;
			return BitConverter.ToUInt16(tmp.ReverseIfNeed(), 0);
		}

		public static UInt32 ReadUInt32(this byte[] ba, ref int pos)
		{
			byte[] tmp = new byte[4];
			Buffer.BlockCopy(ba, pos, tmp, 0, 4);
			pos += 4;
			return BitConverter.ToUInt32(tmp.ReverseIfNeed(), 0);
		}

		public static UInt64 ReadUInt64(this byte[] ba, ref int pos)
		{
			byte[] tmp = new byte[8];
			Buffer.BlockCopy(ba, pos, tmp, 0, 8);
			pos += 8;
			return BitConverter.ToUInt64(tmp.ReverseIfNeed(), 0);
		}

		public static byte[] ReadBytes(this byte[] ba, ref int pos, int count)
		{
			byte[] tmp = new byte[count];
			Buffer.BlockCopy(ba, pos, tmp, 0, count);
			pos += count;
			return tmp;
		}

		private const int AverageProofOfWorkNonceTrialsPerByte = 320;
		private const int PayloadLengthExtraBytes = 14000;


		public static bool IsProofOfWorkSufficient(this byte[] ba)
		{
			var sha512 = new SHA512Managed();
			byte[] buff = new byte[8 + 512/8];
			Buffer.BlockCopy(ba, 0, buff, 0, 8);

			int pos = 8;
			byte[] initialHash = sha512.ComputeHash(ba.ReadBytes(ref pos, ba.Length - pos));
			Buffer.BlockCopy(initialHash, 0, buff, 8, initialHash.Length);
			byte[] resultHash = sha512.ComputeHash(sha512.ComputeHash(buff));
			
			pos = 0;
			UInt64 pow = resultHash.ReadUInt64(ref pos);

			UInt64 target =
				(UInt64) ((decimal) Math.Pow(2, 64)/((ba.Length + PayloadLengthExtraBytes)*AverageProofOfWorkNonceTrialsPerByte));

			Debug.WriteLine("ProofOfWork="+(pow < target) + " pow=" + pow + " target=" + target + " lendth=" + ba.Length);

			return pow < target;
		}
	}

	public class ByteArrayComparer : IEqualityComparer<byte[]>
	{
		public bool Equals(byte[] left, byte[] right)
		{
			if (left == null || right == null)
			{
				return left == right;
			}
			return left.SequenceEqual(right);
		}
		public int GetHashCode(byte[] key)
		{
			if (key == null)
				throw new ArgumentNullException("key");
			return key.Sum(b => b);
		}
	}
}
