using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;

namespace EdgeChronicles.Tests.Criteria
{
    /// <summary>
    /// A35 · mechanic_mod 枚举的双向一致性。
    ///
    /// 【为什么要这条】判据目录里写着"28 项合法集合"，生产侧也有一份枚举。
    /// 两份一旦分叉，A35 要么永远报绿、要么永远报红——两种都是废判据。
    /// 这条测试用**反射**读生产枚举（不产生编译期耦合），逐项比对：
    ///   · 数量一致
    ///   · 名称与顺序一致（顺序 = 位序，位序变了等于语义变了）
    /// </summary>
    public class MechanicModParityTests
    {
        [Test]
        public void 生产枚举_与判据基准逐项一致()
        {
            Type t = ProductionContracts.TryResolve("EdgeChronicles.Data.MechanicMod");
            if (t == null)
            {
                Assert.Fail("契约未实现：找不到类型 EdgeChronicles.Data.MechanicMod（生产侧 Data 程序集）。"
                    + "A35 判据依赖该枚举，缺失时本判据无法判定。");
            }

            string[] actual = Enum.GetNames(t);
            CollectionAssert.AreEqual(MechanicModCatalog.Expected, actual,
                "生产侧 mechanic_mod 枚举与判据基准不一致。生产侧增删项时，"
                + "必须同步更新判据目录与 CombatSpec —— 两份清单分叉后 A35 就是废判据。");
        }

        [Test]
        public void 生产枚举_合法集合数量为二十八()
        {
            Type t = ProductionContracts.TryResolve("EdgeChronicles.Data.MechanicMod");
            if (t == null) { Assert.Fail("契约未实现：EdgeChronicles.Data.MechanicMod 缺失。"); }
            Assert.AreEqual(28, Enum.GetNames(t).Length, "EL-03 §2.1 规定 mechanic_mod 共 28 项。");
            Assert.AreEqual(28, MechanicModCatalog.Expected.Length, "判据基准的项数应为 28。");
        }
    }
}
