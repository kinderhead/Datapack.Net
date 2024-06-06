﻿using Datapack.Net.CubeLib.Builtins.Static;
using Datapack.Net.CubeLib.Utils;
using Datapack.Net.Data;
using Datapack.Net.Function;
using Datapack.Net.Function.Commands;
using Datapack.Net.Pack;
using Datapack.Net.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Datapack.Net.Data._1_20_4.Blocks;
using static Datapack.Net.Function.Commands.Execute.Subcommand;

namespace Datapack.Net.CubeLib
{
    public abstract partial class Project
    {
        public static Project ActiveProject { get; private set; }
        public static readonly List<Project> Projects = [];
        public static readonly Storage GlobalStorage = new(new("cubelib", "global"));
        public static readonly NamedTarget GlobalScoreEntity = new("#_cubelib_score");

        internal static Dictionary<Delegate, MCFunction> RuntimeMethods = [];

        public readonly DP Datapack;
        public abstract string Namespace { get; }

        private readonly Dictionary<Delegate, MCFunction> MCFunctions = [];
        private readonly List<MCFunction> MiscMCFunctions = [];
        private readonly Queue<KeyValuePair<Delegate, MCFunction>> FunctionsToProcess = [];

        private readonly HashSet<Score> Scores = [];
        private readonly HashSet<Score> Registers = [];
        private readonly HashSet<Score> Globals = [];
        private readonly List<IStaticType> StaticTypes = [];
        private readonly List<Command> MiscInitCmds = [];
        private readonly Dictionary<int, ScoreRef> Constants = [];

        private MCFunction? currentTarget;
        public MCFunction CurrentTarget { get => currentTarget ?? throw new InvalidOperationException("Project not building yet"); private set => currentTarget = value; }
        public MCFunction CurrentTargetCleanup { get; private set; }
        public DeclareMCAttribute CurrentFunctionAttrs { get; private set; }

        private List<ScoreRef> RegistersInUse = [];

        public readonly NamedTarget ScoreEntity;
        public readonly Storage InternalStorage;

        public MCStaticStack RegisterStack;
        public MCStaticStack ArgumentStack;
        public MCStaticHeap Heap;

        private int AnonymousFuncCounter = 0;

        public readonly List<Project> Dependencies = [];

        private bool BuiltOrBuilding = false;

        public CubeLibStd Std;

        public bool ErrorChecking = false;
        public ScoreRef ErrorScore;

        protected Project(DP pack)
        {
            Datapack = pack;
            ScoreEntity = new($"#_{Namespace}_cubelib_score");
            InternalStorage = new(new(Namespace, "_internal"));
        }

        public static T Create<T>(DP pack) where T : Project
        {
            T project = (T?)Activator.CreateInstance(typeof(T), [pack]) ?? throw new ArgumentException("Invalid project constructor");
            var index = Projects.FindIndex((i) => i.GetType() == project.GetType());
            if (index == -1)
            {
                Projects.Add(project);
                return project;
            }
            return (T)Projects[index];
        }

        public T AddDependency<T>() where T : Project
        {
            var project = Create<T>(Datapack);
            project.ErrorChecking = ErrorChecking;
            project.Build();
            ActiveProject = this;
            Dependencies.Add(project);
            return project;
        }

