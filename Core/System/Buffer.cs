using System.Collections.Concurrent;

namespace TinyMUD
{
	public static class Buffer
	{
		private struct Pool
		{
			public ConcurrentStack<byte[]> stack;

			public byte[] Acquire(int length)
			{
				byte[] result;
				if (!stack.TryPop(out result))
					result = new byte[length];
				return result;
			}

			public void Release(byte[] item)
			{
				stack.Push(item);
			}
		}

		private static readonly ConcurrentDictionary<int, Pool> pools = new ConcurrentDictionary<int, Pool>();

		public static byte[] Acquire(int length)
		{
			Pool pool = pools.GetOrAdd(length, len =>
			{
				return new Pool { stack = new ConcurrentStack<byte[]>() };
			});
			return pool.Acquire(length);
		}

		public static void Release(byte[] item)
		{
			Pool pool = pools.GetOrAdd(item.Length, len =>
			{
				return new Pool { stack = new ConcurrentStack<byte[]>() };
			});
			pool.Release(item);
		}
	}
}
