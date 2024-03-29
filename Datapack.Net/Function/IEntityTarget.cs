﻿using Datapack.Net.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datapack.Net.Function
{
    public interface IEntityTarget
    {
        public string Get();

        public bool IsOne();

        public IEntityTarget RequireOne()
        {
            if (!IsOne())
            {
                throw new ArgumentException($"Entity target is not singular: {Get()}");
            }

            return this;
        }
    }

    public class NamedTarget(string name) : IEntityTarget
    {
        public readonly string Name = name;

        public string Get()
        {
            return Name;
        }

        public bool IsOne()
        {
            return true;
        }
    }
}
