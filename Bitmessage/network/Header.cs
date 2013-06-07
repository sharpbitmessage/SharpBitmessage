using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace bitmessage.network
{
	public class Header
	{
		public readonly string Command;
		public readonly UInt32 Length;
		public readonly byte[] Checksum;

		public Header(BinaryReader br)
		{
			ReadHeaderMagic(br);
			Command = ReadHeaderCommand(br);
			Length = BitConverter.ToUInt32(br.ReadBytes(4).ReverseIfNeed(), 0);
			Checksum = br.ReadBytes(4);
		}

		private static void ReadHeaderMagic(BinaryReader br)
		{
			Debug.Write("ReadHeaderMagic ");
			do
			{
				do
				{
					do
					{
						do
						{
						} while (br.ReadByte() != 0xE9);
					} while (br.ReadByte() != 0xBE);
				} while (br.ReadByte() != 0xB4);
			} while (br.ReadByte() != 0xD9);
			Debug.WriteLine(" - ok");
		}


		private static string ReadHeaderCommand(BinaryReader br)
		{
			Debug.Write("ReadHeaderCommand - ");

			byte[] bytes = br.ReadBytes(12);

			StringBuilder result = new StringBuilder(12);
			foreach (byte b in bytes)
			{
				if (b == 0) break;
				result.Append(Encoding.ASCII.GetChars(new[] {b}));
			}
			Debug.WriteLine("ReadHeaderCommand - " + result);
			return result.ToString();
		}
	}
}
