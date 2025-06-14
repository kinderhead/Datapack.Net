﻿using CommandLine;

namespace Amethyst
{
	internal class Program
	{
		static void Main(string[] args)
		{
#if DEBUG
			Console.Clear(); // Thanks Visual Studio for being sad
#endif
			if (!new Compiler(args).Compile()) Environment.Exit(1);
		}
	}
}
