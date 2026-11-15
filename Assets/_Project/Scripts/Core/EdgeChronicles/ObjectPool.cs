using System;

namespace EdgeChronicles.Core
{
    /// <summary>
    /// 通用对象池。
    /// <para>
    /// 契约：<b>预热完成后，Rent / Return 不产生任何堆分配</b>。
    /// 用于高频生灭对象（命中特效、伤害数字、逻辑投影点）。
    /// </para>
    /// <para>
    /// 治理约束：池里归还的对象必须<b>回到可复用状态</b>——归还时调用 <c>onReturn</c> 重置，
    /// 取出时调用 <c>onRent</c> 初始化。这两个回调由调用方提供，
    /// Core 不猜测对象的内部状态（Core 不认识任何游戏对象）。
    /// </para>
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onRent;
        private readonly Action<T> _onReturn;
        private readonly int _maxRetained;

        private T[] _items;
        private int _count;

        /// <param name="create">池空时的构造函数。</param>
        /// <param name="onRent">取出时的初始化回调（可为 null）。</param>
        /// <param name="onReturn">归还时的重置回调（可为 null）。</param>
        /// <param name="prewarm">预热数量。</param>
        /// <param name="maxRetained">最多保留的闲置对象数，超出的归还对象直接丢弃（防止无限增长）。</param>
        public ObjectPool(
            Func<T> create,
            Action<T> onRent = null,
            Action<T> onReturn = null,
            int prewarm = 0,
            int maxRetained = 1024)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onRent = onRent;
            _onReturn = onReturn;
            _maxRetained = maxRetained > 0 ? maxRetained : 1;

            int capacity = prewarm > 0 ? prewarm : 8;
            _items = new T[capacity];
            for (int i = 0; i < prewarm; i++) _items[_count++] = _create();
        }

        /// <summary>池中闲置对象数量。</summary>
        public int CountInactive => _count;

        /// <summary>池容量（含闲置）。</summary>
        public int Capacity => _items.Length;

        /// <summary>
        /// 取一个对象。池空时新建（本次会分配；预热足够时不会）。
        /// </summary>
        public T Rent()
        {
            T item;
            if (_count > 0)
            {
                item = _items[--_count];
                _items[_count] = null;
            }
            else
            {
                item = _create();
            }

            _onRent?.Invoke(item);
            return item;
        }

        /// <summary>
        /// 取一个对象（避免调用方写 lambda 造成闭包分配）。
        /// </summary>
        public void Rent(out T item)
        {
            item = Rent();
        }

        /// <summary>归还一个对象。返回是否被池收下（超出保留上限时返回 false）。</summary>
        public bool Return(T item)
        {
            if (item == null) return false;

            _onReturn?.Invoke(item);

            if (_count >= _maxRetained) return false;

            if (_count == _items.Length)
            {
                int newSize = _items.Length * 2;
                if (newSize > _maxRetained) newSize = _maxRetained;
                var grown = new T[newSize];
                Array.Copy(_items, grown, _count);
                _items = grown;
            }

            _items[_count++] = item;
            return true;
        }

        /// <summary>清空池（闲置对象全部丢弃）。</summary>
        public void Clear()
        {
            for (int i = 0; i < _count; i++) _items[i] = null;
            _count = 0;
        }
    }
}