        public void Build()
        {
            if (BuiltOrBuilding) return;
            BuiltOrBuilding = true;
            ActiveProject = this;

            Std = AddDependency<CubeLibStd>();
            Init();

            foreach (var i in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                if (i.GetCustomAttribute<DeclareMCAttribute>() is not null) AddFunction(DelegateUtils.Create(i, this));
            }

            var tags = Datapack.GetResource<Tags>();
            var mainTag = tags.GetTag(new("minecraft", "load"), "functions");
            var tickTag = tags.GetTag(new("minecraft", "tick"), "functions");

            mainTag.Values.Add(new(Namespace, "_main"));
            tickTag.Values.Add(new(Namespace, "_tick"));

            RegisterStack = new(GlobalStorage, "register_stack");
            AddStaticObject(RegisterStack);
            ArgumentStack = new(GlobalStorage, "argument_stack");
            AddStaticObject(ArgumentStack);
            Heap = new(GlobalStorage, "heap");
            AddStaticObject(Heap);

            if (ErrorChecking)
            {
                var score = new Score("_cl_err", "dummy");
                Scores.Add(score);
                ErrorScore = new(score, GlobalScoreEntity);
            }

            while (FunctionsToProcess.TryDequeue(out var i))
            {
                RegistersInUse.Clear();
                CurrentTarget = i.Value;
                CurrentTargetCleanup = new MCFunction(new(i.Value.ID.Namespace, $"zz_cleanup/{i.Value.ID.Path}"), true);
                CurrentFunctionAttrs = DeclareMCAttribute.Get(i.Key);
                MiscMCFunctions.Add(CurrentTargetCleanup);

                var args = DeclareMCAttribute.Args(i.Key);
                if (args.Length == 0) i.Key.DynamicInvoke();
                else
                {
                    List<IRuntimeArgument> funcArgs = [];

                    foreach (var e in args)
                    {
                        var arg = ArgumentStack.Dequeue();

                        if (e.IsAssignableTo(typeof(IBaseRuntimeObject))) funcArgs.Add(IBaseRuntimeObject.Create(arg, e));
                        else funcArgs.Add((IRuntimeArgument?)e.GetMethod("Create")?.Invoke(null, [arg]) ?? throw new ArgumentException($"Invalid arguments for function {i.Key.Method.Name}"));
                    }

                    i.Key.DynamicInvoke([.. funcArgs]);
                }

                RegistersInUse.Reverse();

                WithCleanup(() =>
                {
                    foreach (var r in RegistersInUse)
                    {
                        RegisterStack.Dequeue(r);
                    }
                });

                Call(CurrentTargetCleanup);
            }

            // Prepend Main
            foreach (var i in StaticTypes)
            {
                i.Init();
            }

            foreach (var i in Constants)
            {
                PrependMain(new Scoreboard.Players.Set(i.Value.Target, i.Value.Score, i.Key));
            }

            foreach (var i in MiscInitCmds)
            {
                PrependMain(i);
            }

            foreach (var i in Registers)
            {
                PrependMain(new Scoreboard.Players.Set(ScoreEntity, i, 0));
            }

            foreach (var i in Scores.Reverse())
            {
                PrependMain(new Scoreboard.Objectives.Add(i));
            }

            foreach (var i in MCFunctions.Values)
            {
                Datapack.GetResource<Functions>().Add(i);
            }

            foreach (var i in MiscMCFunctions)
            {
                Datapack.GetResource<Functions>().Add(i);
            }
        }

        public MCFunction GuaranteeFunc(Delegate func, bool macro)
        {
            var attr = DeclareMCAttribute.Get(func);
            MCFunction retFunc;
            if (func.Target != this && func.Target is Project lib)
            {
                if (lib.BuiltOrBuilding) return lib.GuaranteeFunc(func, macro);
                retFunc = new(new(lib.Namespace, attr.Path), true);
            }
            else if (FindFunction(func) is MCFunction mcfunc) retFunc = mcfunc;
            else retFunc = AddFunction(func);

            if (macro && attr.Macros.Length == 0) throw new ArgumentException("Attempted to call a non macro function with arguments");
            if (!macro && attr.Macros.Length != 0) throw new ArgumentException("Attempted to call a macro function without arguments");

            return retFunc;
        }

        public void Call(Action func, bool macro = false) => Call(GuaranteeFunc(func, false), macro);
        public void Call(MCFunction func, bool macro = false) => AddCommand(new FunctionCommand(func, macro));

        public void Call(Action func, Storage storage, string path = "", bool macro = false) => Call(GuaranteeFunc(func, true), storage, path, macro);
        public void Call(MCFunction func, Storage storage, string path = "", bool macro = false) => AddCommand(new FunctionCommand(func, storage, path, macro));

        public void Call(Action func, KeyValuePair<string, object>[] args, bool macro = false) => Call(GuaranteeFunc(func, true), args, macro);
        public void Call(MCFunction func, KeyValuePair<string, object>[] args, bool macro = false) => AddCommand(BaseCall(func, args, macro));

