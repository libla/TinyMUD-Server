using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace TinyMUD
{
	public abstract class Session
	{
		#region Session相关的类
		public interface ISettings
		{
			string ip { get; }
			int port { get; }
			int timeout { get; }
			int buffersize { get; }
			int sumsending { get; }
			void request(Session session, Request request);
			void read(Session session, int length);
			void write(Session session, int length);
			void close(Session session);
			void exception(Session session, Exception e);
		}

		public struct Events
		{
			public Action<Session, Request> request;
			public Action<Session, int> read;
			public Action<Session, int> write;
			public Action<Session> close;
			public Action<Session, Exception> exception;
		}

		public class Settings : ISettings
		{
			public string ip { get; set; }
			public int port { get; set; }
			public int timeout { get; set; }
			public int buffersize { get; set; }
			public int sumsending { get; set; }
			public Events events;

			public void request(Session session, Request request)
			{
				if (events.request != null)
					events.request(session, request);
			}

			public void read(Session session, int length)
			{
				if (events.read != null)
					events.read(session, length);
			}

			public void write(Session session, int length)
			{
				if (events.write != null)
					events.write(session, length);
			}

			public void close(Session session)
			{
				if (events.close != null)
					events.close(session);
			}

			public void exception(Session session, Exception e)
			{
				if (events.exception != null)
					events.exception(session, e);
				else
					Log.Error(e);
			}

			public Settings(string ip, int port)
			{
				this.ip = ip;
				this.port = port;
				this.timeout = Timeout.Infinite;
				this.buffersize = 65536;
				this.sumsending = 16;
			}
		}

		[Flags]
		public enum State
		{
			NONE = 0,
			INIT = 1,
			SEND = 2,
			CLOSE = 4,
		}
		#endregion

		protected readonly Loop _loop;
		private readonly ISettings _settings;
		private readonly Socket _socket;
		private Loop.Timer _timer;
		private long _alivetime;
		private int _state;

		private readonly ConcurrentQueue<ArraySegment<byte>> _sendqueue;
		private byte[] _sendbytes;
		private int _senduse;
		private readonly SocketAsyncEventArgs _sendargs;

		public bool Connected
		{
			get
			{
				return (_state & (int)State.CLOSE) == 0;
			}
		}

		public IPEndPoint Remote
		{
			get
			{
				return _socket.RemoteEndPoint as IPEndPoint;
			}
		}

		public IPEndPoint Local
		{
			get
			{
				return _socket.LocalEndPoint as IPEndPoint;
			}
		}

		public short TTL
		{
			get { return _socket.Ttl; }
			set { _socket.Ttl = value; }
		}

		public bool NoDelay
		{
			get { return _socket.NoDelay; }
			set { _socket.NoDelay = value; }
		}

		protected Session(Loop loop, ISettings settings, Socket socket)
		{
			_loop = loop;
			_settings = settings;
			_socket = socket;
			_timer = null;
			_alivetime = Application.Now;
			_state = (int)State.NONE;

			_sendqueue = new ConcurrentQueue<ArraySegment<byte>>();
			_sendbytes = null;
			_sendargs = new SocketAsyncEventArgs { BufferList = new List<ArraySegment<byte>>() };
			_sendargs.Completed += (sender, args) =>
			{
				ProcessSend();
			};
		}

		internal void Start()
		{
			if (AddState(State.INIT))
			{
				if ((_state & (int)State.CLOSE) != 0)
					return;
				_loop.Retain();
				if (_settings.timeout != Timeout.Infinite)
				{
					_timer = Loop.Timer.Create(_settings.timeout, false, () =>
					{
						int dtime = (int)(Application.Now - _alivetime / 1000);
						if (dtime >= _settings.timeout)
						{
							Close();
						}
						else
						{
							_timer.Time = _settings.timeout - dtime;
							_timer.Start();
						}
					});
					_timer.Start();
				}
				SocketAsyncEventArgs readargs = new SocketAsyncEventArgs();
				readargs.SetBuffer(new byte[_settings.buffersize], 0, _settings.buffersize);
				readargs.Completed += (sender, args) =>
				{
					ProcessReceive(args);
				};
				PostReceive(readargs);
			}
		}

		public bool Write(byte[] bytes, int offset, int length)
		{
			if ((_state & (int)State.CLOSE) != 0)
				return false;
			int remain = (_settings.sumsending - _sendqueue.Count) * _settings.buffersize;
			if (_sendbytes != null)
				remain -= _senduse;
			if (remain < length)
				return false;
			while (length != 0)
			{
				if (_sendbytes == null)
				{
					_sendbytes = Buffer.Acquire(_settings.buffersize);
					_senduse = 0;
				}
				int step = Math.Min(_settings.buffersize - _senduse, length);
				Array.Copy(bytes, offset, _sendbytes, _senduse, step);
				_senduse += step;
				offset += step;
				length -= step;
				if (_senduse == _settings.buffersize)
				{
					_sendqueue.Enqueue(new ArraySegment<byte>(_sendbytes, 0, _senduse));
					_sendbytes = null;
				}
			}
			return true;
		}
		public bool Write(byte[] bytes, int length)
		{
			return Write(bytes, 0, length);
		}
		public bool Write(byte[] bytes)
		{
			return Write(bytes, 0, bytes.Length);
		}
		public bool Write(byte onebyte)
		{
			if ((_state & (int)State.CLOSE) != 0)
				return false;
			int remain = (_settings.sumsending - _sendqueue.Count) * _settings.buffersize;
			if (_sendbytes != null)
				remain -= _senduse;
			if (remain < 1)
				return false;
			if (_sendbytes == null)
			{
				_sendbytes = Buffer.Acquire(_settings.buffersize);
				_senduse = 0;
			}
			_sendbytes[_senduse++] = onebyte;
			if (_senduse == _settings.buffersize)
			{
				_sendqueue.Enqueue(new ArraySegment<byte>(_sendbytes, 0, _senduse));
				_sendbytes = null;
			}
			return true;
		}
		public bool Write(IntPtr ptr, int length)
		{
			if ((_state & (int)State.CLOSE) != 0)
				return false;
			int remain = (_settings.sumsending - _sendqueue.Count) * _settings.buffersize;
			if (_sendbytes != null)
				remain -= _senduse;
			if (remain < length)
				return false;
			while (length != 0)
			{
				if (_sendbytes == null)
				{
					_sendbytes = Buffer.Acquire(_settings.buffersize);
					_senduse = 0;
				}
				int step = Math.Min(_settings.buffersize - _senduse, length);
				Marshal.Copy(ptr, _sendbytes, _senduse, step);
				_senduse += step;
				ptr += step;
				length -= step;
				if (_senduse == _settings.buffersize)
				{
					_sendqueue.Enqueue(new ArraySegment<byte>(_sendbytes, 0, _senduse));
					_sendbytes = null;
				}
			}
			return true;
		}

		public void Flush()
		{
			if ((_state & (int)State.CLOSE) == 0)
			{
				if (_sendbytes != null)
				{
					_sendqueue.Enqueue(new ArraySegment<byte>(_sendbytes, 0, _senduse));
					_sendbytes = null;
				}
				if (AddState(State.SEND))
					PostSend();
			}
		}

		public void Close()
		{
			if (AddState(State.CLOSE))
			{
				_socket.Close();
				AddAction();
				_loop.Release();
				if (_timer != null && _timer.IsRunning)
					_timer.Stop();
			}
		}

		internal abstract void OnReceive(byte[] bytes, int length);
		internal abstract void OnRequestRelease(Request request);

		#region 内部执行Loop Action
		private class HandlerAction
		{
			public Action emit
			{
				get;
				private set;
			}
			public Session session;
			public Exception exception;
			public Request request;
			public int length;
			public HandlerAction()
			{
				emit = () =>
				{
					session.ExecAction(this);
				};
			}

			private static readonly Pool<HandlerAction> pool = new Pool<HandlerAction>();
			public static HandlerAction Acquire()
			{
				return pool.Acquire();
			}
			public void Release()
			{
				pool.Release(this);
			}
		}

		protected void AddAction()
		{
			HandlerAction action = HandlerAction.Acquire();
			action.session = this;
			_loop.Execute(action.emit);
		}

		protected void AddAction(Exception e)
		{
			HandlerAction action = HandlerAction.Acquire();
			action.session = this;
			action.exception = e;
			_loop.Execute(action.emit);
		}

		protected void AddAction(Request request)
		{
			HandlerAction action = HandlerAction.Acquire();
			action.session = this;
			action.request = request;
			_loop.Execute(action.emit);
		}

		protected void AddAction(int length)
		{
			HandlerAction action = HandlerAction.Acquire();
			action.session = this;
			action.length = length;
			_loop.Execute(action.emit);
		}

		private void ExecAction(HandlerAction action)
		{
			if (action.exception != null)
			{
				_settings.exception(this, action.exception);
			}
			else if (action.request != null)
			{
				_settings.request(this, action.request);
				action.request.Execute(this);
				OnRequestRelease(action.request);
			}
			else if (action.length != 0)
			{
				_alivetime = Application.Now;
				if (action.length > 0)
					_settings.read(this, action.length);
				else
					_settings.write(this, -action.length);
			}
			else
			{
				_settings.close(this);
			}
			action.session = null;
			action.exception = null;
			action.request = null;
			action.length = 0;
			action.Release();
		}
		#endregion

		#region 状态控制
		private bool AddState(State state)
		{
			while (true)
			{
				int oldState = _state;
				int newState = _state | (int)state;
				if (oldState == newState)
					return false;
				if (Interlocked.CompareExchange(ref _state, newState, oldState) == oldState)
					return true;
			}
		}

		private void RemoveState(State state)
		{
			while (true)
			{
				int oldState = _state;
				int newState = _state & ~((int)state);
				if (Interlocked.CompareExchange(ref _state, newState, oldState) == oldState)
					break;
			}
		}
		#endregion

		#region 处理接收消息
		private void PostReceive(SocketAsyncEventArgs args)
		{
			try
			{
				if (!_socket.ReceiveAsync(args))
					ProcessReceive(args);
			}
			catch (Exception e)
			{
				AddAction(e);
			}
		}

		private void ProcessReceive(SocketAsyncEventArgs args)
		{
			if (args.SocketError != SocketError.Success)
			{
				if (args.SocketError == SocketError.Disconnecting ||
					args.SocketError == SocketError.ConnectionReset)
				{
					Close();
				}
				else if (args.SocketError != SocketError.OperationAborted)
				{
					AddAction(new SocketException((int)args.SocketError));
				}
				return;
			}
			if (args.BytesTransferred == 0)
			{
				Close();
				return;
			}
			OnReceive(args.Buffer, args.BytesTransferred);
			AddAction(args.BytesTransferred);
			if ((_state & (int)State.CLOSE) == 0)
				PostReceive(args);
		}
		#endregion

		#region 处理发送消息
		private void PostSend()
		{
			IList<ArraySegment<byte>> list = _sendargs.BufferList;
			for (int i = list.Count; i < 2; ++i)
			{
				ArraySegment<byte> seg;
				if (!_sendqueue.TryDequeue(out seg))
					break;
				list.Add(seg);
			}
			if (list.Count == 0)
			{
				RemoveState(State.SEND);
				return;
			}
			try
			{
				_sendargs.BufferList = list;
				if (!_socket.SendAsync(_sendargs))
					ProcessSend();
			}
			catch (Exception e)
			{
				AddAction(e);
			}
		}

		private void ProcessSend()
		{
			if (_sendargs.SocketError != SocketError.Success)
			{
				if (_sendargs.SocketError == SocketError.Disconnecting ||
					_sendargs.SocketError == SocketError.ConnectionReset)
				{
					Close();
				}
				else if (_sendargs.SocketError != SocketError.OperationAborted)
				{
					AddAction(new SocketException((int)_sendargs.SocketError));
				}
				return;
			}
			int length = _sendargs.BytesTransferred;
			AddAction(-length);
			IList<ArraySegment<byte>> list = _sendargs.BufferList;
			int total = 0;
			int index = 0;
			for (int i = 0; i < list.Count; ++i)
			{
				total += list[i].Count;
				if (total > length)
					break;
				++index;
			}
			if (index == list.Count)
			{
				list.Clear();
			}
			else
			{
				for (int i = 0, j = list.Count - index; i < j; ++i)
				{
					list[i] = list[i + index];
				}
				for (int i = list.Count - 1, j = list.Count - index; i >= j; --i)
				{
					list.RemoveAt(i);
				}
				ArraySegment<byte> seg = list[0];
				list[0] = new ArraySegment<byte>(seg.Array, seg.Offset + (seg.Count - (total - length)), total - length);
			}
			if ((_state & (int)State.CLOSE) == 0)
				PostSend();
		}
		#endregion
	}

	public sealed class Session<T> : Session
		where T : Request, new()
	{
		private readonly Pool<T> pool;
		private T request;

		internal Session(Loop loop, ISettings settings, Socket socket)
			: this(loop, settings, socket, Pool<T>.Default) { }

		internal Session(Loop loop, ISettings settings, Socket socket, Pool<T> pool)
			: base(loop, settings, socket)
		{
			this.pool = pool;
			this.request = pool.Acquire();
		}

		internal override void OnReceive(byte[] bytes, int length)
		{
			int offset = 0;
			int size = length - offset;
			try
			{
				while (size > 0 && request.Input(bytes, offset, ref size))
				{
					offset += size;
					size = length - offset;
					var old = request;
					request = pool.Acquire();
					AddAction(old);
				}
			}
			catch (Exception e)
			{
				Close();
				AddAction(e);
			}
		}

		internal override void OnRequestRelease(Request request)
		{
			T t = (T)request;
			t.Reset();
			pool.Release(t);
		}

		public static Task<Session<T>> Connect(Loop loop, ISettings settings, int timeout, Pool<T> pool)
		{
			TaskCompletionSource<Session<T>> evt = new TaskCompletionSource<Session<T>>();
			IPAddress ip;
			if (IPAddress.TryParse(settings.ip, out ip))
			{
				loop.Retain();
				Socket socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, new LingerOption(false, 0));
				socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
				Timer timer = new Timer(state =>
				{
					socket.Close();
				}, null, Timeout.Infinite, Timeout.Infinite);
				socket.BeginConnect(ip, settings.port, result =>
				{
					try
					{
						timer.Dispose();
					}
					catch
					{
					}
					try
					{
						socket.EndConnect(result);
						Session<T> session = new Session<T>(loop, settings, socket, pool);
						if (!evt.TrySetResult(session))
						{
							socket.Close();
							return;
						}
						loop.Execute(() =>
						{
							session.Start();
						});
					}
					catch (ObjectDisposedException)
					{
						evt.TrySetException(new TimeoutException());
					}
					catch (Exception e)
					{
						evt.TrySetException(e);
					}
					finally
					{
						loop.Release();
					}
				}, null);
				timer.Change(timeout, Timeout.Infinite);
			}
			else
			{
				loop.Retain();
				Dns.BeginGetHostAddresses(settings.ip, ar =>
				{
					try
					{
						IPAddress[] iplist = Dns.EndGetHostAddresses(ar);
						AddressFamily family = AddressFamily.InterNetworkV6;
						for (int i = 0; i < iplist.Length; ++i)
						{
							if (iplist[i].AddressFamily == AddressFamily.InterNetwork)
							{
								family = AddressFamily.InterNetwork;
								break;
							}
						}
						loop.Retain();
						Socket socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp);
						socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, new LingerOption(false, 0));
						socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
						Timer timer = new Timer(state =>
						{
							socket.Close();
						}, null, Timeout.Infinite, Timeout.Infinite);
						socket.BeginConnect(iplist, settings.port, result =>
						{
							try
							{
								timer.Dispose();
							}
							catch
							{
							}
							try
							{
								socket.EndConnect(result);
								Session<T> session = new Session<T>(loop, settings, socket, pool);
								if (!evt.TrySetResult(session))
								{
									socket.Close();
									return;
								}
								loop.Execute(() =>
								{
									session.Start();
								});
							}
							catch (ObjectDisposedException)
							{
								evt.TrySetException(new TimeoutException());
							}
							catch (Exception e)
							{
								evt.TrySetException(e);
							}
							finally
							{
								loop.Release();
							}
						}, null);
						timer.Change(timeout, Timeout.Infinite);
					}
					catch (Exception e)
					{
						evt.TrySetException(e);
					}
					finally
					{
						loop.Release();
					}
				}, null);
			}
			return evt.Task;
		}
		public static Task<Session<T>> Connect(ISettings settings, int timeout, Pool<T> pool)
		{
			return Connect(Loop.Current, settings, timeout, pool);
		}
		public static Task<Session<T>> Connect(Loop loop, ISettings settings, int timeout)
		{
			return Connect(loop, settings, timeout, Pool<T>.Default);
		}
		public static Task<Session<T>> Connect(ISettings settings, int timeout)
		{
			return Connect(Loop.Current, settings, timeout, Pool<T>.Default);
		}
	}
}
