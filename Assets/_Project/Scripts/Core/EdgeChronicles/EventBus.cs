using System;

namespace EdgeChronicles.Core
{
    /// <summary>
    /// 事件总线（按事件类型分通道）。
    /// <para>
    /// 设计取舍：<b>不做「一个全局总线 + 字符串事件名」</b>——那会让订阅者与事件名形成字符串耦合，
    /// 且派发时无法保证零分配。这里改为「每种事件一个通道」，通道之间互不影响，
    /// 事件名（字符串）不进入派发热路径。
    /// </para>
    /// <para>
    /// 性能契约：<b>派发零分配</b>（IMPL-v2.0 §6：事件总线 10k 事件 GC.Alloc == 0）。
    /// 订阅 / 退订允许分配（不在热路径）。
    /// </para>
    /// <para>
    /// 变更可见性：
    /// ① 退订在<b>派发过程中立即生效</b>（对应槽位置空，本次派发跳过）；
    /// ② 订阅在<b>派发过程中不生效于本次派发</b>（容量不足时延后到本次派发结束）。
    /// </para>
    /// </summary>
    /// <typeparam name="TEvent">事件载荷类型。结构体优先（避免装箱）。</typeparam>
    public sealed class EventBus<TEvent>
    {
        private Action<TEvent>[] _slots;
        private int _count;
        private int _liveCount;
        private bool _publishing;
        private bool _needsCompaction;
        private Action<TEvent>[] _pending;

        public EventBus(int initialCapacity = 8)
        {
            if (initialCapacity < 1) initialCapacity = 1;
            _slots = new Action<TEvent>[initialCapacity];
        }

        /// <summary>当前有效订阅者数量。</summary>
        public int SubscriberCount => _liveCount;

        /// <summary>已占用槽位数（含待压缩的空槽）。</summary>
        public int SlotCount => _count;

        /// <summary>订阅。同一委托重复订阅会被去重。</summary>
        public void Subscribe(Action<TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (IndexOf(handler) >= 0) return;

            if (_count == _slots.Length)
            {
                if (_publishing)
                {
                    // 派发中不允许扩容（会换掉正在被遍历的数组）。
                    // 扩容请求延后到本次派发结束——订阅不在热路径，延后不影响正确性。
                    _pending = _pending ?? new Action<TEvent>[4];
                    AppendPending(handler);
                    return;
                }

                int newSize = _slots.Length * 2;
                var grown = new Action<TEvent>[newSize];
                Array.Copy(_slots, grown, _count);
                _slots = grown;
            }

            _slots[_count++] = handler;
            _liveCount++;
        }

        /// <summary>退订。返回是否确实移除了一个订阅。</summary>
        public bool Unsubscribe(Action<TEvent> handler)
        {
            if (handler == null) return false;
            int index = IndexOf(handler);
            if (index < 0) return false;

            _slots[index] = null;
            _liveCount--;
            _needsCompaction = true;
            return true;
        }

        /// <summary>
        /// 派发。<b>零分配</b>（预热后）：不产生闭包、不产生枚举器、不装箱。
        /// </summary>
        public void Publish(in TEvent evt)
        {
            CompactIfNeeded();

            var slots = _slots;
            int count = _count;
            _publishing = true;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var handler = slots[i];
                    if (handler != null) handler(evt);
                }
            }
            finally
            {
                _publishing = false;
            }

            FlushPending();
        }

        /// <summary>清空所有订阅。</summary>
        public void Clear()
        {
            Array.Clear(_slots, 0, _count);
            _count = 0;
            _liveCount = 0;
            _needsCompaction = false;
            _pending = null;
        }

        private int IndexOf(Action<TEvent> handler)
        {
            for (int i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_slots[i], handler)) return i;
            }
            return -1;
        }

        private void CompactIfNeeded()
        {
            if (!_needsCompaction || _publishing) return;

            int write = 0;
            for (int i = 0; i < _count; i++)
            {
                var handler = _slots[i];
                if (handler == null) continue;
                _slots[write++] = handler;
            }

            for (int i = write; i < _count; i++) _slots[i] = null;
            _count = write;
            _needsCompaction = false;
        }

        private void AppendPending(Action<TEvent> handler)
        {
            if (_pending.Length == 0) return;
            int used = 0;
            while (used < _pending.Length && _pending[used] != null) used++;
            if (used == _pending.Length)
            {
                var grown = new Action<TEvent>[_pending.Length * 2];
                Array.Copy(_pending, grown, used);
                _pending = grown;
            }
            _pending[used] = handler;
        }

        private void FlushPending()
        {
            if (_pending == null) return;

            for (int i = 0; i < _pending.Length; i++)
            {
                var handler = _pending[i];
                _pending[i] = null;
                if (handler != null) Subscribe(handler);
            }
        }
    }
}
