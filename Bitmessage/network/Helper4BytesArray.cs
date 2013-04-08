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
		private const int maximumAgeOfAnObjectThatIAmWillingToAccept = 216000;

		public static bool IsValid(this byte[] ba)
		{
			#region IsProofOfWorkSufficient

			int pos;
			byte[] resultHash;
			using (var sha512 = new SHA512Managed())
			{
				byte[] buff = new byte[8 + 512/8];
				Buffer.BlockCopy(ba, 0, buff, 0, 8);

				pos = 8;
				byte[] initialHash = sha512.ComputeHash(ba.ReadBytes(ref pos, ba.Length - pos));
				Buffer.BlockCopy(initialHash, 0, buff, 8, initialHash.Length);
				resultHash = sha512.ComputeHash(sha512.ComputeHash(buff));
			}

			pos = 0;
			UInt64 pow = resultHash.ReadUInt64(ref pos);

			UInt64 target =
				(UInt64) ((decimal) Math.Pow(2, 64)/((ba.Length + PayloadLengthExtraBytes)*AverageProofOfWorkNonceTrialsPerByte));

			Debug.WriteLine("ProofOfWork="+(pow < target) + " pow=" + pow + " target=" + target + " lendth=" + ba.Length);

			if (pow >= target) return false;

			#endregion IsProofOfWorkSufficient

			#region Check embeddedTime
			
			UInt64 embeddedTime = ba.GetEmbeddedTime();

			Debug.WriteLine("embeddedTime = "+embeddedTime.FromUnix());

			if ((embeddedTime > (DateTime.UtcNow.ToUnix() + 10800))
				|| (embeddedTime < (DateTime.UtcNow.ToUnix() - maximumAgeOfAnObjectThatIAmWillingToAccept)))
				return false;

			#endregion Check embeddedTime

			return true;
		}

		public static byte[] CalculateInventoryHash(this byte[] ba)
		{
			byte[] inventoryHash = new byte[32];
			using (var sha512 = new SHA512Managed())
				Buffer.BlockCopy(sha512.ComputeHash(sha512.ComputeHash(ba)), 0, inventoryHash, 0, 32);
			return inventoryHash;
		}

		public static UInt32 GetEmbeddedTime(this byte[] ba)
		{
			int pos = 8;
			return ba.ReadUInt32(ref pos);
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