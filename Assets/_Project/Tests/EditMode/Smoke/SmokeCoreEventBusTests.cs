using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;

namespace EdgeChronicles.Tests.Smoke
{
    /// <summary>
    /// SC-01 · Core：事件总线 10k 事件零 GC。
    ///
    /// 【口径】量 = 线程分配字节数（byte），时钟轴 = 墙钟；
    /// 测量窗口只覆盖"发布"这一段，订阅与预热都在窗口之外。
    /// </summary>
    public class SmokeCoreEventBusTests
    {
        private const int PublishCount = 10000;
        private const int EventId = 1;

        [Test]
        public void 事件总线_一万次发布零分配()
        {
            var bus = new ReferenceEventBus();
            int received = 0;
            object payload = new object(); // 预分配负载：发布路径不允许 new

            bus.Subscribe(EventId, delegate (object _) { received++; });

            // 预热：JIT 与容器首次扩容都发生在窗口之外。
            bus.Publish(EventId, payload);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < PublishCount; i++)
            {
                bus.Publish(EventId, payload);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            long allocated = after - before;
            Assert.AreEqual(0L, allocated,
                "事件总线在 " + PublishCount + " 次发布中分配了 " + allocated + " 字节。"
                + "发布路径不允许装箱、容器扩容或委托再分配。");
            Assert.AreEqual(PublishCount + 1, received, "订阅者收到的事件数不对。");
        }

        [Test]
        public void 事件总线_未订阅的事件不产生回调()
        {
            var bus = new ReferenceEventBus();
            int received = 0;
            bus.Subscribe(EventId, delegate (object _) { received++; });

            bus.Publish(EventId + 1, new object());

            Assert.AreEqual(0, received, "未订阅的事件号不应触发任何回调。");
        }
    }
}
