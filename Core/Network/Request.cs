using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace TinyMUD
{
	public class RequestException : SystemException
	{
		public RequestException(int n)
			: base(string.Format("Request parse error {0}", n)) { }
	}

	public abstract class Request
	{
		/// <summary>
		/// 请求接收缓冲区
		/// </summary>
		protected class ByteBuffer
		{
			private byte[] _array;
			public byte[] array
			{
				get { return _array; }
			}
			private int _length;
			public int length
			{
				get { return _length; }
			}
			private int _offset;
			public int offset
			{
				get { return _offset; }
			}

			private ByteBuffer()
			{
				_array = new byte[1024];
				_length = 0;
				_offset = 0;
			}

			public void Write(byte[] bytes)
			{
				Write(bytes, 0, bytes.Length);
			}

			public void Write(byte[] bytes, int length)
			{
				Write(bytes, 0, length);
			}

			private void checksize(int length)
			{
				if (_offset + _length + length > _array.Length)
				{
					int need = _length + length;
					if (need <= _array.Length && _offset >= (_array.Length + 1) >> 1)
					{
						Array.Copy(_array, _offset, _array, 0, _length);
						_offset = 0;
					}
					else
					{
						int size = _array.Length << 1;
						while (size < need)
						{
							size = size << 1;
						}
						byte[] array = new byte[size];
						Array.Copy(_array, _offset, array, 0, _length);
						_offset = 0;
						_array = array;
					}
				}
			}

			public void Write(byte[] bytes, int offset, int length)
			{
				checksize(length);
				Array.Copy(bytes, offset, _array, _offset + _length, length);
				_length += length;
			}

			public void Pop(int length)
			{
				if (length > _length)
					length = _length;
				_offset += length;
				_length -= length;
				if (_length == 0)
				{
					Reset();
				}
			}

			public void Reset()
			{
				_offset = 0;
				_length = 0;
			}

			private static readonly ConcurrentStack<ByteBuffer> freelist = new ConcurrentStack<ByteBuffer>();

			public static ByteBuffer New()
			{
				ByteBuffer result;
				if (!freelist.TryPop(out result))
					result = new ByteBuffer();
				return result;
			}

			public void Release()
			{
				Reset();
				freelist.Push(this);
			}
		}

		private readonly ByteBuffer buffer;

		protected Request()
		{
			buffer = ByteBuffer.New();
		}

		~Request()
		{
			buffer.Release();
		}

		internal bool Input(byte[] bytes, int offset, ref int length)
		{
			int len = length;
			while (len > 0)
			{
				int l = PreTest(buffer, bytes, offset, length);
				offset += l;
				len -= l;
				if (Test(buffer))
				{
					length -= len;
					return true;
				}
			}
			return false;
		}

		protected abstract int PreTest(ByteBuffer buffer, byte[] bytes, int offset, int length);
		protected abstract bool Test(ByteBuffer buffer);
		protected virtual void Clear() { }
		public abstract bool Send(Session session);
		internal abstract void Execute(Session session);

		public void Reset()
		{
			buffer.Reset();
			Clear();
		}
	}

	public abstract class Request<T> : Request
	{
		public T Value;
		internal override void Execute(Session session)
		{
			Handle(session, Value);
		}

		protected virtual void Handle(Session session, T t)
		{
			if (DefaultHandler != null)
				DefaultHandler(session, t);
		}

		public static event Action<Session, T> DefaultHandler;
	}

	public abstract class Request<TKey, T> : Request
	{
		public TKey ID;
		public T Value;
		internal override void Execute(Session session)
		{
			Handle(session, ID, Value);
		}

		protected virtual void Handle(Session session, TKey k, T t)
		{
			Action<Session, T> action;
			if (_Handlers.TryGetValue(k, out action))
			{
				action(session, t);
			}
			else
			{
				if (DefaultHandler != null)
					DefaultHandler(session, k, t);
			}
		}

		private static readonly Dictionary<TKey, Action<Session, T>> _Handlers = new Dictionary<TKey, Action<Session, T>>();
		public static Dictionary<TKey, Action<Session, T>> Handlers
		{
			get
			{
				return _Handlers;
			}
		}
		public static event Action<Session, TKey, T> DefaultHandler;
	}

	public class UTF8StringRequest : Request<string>
	{
		protected override int PreTest(ByteBuffer buffer, byte[] bytes, int offset, int length)
		{
			int len = length;
			for (int i = offset; i < length; ++i)
			{
				if (bytes[i] == 0)
				{
					len = i - offset + 1;
					break;
				}
			}
			buffer.Write(bytes, offset, len);
			return len;
		}

		protected override bool Test(ByteBuffer buffer)
		{
			if (buffer.array[buffer.offset + buffer.length - 1] == 0)
			{
				Value = System.Text.Encoding.UTF8.GetString(buffer.array, buffer.offset, buffer.length - 1);
				buffer.Reset();
				return true;
			}
			return false;
		}

		public override bool Send(Session session)
		{
			if (!session.Write(System.Text.Encoding.UTF8.GetBytes(Value)))
				return false;
			if (!session.Write(0))
				return false;
			return true;
		}

		protected override void Clear()
		{
			Value = null;
		}
	}
}
