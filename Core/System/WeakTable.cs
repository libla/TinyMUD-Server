using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
	public sealed class WeakTable<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable where TKey : class
	{
		private readonly ConditionalWeakTable<TKey, WeakNode> table;
		private readonly LinkedList<KeyValue> list;
		private readonly ConcurrentStack<LinkedListNode<KeyValue>> deletes;
		private readonly ConditionalWeakTable<TKey, WeakNode>.CreateValueCallback create;

		public WeakTable()
		{
			table = new ConditionalWeakTable<TKey, WeakNode>();
			list = new LinkedList<KeyValue>();
			deletes = new ConcurrentStack<LinkedListNode<KeyValue>>();
			create = key =>
			{
				LinkedListNode<KeyValue> node = list.AddLast(new KeyValue { Key = new WeakReference<TKey>(key) });
				return new WeakNode(node, this);
			};
		}

		public TValue this[TKey key]
		{
			get
			{
				clean();
				WeakNode node;
				if (!table.TryGetValue(key, out node))
					throw new KeyNotFoundException();
				return node.node.Value.Value;
			}
			set
			{
				clean();
				WeakNode node = table.GetValue(key, create);
				KeyValue kv = node.node.Value;
				kv.Value = value;
				node.node.Value = kv;
			}
		}

		public void Add(TKey key, TValue value)
		{
			WeakNode node = new WeakNode(null, this);
			table.Add(key, node);
			node.node = list.AddLast(new KeyValue {Key = new WeakReference<TKey>(key), Value = value});
		}

		public bool Remove(TKey key)
		{
			WeakNode node;
			if (!table.TryGetValue(key, out node))
				return false;
			list.Remove(node.node);
			return true;
		}

		public void Clear()
		{
			foreach (KeyValue kv in list)
			{
				TKey key;
				if (kv.Key.TryGetTarget(out key))
					table.Remove(key);
			}
			list.Clear();
			deletes.Clear();
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			clean();
			WeakNode node;
			if (!table.TryGetValue(key, out node))
			{
				value = default(TValue);
				return false;
			}
			value = node.node.Value.Value;
			return true;
		}

		public Enumerator GetEnumerator()
		{
			clean();
			return new Enumerator(list.GetEnumerator());
		}

		private void clean()
		{
			LinkedListNode<KeyValue> node;
			while (deletes.TryPop(out node))
			{
				try
				{
					list.Remove(node);
				}
				catch (InvalidOperationException)
				{
				}
			}
		}

		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#region 内部类
		internal struct KeyValue
		{
			public WeakReference<TKey> Key;
			public TValue Value;
		}

		private class WeakNode
		{
			public LinkedListNode<KeyValue> node;
			private readonly WeakTable<TKey, TValue> table;

			public WeakNode(LinkedListNode<KeyValue> node, WeakTable<TKey, TValue> table)
			{
				this.node = node;
				this.table = table;
			}

			~WeakNode()
			{
				table.deletes.Push(node);
			}
		}
		#endregion

		#region 迭代器
		public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDisposable, IDictionaryEnumerator, IEnumerator
		{
			private LinkedList<KeyValue>.Enumerator enumerator;
			private KeyValuePair<TKey, TValue> current;

			internal Enumerator(LinkedList<KeyValue>.Enumerator enumerator)
			{
				this.enumerator = enumerator;
				this.current = default(KeyValuePair<TKey, TValue>);
			}

			public KeyValuePair<TKey, TValue> Current
			{
				get { return current; }
			}

			public void Dispose()
			{
				enumerator.Dispose();
				current = default(KeyValuePair<TKey, TValue>);
			}

			public bool MoveNext()
			{
				while (enumerator.MoveNext())
				{
					TKey key;
					KeyValue kv = enumerator.Current;
					if (kv.Key.TryGetTarget(out key))
					{
						current = new KeyValuePair<TKey, TValue>(key, kv.Value);
						return true;
					}
				}
				return false;
			}

			void IEnumerator.Reset()
			{
				((IEnumerator)enumerator).Reset();
				current = default(KeyValuePair<TKey, TValue>);
			}

			object IEnumerator.Current
			{
				get { return current; }
			}

			object IDictionaryEnumerator.Key
			{
				get { return current.Key; }
			}

			object IDictionaryEnumerator.Value
			{
				get { return current.Value; }
			}

			DictionaryEntry IDictionaryEnumerator.Entry
			{
				get { return new DictionaryEntry(current.Key, current.Value); }
			}
		}
		#endregion
	}
}
