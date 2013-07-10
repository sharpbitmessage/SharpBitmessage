using System;
using System.IO;
using System.Text;

namespace bitmessage.network
{
	public class Header
	{
		public readonly string Command;
		public readonly int Length;
		public readonly byte[] Checksum;

		public Header(BinaryReader br)
		{
			ReadHeaderMagic(br);
			Command = ReadHeaderCommand( br.ReadBytes(12) );
			Length  = ReadLength       ( br.ReadBytes(4)  );
			Checksum =                   br.ReadBytes(4)   ;
		}

		public static int ReadLength(byte[] bytes)
		{
			UInt32 tmpLength = BitConverter.ToUInt32(bytes.ReverseIfNeed(), 0);
			if (tmpLength < int.MaxValue)
				return (int) tmpLength;
			throw new Exception("Header Length > int.MaxValue");
		}

		public static byte[] Magic = new byte[] { 0xE9, 0xBE, 0xB4, 0xD9 };

		private static void ReadHeaderMagic(BinaryReader br)
		{
			//Debug.Write("ReadHeaderMagic ");
			do
			{
				do
				{
					do
					{
						do
						{
						} while (br.ReadByte() != Magic[0]);
					} while (br.ReadByte() != Magic[1]);
				} while (br.ReadByte() != Magic[2]);
			} while (br.ReadByte() != Magic[3]);
			//Debug.WriteLine(" - ok");
		}

		public static string ReadHeaderCommand(byte[] bytes)
		{
			var result = new StringBuilder(12);
			foreach (byte b in bytes)
			{
				if (b == 0) break;
				result.Append(Encoding.ASCII.GetChars(new[] { b }));
			}
//			Debug.WriteLine("ReadHeaderCommand - " + result);
			return result.ToString();
		}
	}
}
