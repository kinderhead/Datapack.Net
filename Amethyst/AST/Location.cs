﻿using Antlr4.Runtime;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amethyst.AST
{
	public readonly record struct Location(string File, int Line, int Column)
	{
		public static Location From(string path, IToken tok) => new(path, tok.Line, tok.Column + 1);

		public override string ToString() => $"{File}: {Line}:{Column}";
	}

	public readonly record struct LocationRange(Location Start, Location End)
	{
		public static LocationRange From(string path, ParserRuleContext ctx)
		{
			var stopToken = ctx.Stop;
			// Might have to fix if there are multiline tokens, but I don't think there are any
			int endColumn = stopToken.Column + stopToken.StopIndex - stopToken.StartIndex + 1;
			var end = new Location(path, stopToken.Line, endColumn);
			return new LocationRange(Location.From(path, ctx.Start), end);
		}

		public override string ToString()
		{
			if (Start.Line == End.Line) return $"{Start.File}: {Start.Line}:{Start.Column}-{End.Column}";
			else return $"{Start.File}: {Start.Line}:{Start.Column}-{End.Line}:{End.Column}";
		}
	}

	public interface ILocatable
	{
		public LocationRange Location { get; }
	}
}
