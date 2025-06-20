﻿using Amethyst.AST;
using Amethyst.Codegen.IR;
using Amethyst.Errors;
using Datapack.Net.Data;
using Datapack.Net.Function;
using Datapack.Net.Function.Commands;
using Datapack.Net.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amethyst.Codegen
{
	public abstract class Value(TypeSpecifier type)
	{
		public TypeSpecifier Type = type;
		public virtual bool IsMacro => false;

		public abstract Command AppendTo(Storage storage, string path);
		public abstract Command ReadTo(Storage storage, string path);
		public abstract Command ReadTo(IEntityTarget target, Score score);
		public abstract Command Get();
		public abstract FormattedText Format(FormattedText text);

		public virtual Command ReadTo(MutableValue val)
		{
			if (val is StorageValue s) return ReadTo(s.Storage, s.Path);
			else if (val is ScoreValue c) return ReadTo(c.Target, c.Score);
			else throw new NotImplementedException();
		}

		public virtual ScoreValue AsScore(FunctionContext ctx)
		{
			//if (!NBTValue.IsOperableType(Type.Type)) throw new InvalidOperationException();
			var tmp = ctx.AllocTempScore();
			tmp.Store(ctx, this);
			return tmp;
		}

		public virtual StorageValue AsStorage(FunctionContext ctx)
		{
			var tmp = ctx.AllocTemp(Type);
			tmp.Store(ctx, this);
			return tmp;
		}

		public virtual MutableValue GetProperty(FunctionContext ctx, string prop) => throw new InvalidTypeError(ctx.CurrentLocator.Location, Type.ToString());

		public virtual NBTValue? AsConstant() => null;

		public virtual Value AsNotScore(FunctionContext ctx) => this;
	}

	public class LiteralValue(NBTValue val, TypeSpecifier? type = null) : Value(type ?? new PrimitiveTypeSpecifier(val.Type))
	{
		public readonly NBTValue Value = val;

        public int ToScoreInt()
		{
			if (NBTValue.IsOperableType(Value.Type) && Value is INBTNumber num) return Convert.ToInt32(num.RawValue);
			else if (Value is ICollection l) return l.Count;
			else if (Value is NBTString s) return s.Value.Length;
			throw new NotImplementedException();
		}

		public override FormattedText Format(FormattedText text) => Value is NBTString str ? text.Text(str.Value) : text.Text(Value.ToString());
		public override Command Get() => throw new InvalidOperationException(); // Maybe implement this
		public override Command ReadTo(Storage storage, string path) => new DataCommand.Modify(storage, path).Set().Value(Value.ToString());
		public override Command ReadTo(IEntityTarget target, Score score) => new Scoreboard.Players.Set(target, score, ToScoreInt());
		public override Command AppendTo(Storage storage, string path) => new DataCommand.Modify(storage, path).Append().Value(Value.ToString());
		public override NBTValue? AsConstant() => Value;

		public override ScoreValue AsScore(FunctionContext ctx)
		{
			if (ctx.Compiler.Options.NoConstantScores || Value is not NBTInt i) return base.AsScore(ctx);
			else return ctx.Constant(i.Value);
        }

		// public override MutableValue GetProperty(FunctionContext ctx, string prop)
		// {
		// 	if (Value is not NBTCompound nbt || !nbt.ContainsKey(prop)) return base.GetProperty(ctx, prop);
		// 	else return 
        // }

		public static LiteralValue BooleanEquivalent(FunctionContext ctx, NBTType type, bool value)
		{
			if (type == NBTType.Boolean)
			{
				if (value) return new(new NBTBool(true));
				else return new(new NBTBool(false));

			}
			else if (type == NBTType.Byte)
			{
				if (value) return new(new NBTByte(1));
				else return new(new NBTByte(0));
			}
			else if (type == NBTType.Short)
			{
				if (value) return new(new NBTShort(1));
				else return new(new NBTShort(0));
			}
			else if (type == NBTType.Int)
			{
				if (value) return new(new NBTInt(1));
				else return new(new NBTInt(0));
			}
			else throw new InvalidTypeError(ctx.CurrentLocator.Location, Enum.GetName(type)?.ToLower() ?? "void");
		}
    }

	public class VoidValue() : Value(new VoidTypeSpecifier())
	{
		public override Command AppendTo(Storage storage, string path) => throw new InvalidOperationException();
		public override FormattedText Format(FormattedText text) => text.Text("void");
		public override Command Get() => throw new InvalidOperationException();
		public override Command ReadTo(Storage storage, string path) => throw new InvalidOperationException();
		public override Command ReadTo(IEntityTarget target, Score score) => throw new InvalidOperationException();
	}

	public class StaticFunctionValue(NamespacedID id, FunctionTypeSpecifier type) : LiteralValue(new NBTString(id.ToString()), type)
	{
		public readonly NamespacedID ID = id;
		public FunctionTypeSpecifier FuncType => (FunctionTypeSpecifier)Type;
	}

    public class MacroValue(string name, TypeSpecifier type) : Value(type)
    {
		public readonly string Name = name;
        public override bool IsMacro => true;

		public override Command AppendTo(Storage storage, string path) => new DataCommand.Modify(storage, path, true).Append().Value($"$({Name})");
		public override FormattedText Format(FormattedText text)
		{
			text.Macro = true;
			return text.Text($"$({Name})");
		}

        public override Command Get() => throw new NotImplementedException();
		public override NBTValue? AsConstant() => new NBTRawString($"$({Name})");
		public override Command ReadTo(Storage storage, string path) => new DataCommand.Modify(storage, path, true).Set().Value($"$({Name})");
		public override Command ReadTo(IEntityTarget target, Score score) => new Scoreboard.Players.Set(target, score, $"$({Name})");
    }

    public abstract class MutableValue(TypeSpecifier type) : Value(type)
	{
		public abstract void Store(FunctionContext ctx, Value val);
		public abstract void Store(FunctionContext ctx, Value val, NBTNumberType type);

		public abstract Execute WriteFrom(Execute cmd, bool result = true);
	}

	public class StorageValue(Storage storage, string path, TypeSpecifier type) : MutableValue(type)
	{
		public readonly Storage Storage = storage;
		public readonly string Path = path;

		public override FormattedText Format(FormattedText text) => text.Storage(Storage, Path);
		public override Command Get() => new DataCommand.Get(Storage, Path);

		public override void Store(FunctionContext ctx, Value val)
		{
			ctx.Add(new StoreInstruction(ctx.CurrentLocator.Location, this, val));
		}

		public override void Store(FunctionContext ctx, Value val, NBTNumberType type)
		{
			ctx.Add(new StoreAsTypeInstruction(ctx.CurrentLocator.Location, this, val, type));
		}

		public override Command ReadTo(Storage storage, string path) => new DataCommand.Modify(storage, path).Set().From(Storage, Path);
		public override Command ReadTo(IEntityTarget target, Score score)
		{
			//if (!NBTValue.IsOperableType(Type.Type)) throw new InvalidOperationException();

			return new Execute().Store(target, score).Run(Get());
		}

		public override Execute WriteFrom(Execute cmd, bool result = true) => cmd.Store(Storage, Path, (NBTNumberType)Type.EffectiveType, 1, result);
		public override Command AppendTo(Storage storage, string path) => new DataCommand.Modify(storage, path).Append().From(Storage, Path);
		public override StorageValue AsStorage(FunctionContext ctx) => this;

		public override MutableValue GetProperty(FunctionContext ctx, string prop)
		{
			if (Type.Property(prop) is not TypeSpecifier t) return base.GetProperty(ctx, prop);
			return new StorageValue(Storage, Path + $".{prop}", t);
        }
	}

	public class ScoreValue(IEntityTarget target, Score score) : MutableValue(new PrimitiveTypeSpecifier(NBTType.Int))
	{
		public readonly IEntityTarget Target = target;
		public readonly Score Score = score;

		public override Command Get() => new Scoreboard.Players.Get(Target, Score);
		public override void Store(FunctionContext ctx, Value val) => ctx.Add(new StoreInstruction(ctx.CurrentLocator.Location, this, val));
		public override void Store(FunctionContext ctx, Value val, NBTNumberType type) => Store(ctx, val);
		public override Command ReadTo(Storage storage, string path) => new Execute().Store(storage, path, NBTNumberType.Int, 1).Run(Get());
		public override Command ReadTo(IEntityTarget target, Score score) => new Scoreboard.Players.Operation(target, score, ScoreOperation.Assign, Target, Score);
		public override ScoreValue AsScore(FunctionContext ctx) => this;
		public override FormattedText Format(FormattedText text) => text.Score(Target, Score);
		public override Execute WriteFrom(Execute cmd, bool result = true) => cmd.Store(Target, Score, result);
		public override Command AppendTo(Storage storage, string path) => throw new InvalidOperationException();

		public override Value AsNotScore(FunctionContext ctx)
		{
			var tmp = ctx.AllocTemp(Type);
			tmp.Store(ctx, this);
			return tmp;
		}
	}
}