        public void CallArg(Delegate func, IRuntimeArgument[] args, bool macro = false) => CallArg(GuaranteeFunc(func, false), args, macro);
        public void CallArg(Delegate func, IRuntimeArgument[] args, KeyValuePair<string, object>[] macros, bool macro = false) => CallArg(GuaranteeFunc(func, true), args, macros, macro);
        public void CallArg(MCFunction func, IRuntimeArgument[] args, bool macro = false) => AddCommand(BaseCall(func, args, macro));
        public void CallArg(MCFunction func, IRuntimeArgument[] args, KeyValuePair<string, object>[] macros, bool macro = false) => AddCommand(BaseCall(func, args, macros, macro));

        public ScoreRef CallRet(Action func)
        {
            var ret = Local();
            CallRet(func, ret);
            return ret;
        }

        public void CallRet(Action func, ScoreRef ret)
        {
            if (!DeclareMCAttribute.Get(func).Returns) throw new InvalidOperationException("Function does not return a value");

            CallRet(GuaranteeFunc(func, false), ret);
        }

        public void CallRet(MCFunction func, ScoreRef ret, bool macro = false) => AddCommand(new Execute(macro).Store(ret).Run(new FunctionCommand(func)));

        public void CallRet(Action func, ScoreRef ret, Storage storage, string path = "", bool macro = false) => CallRet(GuaranteeFunc(func, true), ret, storage, path, macro);
        public void CallRet(MCFunction func, ScoreRef ret, Storage storage, string path = "", bool macro = false) => AddCommand(new Execute(macro).Store(ret).Run(new FunctionCommand(func, storage, path)));

        public void CallRet(Action func, ScoreRef ret, KeyValuePair<string, object>[] args, bool macro = false, int tmp = 0) => CallRet(GuaranteeFunc(func, true), ret, args, macro, tmp);

        public ScoreRef CallRet(Action func, KeyValuePair<string, object>[] args, bool macro = false, int tmp = 0)
        {
            var ret = Local();
            CallRet(func, ret, args, macro, tmp);
            return ret;
        }

        public void CallRet(MCFunction func, ScoreRef ret, KeyValuePair<string, object>[] args, bool macro = false, int tmp = 0)
        {
            AddCommand(new Execute(macro).Store(ret).Run(BaseCall(func, args, macro, tmp)));
        }

        public void CallArgRet(Delegate func, ScoreRef ret, IRuntimeArgument[] args, bool macro = false) => CallArgRet(GuaranteeFunc(func, false), ret, args, macro);
        public void CallArgRet(Delegate func, ScoreRef ret, IRuntimeArgument[] args, KeyValuePair<string, object>[] macros, bool macro = false, int tmp = 0) => CallArgRet(GuaranteeFunc(func, false), ret, args, macros, macro, tmp);
        public void CallArgRet(MCFunction func, ScoreRef ret, IRuntimeArgument[] args, bool macro = false) => AddCommand(new Execute(macro).Store(ret).Run(BaseCall(func, args, macro)));
        public void CallArgRet(MCFunction func, ScoreRef ret, IRuntimeArgument[] args, KeyValuePair<string, object>[] macros, bool macro = false, int tmp = 0) => AddCommand(new Execute().Store(ret).Run(BaseCall(func, args, macros, macro, tmp)));

        public FunctionCommand BaseCall(MCFunction func, KeyValuePair<string, object>[] args, bool macro = false, int tmp = 0)
        {
            var parameters = new NBTCompound();
            var runtimeScores = new Dictionary<string, ScoreRef>();

            foreach (var i in args)
            {
                if (NBTType.ToNBT(i.Value) != null) parameters[i.Key] = NBTType.ToNBT(i.Value) ?? throw new Exception("How did we get here?");
                else if (i.Value.GetType().IsAssignableTo(typeof(NBTType))) parameters[i.Key] = (NBTType)i.Value;
                else if (i.Value is ScoreRef score) runtimeScores[i.Key] = score;
                else if (i.Value is NamespacedID id) parameters[i.Key] = id.ToString();
                else if (i.Value is Storage storage) parameters[i.Key] = storage.ToString();
                else throw new ArgumentException($"Type {i.Value.GetType().Name} is not supported yet");
            }

            if (runtimeScores.Count == 0)
            {
                return new FunctionCommand(func, parameters, macro);
            }

            AddCommand(new DataCommand.Modify(InternalStorage, $"func_tmp{tmp}", macro).Set().Value(parameters.ToString()));
            foreach (var i in runtimeScores)
            {
                AddCommand(new Execute(macro).Store(InternalStorage, $"func_tmp{tmp}.{i.Key}", NBTNumberType.Int, 1).Run(i.Value.Get()));
            }

            return new FunctionCommand(func, InternalStorage, $"func_tmp{tmp}");
        }

