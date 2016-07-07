using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
	internal struct WeakKey<T> where T : class
	{
		public WeakReference<T> Key;
		public int Hash;

		public static implicit operator WeakKey<T>(T key)
		{
			if (key == null)
				throw new ArgumentNullException("key");
			return new WeakKey<T> { Key = new WeakReference<T>(key), Hash = key.GetHashCode() };
		}

		private class EqualityComparer : IEqualityComparer<WeakKey<T>>
		{
			public bool Equals(WeakKey<T> x, WeakKey<T> y)
			{
				if (x.Hash != y.Hash)
					return false;
				T k1, k2;
				bool r1 = x.Key.TryGetTarget(out k1);
				bool r2 = y.Key.TryGetTarget(out k2);
				if (r1 != r2)
					return true;
				return !r1 || k1.Equals(k2);
			}

			public int GetHashCode(WeakKey<T> obj)
			{
				return obj.Hash;
			}
		}

		public static readonly IEqualityComparer<WeakKey<T>> DefaultEqualityComparer = new EqualityComparer();
	}

	public sealed class WeakTable<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable where TKey : class
	{
		private readonly Dictionary<WeakKey<TKey>, TValue> dict;
		private readonly List<WeakKey<TKey>> cleans;
		private int step;
		private int maxstep;

		public WeakTable()
		{
			dict = new Dictionary<WeakKey<TKey>, TValue>(WeakKey<TKey>.DefaultEqualityComparer);
			cleans = new List<WeakKey<TKey>>();
			step = 0;
			maxstep = 64;
		}

		public WeakTable(int capacity)
		{
			dict = new Dictionary<WeakKey<TKey>, TValue>(capacity, WeakKey<TKey>.DefaultEqualityComparer);
			cleans = new List<WeakKey<TKey>>();
			step = 0;
			maxstep = 64;
		}

		public WeakTable(IDictionary<TKey, TValue> dictionary)
			: this(dictionary.Count)
		{
			foreach (KeyValuePair<TKey, TValue> kv in dictionary)
			{
				Add(kv.Key, kv.Value);
			}
		}

		public Enumerator GetEnumerator()
		{
			onestep();
			return new Enumerator(dict.GetEnumerator());
		}

		public TValue this[TKey key]
		{
			get
			{
				onestep();
				return dict[key];
			}
			set
			{
				onestep();
				dict[key] = value;
			}
		}

		public void Add(TKey key, TValue value)
		{
			onestep();
			dict.Add(key, value);
		}

		public bool Remove(TKey key)
		{
			return dict.Remove(key);
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			onestep();
			return dict.TryGetValue(key, out value);
		}

		public void Clear()
		{
			step = 0;
			dict.Clear();
		}

		private void onestep()
		{
			if (++step >= maxstep)
			{
				step = 0;
				foreach (KeyValuePair<WeakKey<TKey>, TValue> kv in dict)
				{
					TKey key;
					if (!kv.Key.Key.TryGetTarget(out key))
					{
						cleans.Add(kv.Key);
					}
				}
				for (int i = 0; i < cleans.Count; ++i)
				{
					dict.Remove(cleans[i]);
				}
				if (cleans.Count >= maxstep >> 1)
				{
					maxstep <<= 1;
				}
				cleans.Clear();
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

		public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDisposable, IDictionaryEnumerator, IEnumerator
		{
			private Dictionary<WeakKey<TKey>, TValue>.Enumerator enumerator;
			private KeyValuePair<TKey, TValue> current;

			internal Enumerator(Dictionary<WeakKey<TKey>, TValue>.Enumerator enumerator)
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
					KeyValuePair<WeakKey<TKey>, TValue> kv = enumerator.Current;
					if (kv.Key.Key.TryGetTarget(out key))
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
	}
}
