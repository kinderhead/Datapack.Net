﻿using Datapack.Net.Data._1_20_4;
using Datapack.Net.Function.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datapack.Net.Tests
{
    public class CommandTest
    {
        [Test]
        public void Say()
        {
            var cmd = new SayCommand("test");
            Assert.That(cmd.Build(), Is.EqualTo("say test"));
        }

        #region Function

        [Test]
        public void Function1()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new FunctionCommand(func);
            Assert.That(cmd.Build(), Is.EqualTo("function test:func"));
        }

        [Test]
        public void Function2()
        {
            var func = new MCFunction(new("test", "func"));
            func.Add(new SayCommand("test", true));

            var cmd = new FunctionCommand(func, []);
            Assert.That(cmd.Build(), Is.EqualTo("function test:func {}"));
        }

        [Test]
        public void Function3()
        {
            var func = new MCFunction(new("test", "func"));
            func.Add(new SayCommand("test", true));

            var cmd = new FunctionCommand(func, new Position(4, 4, 4));
            Assert.That(cmd.Build(), Is.EqualTo("function test:func with block 4 4 4"));
        }

        [Test]
        public void Function4()
        {
            var func = new MCFunction(new("test", "func"));
            func.Add(new SayCommand("test", true));

            var cmd = new FunctionCommand(func, new NamedTarget("wah"));
            Assert.That(cmd.Build(), Is.EqualTo("function test:func with entity wah"));
        }

        [Test]
        public void Function5()
        {
            var storage = new Storage(new("test:test"));
            var func = new MCFunction(new("test", "func"));
            func.Add(new SayCommand("test", true));

            var cmd = new FunctionCommand(func, storage);
            Assert.That(cmd.Build(), Is.EqualTo("function test:func with storage test:test"));
        }

        #endregion

        #region Execute

        [Test]
        public void Execute1()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new Execute().As(new TargetSelector(TargetType.e, type: Entities.Axolotl)).Facing(new(0, 0, 0)).Run(new FunctionCommand(func));

            Assert.That(cmd.Build(), Is.EqualTo("execute as @e[type=minecraft:axolotl] facing 0 0 0 run function test:func"));
        }

        [Test]
        public void Execute2()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new Execute()
                .Align(new("xy"))
                .Anchored(false)
                .At(new TargetSelector(TargetType.r))
                .In(Dimensions.End)
                .On(OnRelation.Leasher)
                .Run(new FunctionCommand(func));

            Assert.That(cmd.Build(), Is.EqualTo("execute align xy anchored feet at @r in minecraft:the_end on leasher run function test:func"));
        }

        [Test]
        public void Execute3()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new Execute()
                .Positioned(new Position(1, 2, 3))
                .Positioned(new TargetSelector(TargetType.a))
                .Positioned(Heightmap.Ocean_Floor)
                .Run(new FunctionCommand(func));

            Assert.That(cmd.Build(), Is.EqualTo("execute positioned 1 2 3 positioned as @a positioned over ocean_floor run function test:func"));
        }

        [Test]
        public void Execute4()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new Execute()
                .Rotated(new Rotation(4, new(4, RotCoordType.Relative)))
                .Rotated(new NamedTarget("bah"))
                .Summon(Entities.Mule)
                .Run(new FunctionCommand(func));

            Assert.That(cmd.Build(), Is.EqualTo("execute rotated 4 ~4 rotated as bah summon minecraft:mule run function test:func"));
        }

        [Test]
        public void Execute5()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new Execute()
                .If.Biome(new(0, 0, 0), Biomes.Savanna)
                .If.Block(new(1, 2, 3), new Blocks.BirchDoor(half: DoorHalf.Upper))
                .If.Blocks(new(0, 0, 0), new(5, 5, 5), new(10, 1, 0))
                .If.Blocks(new(0, 0, 0), new(5, 5, 5), new(10, 1, 0), true)
                .Run(new FunctionCommand(func));

            Assert.That(cmd.Build(), Is.EqualTo("execute if biome 0 0 0 minecraft:savanna if block 1 2 3 minecraft:birch_door[half=upper] if blocks 0 0 0 5 5 5 10 1 0 all if blocks 0 0 0 5 5 5 10 1 0 masked run function test:func"));
        }

        [Test]
        public void Execute6()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new Execute()
                .Unless.Data(new Position(1, 2, 3), "hi")
                .Unless.Data(new TargetSelector(TargetType.s), "three.1")
                .Unless.Data(new Storage(new("test:test")), "test")
                .Run(new FunctionCommand(func));

            Assert.That(cmd.Build(), Is.EqualTo("execute unless data block 1 2 3 hi unless data entity @s three.1 unless data storage test:test test run function test:func"));
        }

        [Test]
        public void Execute7()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new Execute()
                .Unless.Dimension(Dimensions.Nether)
                .Unless.Entity(new TargetSelector(TargetType.e))
                .Unless.Function(func)
                .Unless.Loaded(new(0, 0, 0))
                .Run(new FunctionCommand(func));

            Assert.That(cmd.Build(), Is.EqualTo("execute unless dimension minecraft:the_nether unless entity @e unless function test:func unless loaded 0 0 0 run function test:func"));
        }

        [Test]
        public void Execute8()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new Execute()
                .If.Score(new TargetSelector(TargetType.p), new Score("test", "dummy"), Comparison.Equal, new TargetSelector(TargetType.r), new Score("test", "dummy"))
                .If.Score(new TargetSelector(TargetType.p), new Score("test", "dummy"), new(3,4))
                .Run(new FunctionCommand(func));

            Assert.That(cmd.Build(), Is.EqualTo("execute if score @p test = @r test if score @p test matches 3..4 run function test:func"));
        }

        [Test]
        public void Execute9()
        {
            var func = new MCFunction(new("test", "func"));
            var cmd = new Execute()
                .Store(new Position(0, 0, 0), "hi", NBTNumberType.Byte, 1)
                .Store(new Bossbar(new("test:test")), BossbarValueType.Max, false)
                .Store(new TargetSelector(TargetType.e), "test", NBTNumberType.Int, 4)
                .Store(new TargetSelector(TargetType.e), new Score("test", "dummy"), false)
                .Store(new Storage("test:test"), "wah", NBTNumberType.Double, 4.3)
                .Run(new FunctionCommand(func));

            Assert.That(cmd.Build(), Is.EqualTo("execute store result block 0 0 0 hi byte 1 store success bossbar test:test max store result entity @e test int 4 store success score @e test store result storage test:test wah double 4.3 run function test:func"));
        }

        #endregion

        #region Data

        [Test]
        public void DataGetBlock()
        {
            var cmd = new DataCommand.Get(new Position(0, 0, 0), "test", 3);
            Assert.That(cmd.Build(), Is.EqualTo("data get block 0 0 0 test 3"));
        }

        [Test]
        public void DataGetEntity()
        {
            var cmd = new DataCommand.Get(new NamedTarget("boo"), "test", 3);
            Assert.That(cmd.Build(), Is.EqualTo("data get entity boo test 3"));
        }

        [Test]
        public void DataGetStorage()
        {
            var cmd = new DataCommand.Get(new Storage("test:test"));
            Assert.That(cmd.Build(), Is.EqualTo("data get storage test:test"));
        }

        [Test]
        public void DataMergeBlock()
        {
            var cmd = new DataCommand.Merge(new Position(0, 0, 0), new NBTCompound{{ "test", "test" }});
            Assert.That(cmd.Build(), Is.EqualTo("data merge block 0 0 0 {\"test\":\"test\"}"));
        }

        [Test]
        public void DataMergeEntity()
        {
            var cmd = new DataCommand.Merge(new NamedTarget("boo"), new NBTCompound { { "test", "test" } });
            Assert.That(cmd.Build(), Is.EqualTo("data merge entity boo {\"test\":\"test\"}"));
        }

        [Test]
        public void DataMergeStorage()
        {
            var cmd = new DataCommand.Merge(new Storage("test:test"), new NBTCompound { { "test", "test" } });
            Assert.That(cmd.Build(), Is.EqualTo("data merge storage test:test {\"test\":\"test\"}"));
        }

        #endregion
    }
}
