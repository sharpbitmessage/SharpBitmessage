using System;
using bitmessage;

namespace test
{
	class Program
	{
		static void Main(string[] args)
		{
			var bm = new Bitmessage();
			Console.ReadLine();
			Console.WriteLine(bm.ToString());
		}
	}
}
