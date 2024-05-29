﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Datapack.Net.Function
{
    public abstract partial class Command(bool macro)
    {
        public static readonly Regex MacroRegex = MacroRegexGet();

        public bool Macro = macro;

        public string Build()
        {
            var txt = PreBuild().Trim();

            if (Macro && MacroRegex.IsMatch(txt)) txt = "$" + txt;
            return txt;
        }

        protected abstract string PreBuild();

        public override string ToString()
        {
            return Build();
        }

        [GeneratedRegex(@"\$\((.*?)\)")]
        private static partial Regex MacroRegexGet();
    }
}