        public FunctionCommand BaseCall(MCFunction func, IRuntimeArgument[] args, bool macro = false)
        {
            PushArgs(args);
            return new FunctionCommand(func, macro);
        }

        public FunctionCommand BaseCall(MCFunction func, IRuntimeArgument[] args, KeyValuePair<string, object>[] macros, bool macro = false, int tmp = 0)
        {
            PushArgs(args);
            return BaseCall(func, macros, macro, tmp);
        }

        public void PushArgs(IRuntimeArgument[] args)
        {
            foreach (var i in args.Reverse())
            {
                ArgumentStack.Enqueue(i.GetAsArg());
            }
        }

        public void Print<T>(HeapPointer<T> ptr) => Std.PointerPrint(ptr.StandardMacros());
        public void Print<T>(IRuntimeProperty<T> prop) => Print(prop.Pointer);

        public void Print(params object[] args)
        {
            var text = new FormattedText();

            foreach (var i in args)
            {
                if (i is string str) text.Text(str);
                else if (i is ScoreRef score) text.Score(score);
                else throw new ArgumentException($"Invalid print object {i}. Try using Print<T> for objects on the heap");

                text.Text(" ");
            }

            text.RemoveLast();

            AddCommand(new TellrawCommand(new TargetSelector(TargetType.a), text));
        }

        public void Return(int val) => AddCommand(new ReturnCommand(val));
        public void Return() => Return(1);

        /// <summary>
        /// Returns from the function with the result of the command. Local variables are invalidated. <br/>
        /// Use <see cref="Return(ScoreRef)"/> to return a local variable.
        /// </summary>
        /// <param name="cmd"></param>
        public void Return(Command cmd) => AddCommand(new ReturnCommand(cmd));
        public void Return(ScoreRef score)
        {
            var temp = Temp(0, "ret");
            temp.Set(score);
            Return(temp.Get());
        }

        public void ReturnFail() => AddCommand(new ReturnCommand());

        public void Break() => Return();

        public MCFunction? FindFunction(Delegate func)
        {
            var funcs = MCFunctions.ToList();
            var index = funcs.FindIndex((i) => i.Key.Method.MethodHandle.Equals(func.Method.MethodHandle));
            if (index != -1) return funcs[index].Value;

            var methods = RuntimeMethods.ToList();
            index = methods.FindIndex((i) => i.Key.Method.MethodHandle.Equals(func.Method.MethodHandle));
            if (index != -1) return methods[index].Value;

            return null;
        }

        public MCFunction AddFunction(Delegate func)
        {
            return AddFunction(func, new(Namespace, DeclareMCAttribute.Get(func).Path));
        }

        public MCFunction AddFunction(Delegate func, NamespacedID id, bool scoped = false)
        {
            var mcfunc = new MCFunction(id, true);
            MCFunctions[func] = mcfunc;

            if (scoped)
            {
                var cleanup = new MCFunction(new(id.Namespace, $"zz_cleanup/{id.Path}"), true);
                MiscMCFunctions.Add(cleanup);

                List<ScoreRef> scope = [.. RegistersInUse];
                var oldFunc = CurrentTarget;
                var oldCleanup = CurrentTargetCleanup;
                CurrentTarget = mcfunc;
                CurrentTargetCleanup = cleanup;

                func.DynamicInvoke();

                WithCleanup(() =>
                {
                    for (int i = RegistersInUse.Count - 1; i >= scope.Count; i--)
                    {
                        RegisterStack.Dequeue(RegistersInUse[i]);
                    }
                });

                Call(CurrentTargetCleanup);

                RegistersInUse = scope;
                CurrentTarget = oldFunc;
                CurrentTargetCleanup = oldCleanup;
            }
            else
            {
                FunctionsToProcess.Enqueue(new(func, mcfunc));
            }
            return mcfunc;
        }

