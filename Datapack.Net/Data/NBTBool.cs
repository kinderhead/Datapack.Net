﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datapack.Net.Data
{
    public class NBTBool(bool val) : NBTValue
    {
		public override NBTType Type => NBTType.Boolean;
		public readonly bool Value = val;

        public override void Build(StringBuilder sb)
        {
            sb.Append(Value ? "true" : "false");
        }

        public static implicit operator NBTBool(bool val) => new(val);
        public static implicit operator bool(NBTBool val) => val.Value;
    }
}
