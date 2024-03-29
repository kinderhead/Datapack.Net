﻿using Datapack.Net.Pack;
using Datapack.Net.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datapack.Net.Function
{
    public class MCFunction(NamespacedID id) : Resource(id)
    {
        public bool Macro { get; protected set; }

        protected List<Command> commands = [];

        public void Add(Command command)
        {
            commands.Add(command);
            if (command.Macro) Macro = true;
        }

        public override string Build(Datapack pack)
        {
            StringBuilder sb = new();
            foreach (var i in commands)
            {
                sb.AppendLine(i.Build());
            }
            return sb.ToString();
        }
    }
}
