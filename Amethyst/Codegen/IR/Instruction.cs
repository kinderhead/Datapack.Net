﻿using Amethyst.AST;
using Datapack.Net.Data;
using Datapack.Net.Function;
using Datapack.Net.Function.Commands;
using Datapack.Net.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amethyst.Codegen.IR
{
	public abstract class Instruction(LocationRange loc)
	{
		public readonly LocationRange Location = loc;
		public FunctionContext.Frame Ctx { get => ctx ?? throw new InvalidOperationException(); set => ctx = value; }
		public readonly List<Command> Commands = [];

		private FunctionContext.Frame? ctx = null;

		protected void Prepend(Command cmd) => Commands.Insert(0, cmd);
		protected void Add(Command cmd) => Commands.Add(cmd);
		protected void Add(IEnumerable<Command> cmd) => Commands.AddRange(cmd);

		public abstract void Build();
	}

	public class RawCommandInstruction(LocationRange loc, string cmd, bool macro = false) : Instruction(loc)
	{
		public readonly string Command = cmd;
		public readonly bool Macro = macro;

		public override void Build()
		{
			Add(new RawCommand(Command, Macro));
		}
	}

	public class StoreInstruction(LocationRange loc, MutableValue dest, Value val) : Instruction(loc)
	{
		public readonly MutableValue Dest = dest;
		public readonly Value Value = val;

		public override void Build()
		{
			Add(Value.ReadTo(Dest));
		}
	}

	public class StoreAsTypeInstruction(LocationRange loc, StorageValue dest, Value val, NBTNumberType type) : Instruction(loc)
	{
		public readonly StorageValue Dest = dest;
		public readonly Value Value = val;
		public readonly NBTNumberType Type = type;

		public override void Build()
		{
			Add(new Execute().Store(Dest.Storage, Dest.Path, Type, 1).Run(Value.Get()));
		}
	}

	public class StoreIfSuccessInstruction(LocationRange loc, Execute exec, MutableValue dest, Value val) : Instruction(loc)
	{
		public readonly Execute Command = exec;
		public readonly MutableValue Dest = dest;
		public readonly Value Value = val;

		public override void Build()
		{
			Add(Command.Run(Value.ReadTo(Dest)));
		}
	}

	public class ScoreOperationInstruction(LocationRange loc, ScoreValue dest, ScoreOperation op, ScoreValue val) : Instruction(loc)
	{
		public readonly ScoreValue Dest = dest;
		public readonly ScoreOperation Op = op;
		public readonly ScoreValue Value = val;

		public override void Build()
		{
			Add(new Scoreboard.Players.Operation(Dest.Target, Dest.Score, Op, Value.Target, Value.Score));
		}
	}

	public class CallInstruction(LocationRange loc, NamespacedID id, Dictionary<string, Value> args, Dictionary<string, Value> macros) : Instruction(loc)
	{
		public readonly NamespacedID ID = id;
		public readonly Dictionary<string, Value> Arguments = args;
		public readonly Dictionary<string, Value> Macros = macros;

		public override void Build()
		{
			var args = new StorageValue(Compiler.RuntimeID, "tmp_args", new PrimitiveTypeSpecifier(NBTType.Compound));
			var macros = new StorageValue(Compiler.RuntimeID, "tmp_macros", new PrimitiveTypeSpecifier(NBTType.Compound));
			Add(PopulateInstruction.Apply(args, Arguments, out var actualArgs, out bool argsShouldMacro));
			Add(PopulateInstruction.Apply(macros, Macros, out var actualMacros, out bool shouldMacro));

			if (actualArgs.AsConstant() is NBTCompound a) Add(new DataCommand.Modify(new Storage(Compiler.RuntimeID), "stack", argsShouldMacro).Append().Value(a.ToString()));
			else if (Arguments.Count != 0) Add(new DataCommand.Modify(new Storage(Compiler.RuntimeID), "stack").Append().From(args.Storage, args.Path));

			// TODO: Fix this later
			if (actualMacros.AsConstant() is NBTCompound c) Add(new FunctionCommand(new(ID, true), c, shouldMacro));
			else if (Macros.Count != 0) Add(new FunctionCommand(new(ID, true), macros.Storage, macros.Path));
			else Add(new FunctionCommand(new(ID, true)));
		}
	}

	public class CallMacroInstruction(LocationRange loc, NamespacedID id, StorageValue val) : Instruction(loc)
	{
		public readonly NamespacedID ID = id;
		public readonly StorageValue Value = val;

		public override void Build()
		{
			// Fix this later
			Add(new FunctionCommand(new(ID, true), Value.Storage, Value.Path));
		}
	}

	public class TellrawInstruction(LocationRange loc, FormattedText text) : Instruction(loc)
	{
		public readonly FormattedText Text = text;

		public override void Build()
		{
			Add(new TellrawCommand(new TargetSelector(TargetType.a), Text));
		}
	}

	public class CompareJumpInstruction(LocationRange loc, Execute cmd, SubFunction func) : Instruction(loc)
	{
		public readonly Execute Command = cmd;
		public readonly SubFunction Func = func;

		public override void Build()
		{
			Add(Func.Call(Command));
		}
	}

	// public class StackPushInstruction(LocationRange loc, StorageValue val) : Instruction(loc)
	// {
	// 	public readonly StorageValue Value = val;

	// 	public override void Build()
	// 	{
	// 		Add(new DataCommand.Modify(new Storage(Compiler.RuntimeID), "stack").Append().From(Value.Storage, Value.Path));
	// 	}
	// }
}