        public void AddCommand(Command cmd)
        {
            if (cmd is ReturnCommand) Call(CurrentTargetCleanup);
            else if (cmd is Execute ex && ex.Get<Run>().Command is ReturnCommand)
            {
                var other = ex.Copy();
                other.RemoveAll<Run>();

                if (other.Contains<Execute.Conditional.Subcommand>())
                {
                    var tmpExe = new Execute(cmd.Macro);
                    var tmp = Temp(0, "exe_ret");

                    foreach (var i in ex.GetAll<Execute.Conditional.Subcommand>())
                    {
                        tmpExe.Add((Execute.Subcommand)i.Clone());
                    }

                    tmpExe.Store(tmp);
                    tmpExe.Run(Constant(1).Get());
                    AddCommand(tmpExe);

                    other.RemoveAll<Execute.Conditional.Subcommand>();
                    other.If.Score(tmp, 1);
                    ex.RemoveAll<Execute.Conditional.Subcommand>();
                    ex.If.Score(tmp, 1);
                }

                other.Run(new FunctionCommand(CurrentTargetCleanup));
                AddCommand(other);
            }
            else if (ErrorChecking && cmd is not Execute)
            {
                if (cmd is FunctionCommand) CurrentTarget.Add(new Scoreboard.Players.Set(ErrorScore.Target, ErrorScore.Score, 1));
                CurrentTarget.Add(new Execute(cmd.Macro).Store(ErrorScore, false).Run(cmd));
                CurrentTarget.Add(new Execute(true).If.Score(ErrorScore, 0).Run(new TellrawCommand(new TargetSelector(TargetType.a), new FormattedText().Text($"Command \"{cmd}\" failed"))));
                return;
            }
            CurrentTarget.Add(cmd);
        }

        public void WithCleanup(Action func)
        {
            var oldTarget = CurrentTarget;
            CurrentTarget = CurrentTargetCleanup;

            func();

            CurrentTarget = oldTarget;
        }

        public void AddStaticObject(IStaticType type) => StaticTypes.Add(type);

