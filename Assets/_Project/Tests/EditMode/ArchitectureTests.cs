using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;

namespace EdgeChronicles.Tests
{
    /// <summary>
    /// 架构守卫：把「依赖单向无环、战斗内核不引用 UI / 场景加载」从文档约定变成会红的测试。
    /// 任何人在 asmdef 里接错依赖，这个测试立刻失败。
    /// </summary>
    public class ArchitectureTests
    {
        private static string[] RefsOf(string asmName)
        {
            var asm = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .FirstOrDefault(a => a.name == asmName);
            Assert.IsNotNull(asm, "找不到程序集：" + asmName);
            return asm.assemblyReferences
                .Select(r => r.name)
                .Where(n => n.StartsWith("EdgeChronicles."))
                .OrderBy(n => n)
                .ToArray();
        }

        [Test]
        public void Core_不依赖任何其他程序集()
        {
            CollectionAssert.IsEmpty(RefsOf("EdgeChronicles.Core"));
        }

        [Test]
        public void Data_只依赖Core()
        {
            CollectionAssert.AreEqual(new[] { "EdgeChronicles.Core" }, RefsOf("EdgeChronicles.Data"));
        }

        [Test]
        public void Kernel_只依赖Core与Data()
        {
            CollectionAssert.AreEqual(new[] { "EdgeChronicles.Core", "EdgeChronicles.Data" }, RefsOf("EdgeChronicles.Kernel"));
        }

        [Test]
        public void Presentation_只订阅事件不依赖Kernel()
        {
            CollectionAssert.AreEqual(new[] { "EdgeChronicles.Core", "EdgeChronicles.Data" }, RefsOf("EdgeChronicles.Presentation"));
        }

        [Test]
        public void UI_不依赖Kernel与Binding()
        {
            CollectionAssert.AreEqual(new[] { "EdgeChronicles.Core", "EdgeChronicles.Data" }, RefsOf("EdgeChronicles.UI"));
        }

        [Test]
        public void Binding_可依赖Kernel但不被Kernel依赖()
        {
            CollectionAssert.AreEqual(
                new[] { "EdgeChronicles.Core", "EdgeChronicles.Data", "EdgeChronicles.Kernel" },
                RefsOf("EdgeChronicles.Binding"));
        }

        [Test]
        public void 战斗内核_不得引用UI_Flow_Binding()
        {
            var refs = RefsOf("EdgeChronicles.Kernel");
            CollectionAssert.DoesNotContain(refs, "EdgeChronicles.UI");
            CollectionAssert.DoesNotContain(refs, "EdgeChronicles.Flow");
            CollectionAssert.DoesNotContain(refs, "EdgeChronicles.Binding");
            CollectionAssert.DoesNotContain(refs, "EdgeChronicles.Presentation");
        }
    }
}
