﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Datapack.Net.CubeLib
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DeclareMCAttribute(string name) : Attribute
    {
        public readonly string Name = name;

        public static DeclareMCAttribute Get(Action func)
        {
            return func.Method.GetCustomAttribute<DeclareMCAttribute>() ?? throw new InvalidOperationException("Function does not have the DeclareMC attribute");
        }
    }
}