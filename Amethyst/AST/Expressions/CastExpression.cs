﻿using Amethyst.Codegen;
using Amethyst.Codegen.IR;
using Amethyst.Errors;
using Datapack.Net.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amethyst.AST.Expressions
{
	public class CastExpression(LocationRange loc, TypeSpecifier type, Expression expr) : Expression(loc)
	{
		public readonly TypeSpecifier Type = type;
		public readonly Expression Expression = expr;

		protected override TypeSpecifier _ComputeType(FunctionContext ctx) => Type;

		protected override Value _Execute(FunctionContext ctx)
		{
			var src = Expression.Execute(ctx);
			var ret = new ReturnOrStore(ctx, Type);
			src.Type.Cast(ctx, src, ret);
			return ret.EffectiveValue;
		}

		protected override void _Store(FunctionContext ctx, MutableValue val)
		{
			var ctype = Expression.ComputeType(ctx);
			if (ctype == Type || (ctype.EffectiveType == NBTType.List && val.Type.IsList))
			{
				Expression.Store(ctx, val);
				return;
			}

			if (val.Type != Type) throw new InvalidTypeError(Location, val.Type.ToString(), Type.ToString());

			var src = Expression.Execute(ctx);
			src.Type.Cast(ctx, src, new(ctx, val));
		}

		// private (Value val, Value? post) PreExecute(FunctionContext ctx)
		// {
		// 	var val = Expression.Execute(ctx);
		// 	if (Type == val.Type) return (val, val);
		// 	else if (Type is VoidTypeSpecifier || Type is VarTypeSpecifier) throw new InvalidCastError(Location, val.Type, Type);
		// 	else if (val is LiteralValue l)
		// 	{
		// 		if (l.Value is NBTList list && Type is ListTypeSpecifier lType)
		// 		{
		// 			foreach (var i in list)
		// 			{
		// 				if (i.Type != lType.Inner.EffectiveType) return (val, null);
		// 			}

		// 			return (val, new LiteralValue(list, lType));
		// 		}
		// 		else if (l.Value is INBTNumber num && NBTValue.IsNumberType(Type.EffectiveType))
		// 		{
		// 			switch (Type.EffectiveType)
		// 			{
		// 				case NBTType.Boolean:
		// 					return (val, new LiteralValue(new NBTBool(Convert.ToBoolean(num.RawValue))));
		// 				case NBTType.Byte:
		// 					return (val, new LiteralValue(new NBTByte(Convert.ToByte(num.RawValue))));
		// 				case NBTType.Short:
		// 					return (val, new LiteralValue(new NBTShort(Convert.ToInt16(num.RawValue))));
		// 				case NBTType.Int:
		// 					return (val, new LiteralValue(new NBTInt(Convert.ToInt32(num.RawValue))));
		// 				case NBTType.Long:
		// 					return (val, new LiteralValue(new NBTLong(Convert.ToInt64(num.RawValue))));
		// 				case NBTType.Float:
		// 					return (val, new LiteralValue(new NBTFloat(Convert.ToSingle(num.RawValue))));
		// 				case NBTType.Double:
		// 					return (val, new LiteralValue(new NBTDouble(Convert.ToDouble(num.RawValue))));
		// 				default:
		// 					break;
		// 			}
		// 		}
		// 	}
		// 	return (val, null);
		// }
	}
}
