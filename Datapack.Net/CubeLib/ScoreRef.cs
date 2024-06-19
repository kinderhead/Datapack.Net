﻿using Datapack.Net.CubeLib.Utils;
using Datapack.Net.Function;
using Datapack.Net.Function.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datapack.Net.CubeLib
{
    public class ScoreRef(Score score, IEntityTarget target) : IRuntimeArgument
    {
        public readonly Score Score = score;
        public readonly IEntityTarget Target = target.RequireOne();

        public void Set(int val) => Project.ActiveProject.AddCommand(new Scoreboard.Players.Set(Target, Score, val));
        public void Set(ScoreRef score) => Project.ActiveProject.AddCommand(new Scoreboard.Players.Operation(Target, Score, ScoreOperation.Assign, score.Target, score.Score));
        public void Set(ScoreRefOperation op) => op.Process(this);
        public void Add(int val) => Project.ActiveProject.AddCommand(new Scoreboard.Players.Add(Target, Score, val));
        public void Add(ScoreRef val) => Op(val, ScoreOperation.Add);
        public void Sub(int val) => Project.ActiveProject.AddCommand(new Scoreboard.Players.Remove(Target, Score, val));
        public void Sub(ScoreRef val) => Op(val, ScoreOperation.Sub);
        public void Mul(int val) => Op(val, ScoreOperation.Mul);
        public void Mul(ScoreRef val) => Op(val, ScoreOperation.Mul);
        public void Div(int val) => Op(val, ScoreOperation.Div);
        public void Div(ScoreRef val) => Op(val, ScoreOperation.Div);
        public void Mod(int val) => Op(val, ScoreOperation.Mod);
        public void Mod(ScoreRef val) => Op(val, ScoreOperation.Mod);

        public Scoreboard.Players.Get Get() => new(Target, Score);
        public Scoreboard.Players.Set SetCmd(int val) => new(Target, Score, val);

        public Execute Store(bool macro = false) => new Execute(macro).Store(this);

        public void Op(int val, ScoreOperation op)
        {
            Op(Project.ActiveProject.Constant(val), op);
        }

        public void Op(ScoreRef val, ScoreOperation op)
        {
            Project.ActiveProject.AddCommand(new Scoreboard.Players.Operation(Target, Score, op, val.Target, val.Score));
        }

        public ScoreRefComparison Exists() => this >= -2147483648;

        public static ScoreRefComparison operator ==(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Op = Comparison.Equal };
        public static ScoreRefComparison operator ==(int a, ScoreRef b) => new() { Left = a, RightScore = b, Op = Comparison.Equal };
        public static ScoreRefComparison operator ==(ScoreRef a, int b) => new() { LeftScore = a, Right = b, Op = Comparison.Equal };

        public static ScoreRefComparison operator !=(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Op = Comparison.Equal, If = false };
        public static ScoreRefComparison operator !=(int a, ScoreRef b) => new() { Left = a, RightScore = b, Op = Comparison.Equal, If = false };
        public static ScoreRefComparison operator !=(ScoreRef a, int b) => new() { LeftScore = a, Right = b, Op = Comparison.Equal, If = false };

        public static ScoreRefComparison operator >(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Op = Comparison.GreaterThan};
        public static ScoreRefComparison operator >(int a, ScoreRef b) => new() { Left = a, RightScore = b, Op = Comparison.GreaterThan};
        public static ScoreRefComparison operator >(ScoreRef a, int b) => new() { LeftScore = a, Right = b, Op = Comparison.GreaterThan};

        public static ScoreRefComparison operator <(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Op = Comparison.LessThan};
        public static ScoreRefComparison operator <(int a, ScoreRef b) => new() { Left = a, RightScore = b, Op = Comparison.LessThan};
        public static ScoreRefComparison operator <(ScoreRef a, int b) => new() { LeftScore = a, Right = b, Op = Comparison.LessThan};

        public static ScoreRefComparison operator >=(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Op = Comparison.GreaterThanOrEqual};
        public static ScoreRefComparison operator >=(int a, ScoreRef b) => new() { Left = a, RightScore = b, Op = Comparison.GreaterThanOrEqual};
        public static ScoreRefComparison operator >=(ScoreRef a, int b) => new() { LeftScore = a, Right = b, Op = Comparison.GreaterThanOrEqual};

        public static ScoreRefComparison operator <=(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Op = Comparison.LessThanOrEqual};
        public static ScoreRefComparison operator <=(int a, ScoreRef b) => new() { Left = a, RightScore = b, Op = Comparison.LessThanOrEqual};
        public static ScoreRefComparison operator <=(ScoreRef a, int b) => new() { LeftScore = a, Right = b, Op = Comparison.LessThanOrEqual};

        public static ScoreRefOperation operator +(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Operation = ScoreOperation.Add };
        public static ScoreRefOperation operator +(ScoreRefOperation a, ScoreRef b) => new() { LeftBranch = a, RightScore = b, Operation = ScoreOperation.Add };
        public static ScoreRefOperation operator +(ScoreRef a, ScoreRefOperation b) => new() { LeftScore = a, RightBranch = b, Operation = ScoreOperation.Add };
        public static ScoreRefOperation operator +(int a, ScoreRef b) => new() { LeftConst = a, RightScore = b, Operation = ScoreOperation.Add };
        public static ScoreRefOperation operator +(ScoreRef a, int b) => new() { LeftScore = a, RightConst = b, Operation = ScoreOperation.Add };

        public static ScoreRefOperation operator -(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Operation = ScoreOperation.Sub };
        public static ScoreRefOperation operator -(ScoreRefOperation a, ScoreRef b) => new() { LeftBranch = a, RightScore = b, Operation = ScoreOperation.Sub };
        public static ScoreRefOperation operator -(ScoreRef a, ScoreRefOperation b) => new() { LeftScore = a, RightBranch = b, Operation = ScoreOperation.Sub };
        public static ScoreRefOperation operator -(int a, ScoreRef b) => new() { LeftConst = a, RightScore = b, Operation = ScoreOperation.Sub };
        public static ScoreRefOperation operator -(ScoreRef a, int b) => new() { LeftScore = a, RightConst = b, Operation = ScoreOperation.Sub };

        public static ScoreRefOperation operator *(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Operation = ScoreOperation.Mul };
        public static ScoreRefOperation operator *(ScoreRefOperation a, ScoreRef b) => new() { LeftBranch = a, RightScore = b, Operation = ScoreOperation.Mul };
        public static ScoreRefOperation operator *(ScoreRef a, ScoreRefOperation b) => new() { LeftScore = a, RightBranch = b, Operation = ScoreOperation.Mul };
        public static ScoreRefOperation operator *(int a, ScoreRef b) => new() { LeftConst = a, RightScore = b, Operation = ScoreOperation.Mul };
        public static ScoreRefOperation operator *(ScoreRef a, int b) => new() { LeftScore = a, RightConst = b, Operation = ScoreOperation.Mul };

        public static ScoreRefOperation operator /(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Operation = ScoreOperation.Div };
        public static ScoreRefOperation operator /(ScoreRefOperation a, ScoreRef b) => new() { LeftBranch = a, RightScore = b, Operation = ScoreOperation.Div };
        public static ScoreRefOperation operator /(ScoreRef a, ScoreRefOperation b) => new() { LeftScore = a, RightBranch = b, Operation = ScoreOperation.Div };
        public static ScoreRefOperation operator /(int a, ScoreRef b) => new() { LeftConst = a, RightScore = b, Operation = ScoreOperation.Div };
        public static ScoreRefOperation operator /(ScoreRef a, int b) => new() { LeftScore = a, RightConst = b, Operation = ScoreOperation.Div };

        public static ScoreRefOperation operator %(ScoreRef a, ScoreRef b) => new() { LeftScore = a, RightScore = b, Operation = ScoreOperation.Mod };
        public static ScoreRefOperation operator %(ScoreRefOperation a, ScoreRef b) => new() { LeftBranch = a, RightScore = b, Operation = ScoreOperation.Mod };
        public static ScoreRefOperation operator %(ScoreRef a, ScoreRefOperation b) => new() { LeftScore = a, RightBranch = b, Operation = ScoreOperation.Mod };
        public static ScoreRefOperation operator %(int a, ScoreRef b) => new() { LeftConst = a, RightScore = b, Operation = ScoreOperation.Mod };
        public static ScoreRefOperation operator %(ScoreRef a, int b) => new() { LeftScore = a, RightConst = b, Operation = ScoreOperation.Mod };

        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public ScoreRef GetAsArg() => this;

        public static IRuntimeArgument Create(ScoreRef arg) => new ScoreRef(arg.Score, arg.Target);

        //public static implicit operator ScoreRef(int val)
        //{
        //    return new()
        //}
    }
}
