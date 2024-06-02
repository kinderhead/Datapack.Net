using System;
using Datapack.Net.Data;

namespace Datapack.Net.CubeLib
{
    public interface IBaseRuntimeObject
    {
        public void IfNull(Action func);

        public static abstract IBaseRuntimeObject Create(BaseHeapPointer pointer);
    }

    public abstract class RuntimeObject<TProject, TSelf>(HeapPointer<TSelf> loc) : IBaseRuntimeObject where TProject : Project
    {
        public readonly HeapPointer<TSelf> Pointer = loc;

        protected HeapPointer<T> Get<T>(string path) => Pointer.Get<T>(path);

        protected void Set(string path, NBTType val) => Pointer.Get<NBTType>(path).Set(val);
        protected void Set<T>(string path, HeapPointer<T> pointer) => pointer.Move(Get<T>(path));
        protected void Set<T>(string path, RuntimeProperty<T> prop)
        {
            if (NBTType.IsNBTType<T>()) Set(path, NBTType.ToNBT(prop.Value ?? throw new ArgumentException("RuntimeProperty was not created properly")) ?? throw new Exception("How did we get here?"));
            else if (prop.Pointer is not null) Set(path, prop.Pointer);
            else throw new ArgumentException("RuntimeProperty was not created properly");
        }

        public static TProject State => (TProject)Project.ActiveProject;

        public static IBaseRuntimeObject Create(BaseHeapPointer pointer)
        {
            return (IBaseRuntimeObject?)Activator.CreateInstance(typeof(TSelf), [pointer]) ?? throw new ArgumentException("Failed to create runtime object");
        }

        public void IfNull(Action func)
        {
            State.If(Pointer.Exists(), func);
        }
    }
}