        public void RegisterObject<T>() where T : IBaseRuntimeObject
        {
            var attr = RuntimeObjectAttribute.Get<T>();

            foreach (var i in typeof(T).GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
            {
                if (i.IsStatic && i.GetCustomAttribute<DeclareMCAttribute>() is not null)
                {
                    ProcessRuntimeObjectMethod(i, attr);
                }
            }
        }

        public MCFunction ProcessRuntimeObjectMethod(MethodInfo method, RuntimeObjectAttribute attr)
        {
            var funcAttr = DeclareMCAttribute.Get(method);
            var func = DelegateUtils.Create(method, null);
            var mcFunc = new MCFunction(new(Namespace, $"{attr.Name}/{funcAttr.Path}"), true);
            MCFunctions[func] = mcFunc;
            RuntimeMethods[func] = mcFunc;
            FunctionsToProcess.Enqueue(new(func, mcFunc));
            return mcFunc;
        }

        public void PrependMain(Command cmd)
        {
            (FindFunction(Main) ?? throw new Exception("Main doesn't exist")).Prepend(cmd);
        }

        public void AddScore(Score score) => Scores.Add(score);

        public ScoreRef Temp(int num, string type = "def")
        {
            var score = new Score($"_cl_tmp_{type}_{num}", "dummy");
            if (!Scores.Contains(score)) AddScore(score);
            return new(score, ScoreEntity);
        }

        public ScoreRef Temp(int num, int val, string type = "def")
        {
            var tmp = Temp(num, type);
            tmp.Set(val);
            return tmp;
        }

        public ScoreRef Local()
        {
            var score = new Score($"_cl_reg_{RegistersInUse.Count}", "dummy");
            Registers.Add(score);
            Scores.Add(score);

            var register = new ScoreRef(score, GlobalScoreEntity);
            RegistersInUse.Add(register);

            RegisterStack.Enqueue(register);

            return register;
        }

        public ScoreRef Local(int val)
        {
            var register = Local();
            register.Set(val);
            return register;
        }

        public ScoreRef Local(ScoreRef val)
        {
            var register = Local();
            register.Set(val);
            return register;
        }

        public ScoreRef Global()
        {
            var score = new Score($"_cl_{Namespace}_var_{Globals.Count}", "dummy");
            Globals.Add(score);
            Scores.Add(score);

            return new ScoreRef(score, GlobalScoreEntity);
        }

        public ScoreRef Global(int val)
        {
            var global = Global();
            MiscInitCmds.Add(new Scoreboard.Players.Set(global.Target, global.Score, val));
            return global;
        }

        public ScoreRef Constant(int val)
        {
            if (Constants.TryGetValue(val, out var obj)) return obj;

            var score = new Score("_cl_const", "dummy");
            var c = new ScoreRef(score, new NamedTarget($"#_cl_{val}"));

            Scores.Add(score);
            Constants[val] = c;

            return c;
        }

        public FunctionCommand Lambda(Action func) => LambdaWith(AddFunction(func, new(Namespace, $"zz_anon/{AnonymousFuncCounter++}"), true));

        public FunctionCommand LambdaWith(MCFunction mcfunc)
        {
            if (CurrentFunctionAttrs.Macros.Length == 0) return new(mcfunc);

            var nbt = new NBTCompound();

            foreach (var i in CurrentFunctionAttrs.Macros)
            {
                nbt[i] = $"$({i})";
            }

            return new(mcfunc, nbt, true);
        }

        public T AllocObj<T>() where T : IBaseRuntimeObject => AllocObj<T>(Local());
        public T AllocObj<T>(ScoreRef loc) where T : IBaseRuntimeObject
        {
            var obj = (T)T.Create(Heap.Alloc<T>(loc));
            if (obj.HasMethod("init")) CallArg(obj.GetMethod("init"), [obj]);
            return obj;
        }

        public T AttachObj<T>(ScoreRef loc) where T : IBaseRuntimeObject => (T)T.Create(new HeapPointer<T>(Heap, loc));

        public T AllocObjIfNull<T>(ScoreRef loc) where T : IBaseRuntimeObject
        {
            var obj = AttachObj<T>(loc);
            obj.IfNull(() => Alloc<T>(loc));
            return obj;
        }

        public HeapPointer<T> Alloc<T>() => Alloc<T>(Local());
        public HeapPointer<T> Alloc<T>(ScoreRef loc) => Heap.Alloc<T>(loc);

        public HeapPointer<T> Alloc<T>(ScoreRef loc, T val) where T : NBTType
        {
            var pointer = Heap.Alloc<T>(loc);
            pointer.Set(val);
            return pointer;
        }

        public HeapPointer<T> Attach<T>(ScoreRef loc) => new(Heap, loc);

        public HeapPointer<T> AllocIfNull<T>(ScoreRef loc, int val = 0)
        {
            var ptr = Attach<T>(loc);
            If(!ptr.Exists(), () => Alloc(loc, (NBTInt)val));
            return ptr;
        }

        public IfHandler If(Conditional comp, Action res) => new(this, comp, res);
        public void If(Conditional comp, Command res) => AddCommand(comp.Process(new Execute()).Run(res));

        public void While(Conditional comp, Action res)
        {
            WhileTrue(() =>
            {
                If(!comp, new ReturnCommand(1));
                res();
            });
        }

        public void WhileTrue(Action res)
        {
            var func = Lambda(() =>
            {
                res();
                AddCommand(LambdaWith(CurrentTarget));
            });
            AddCommand(func);
        }

        public void For(int start, ScoreRef end, Action<ScoreRef> res)
        {
            var i = Local(start);
            While(i < end, () =>
            {
                res(i);
                i.Add(1);
            });
        }

        public void Random(MCRange<int> range, ScoreRef score) => AddCommand(new Execute().Store(score).Run(new RandomCommand(range)));

        public ScoreRef Random(MCRange<int> range)
        {
            var score = Local();
            Random(range, score);
            return score;
        }

        protected virtual void Init() { }

        [DeclareMC("_main")]
        protected virtual void Main() { }

        [DeclareMC("_tick")]
        protected virtual void Tick() { }
    }
}
