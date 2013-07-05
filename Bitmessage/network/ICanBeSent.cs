using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace bitmessage.network
{
	public interface ICanBeSent
	{		
		string Command { get; }
		byte[] SentData { get; }
	}

	public static class CanBeSentHalper
	{
		private static readonly byte[] _nullChecksum = new byte[] { 0xCF, 0x83, 0xE1, 0x35 };
		private static readonly byte[] _magic = new byte[] { 0xE9, 0xBE, 0xB4, 0xD9 };

		public static byte[] Magic(this ICanBeSent message)
		{
			return _magic;
		}

		public static byte[] СommandBytes(this ICanBeSent message)
		{
			if (message.Command.Length > 12) throw new Exception("command.Length>12 command=" + message.Command);
			byte[] ascii = Encoding.ASCII.GetBytes(message.Command);
			byte[] result = new byte[12];
			Buffer.BlockCopy(ascii, 0, result, 0, ascii.Length);
			return result;
		}

		public static byte[] Length(this ICanBeSent message)
		{
			return
				message.SentData == null
					? new byte[] {0, 0, 0, 0}
					: BitConverter.GetBytes(message.SentData.Length).ReverseIfNeed();
		}

		public static byte[] Checksum(this ICanBeSent message)
		{
			if (message.SentData == null)
				return _nullChecksum;
			if (message.SentData.Length == 0)
				return _nullChecksum;

			byte[] hash;
			using (var sha512 = new SHA512Managed())
				hash = sha512.ComputeHash(message.SentData);

			byte[] result = new byte[4];
			Buffer.BlockCopy(hash, 0, result, 0, 4);

			return result;
		}

		public static byte[] GetFullMsg(this ICanBeSent message)
		{
			using (MemoryStream ms = new MemoryStream())
			using (BinaryWriter bw = new BinaryWriter(ms))
			{
				bw.Write(message.Magic());
				bw.Write(message.СommandBytes());
				bw.Write(message.Length());
				bw.Write(message.Checksum());
				if (message.SentData != null) bw.Write(message.SentData);

				return ms.ToArray();
			}
		}
	}
}