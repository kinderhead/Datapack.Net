﻿using Datapack.Net.Data;
using Datapack.Net.Function.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Datapack.Net.Function.Commands.Execute;

namespace Datapack.Net.CubeLib
{
    public class ScoreRefComparison : Conditional
    {
        public ScoreRef? LeftScore;
        public ScoreRef? RightScore;
        public int Left;
        public int Right;
        public Comparison Op;

        public override Execute Process(Execute cmd, int tmp = 0)
        {
            Execute.Conditional branch = If ? cmd.If : cmd.Unless;

            ScoreRef a = LeftScore ?? Project.ActiveProject.Constant(Left);
            ScoreRef b = RightScore ?? Project.ActiveProject.Constant(Right);

            branch.Score(a.Target, a.Score, Op, b.Target, b.Score);

            return cmd;
        }
    }

    public class ScoreRefMatches : Conditional
    {
        public ScoreRef Score;
        public MCRange<int> Range;

        public override Execute Process(Execute cmd, int tmp = 0)
        {
            Execute.Conditional branch = If ? cmd.If : cmd.Unless;
            branch.Score(Score.Target, Score.Score, Range);
            return cmd;
        }
    }
}
