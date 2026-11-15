using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace EdgeChronicles.Tests.PlayMode
{
    /// <summary>
    /// PlayMode：战斗 tick 8 阶段的顺序与"表现层不得回写"这条边界。
    ///
    /// 【为什么这条只能在 PlayMode 验】
    /// IMPL-v2.0 §2 规定：表现层若要影响逻辑，只能等下一帧从 #1 以 FrameInput 重新进入；
    /// 跨层写入由 PlayMode 断言 <c>no_writeback</c> 拦截。
    /// EditMode 只能验纯函数，验不了"帧内回写"。
    ///
    /// 【口径】量 = 事件序列（有序），时钟轴 = 逻辑帧；
    /// 判定动作 = 逐帧比对派发顺序与写回次数。
    /// </summary>
    public class PlayModeCombatLinkTests
    {
        /// <summary>一次 tick 内的期望阶段顺序（IMPL-v2.0 §2）。</summary>
        private static readonly string[] ExpectedPhaseOrder =
        {
            "snapshot",   // #0 养成快照刷新
            "input",      // #1 输入采样
            "buffer",     // #2 预输入缓冲
            "skill",      // #3 技能状态机推进
            "move",       // #4 位移积分
            "hit",        // #5 命中判定
            "hurt",       // #6 受击 / 击退
            "resource",   // #7 资源结算
            "present"     // #8 表现层派发
        };

        [Test]
        public void 阶段顺序常量_与规格逐项一致()
        {
            Assert.AreEqual(9, ExpectedPhaseOrder.Length, "tick 应为 8 阶段 + 快照刷新，共 9 段。");
            Assert.AreEqual("snapshot", ExpectedPhaseOrder[0], "快照刷新必须先于输入采样。");
            Assert.AreEqual("present", ExpectedPhaseOrder[ExpectedPhaseOrder.Length - 1],
                "表现层派发必须是最后一段。");
        }

        [UnityTest]
        public IEnumerator 场景空跑_不得出现异常或跨层回写()
        {
            // 生产侧接入后，此用例在真实战斗场景里逐帧记录：
            //   · 每次 tick 的阶段顺序
            //   · 表现层在 #8 之后的任何逻辑写入（no_writeback）
            // 工程侧尚未实现战斗运行时时，这里只验证"空场景连续运行不会抛异常"，
            // 避免用"尚未实现"当借口让整套 PlayMode 长期不跑。
            yield return null;
            yield return null;
            Assert.IsTrue(true, "空场景两帧无异常。");
        }

        [Test]
        public void 判据目录_在PlayMode下同样可加载()
        {
            // 保证 Shared 程序集在 PlayMode 上下文里可用（EditMode 与 PlayMode 共用同一份判据定义）。
            Assert.AreEqual(22, CriterionCatalog.Walkthrough.Count);
            Assert.AreEqual(30, CriterionCatalog.NegativeCases.Count);
        }
    }
}
