using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace TinyMUD
{
	public abstract class Server
	{
		#region Server相关的类
		public interface ISettings : Session.ISettings
		{
			int backlog { get; }

			void accept(Server server, Session session);
			void exception(Server server, Exception e);
		}

		public struct Events
		{
			public Action<Server, Session> accept;
			public Action<Server, Exception> exception;
		}

		public class Settings : ISettings
		{
			public string ip { get; set; }
			public int port { get; set; }
			public int backlog { get; set; }
			public int timeout { get; set; }
			public int buffersize { get; set; }
			public int sumsending { get; set; }
			public Events events;
			public Session.Events sessionevents;

			public void accept(Server server, Session session)
			{
				if (events.accept != null)
					events.accept(server, session);
			}

			public void exception(Server server, Exception e)
			{
				if (events.exception != null)
					events.exception(server, e);
				else
					Log.Error(e);
			}

			public void request(Session session, Request request)
			{
				if (sessionevents.request != null)
					sessionevents.request(session, request);
			}

			public void read(Session session, int length)
			{
				if (sessionevents.read != null)
					sessionevents.read(session, length);
			}

			public void write(Session session, int length)
			{
				if (sessionevents.write != null)
					sessionevents.write(session, length);
			}

			public void close(Session session)
			{
				if (sessionevents.close != null)
					sessionevents.close(session);
			}

			public void exception(Session session, Exception e)
			{
				if (sessionevents.exception != null)
					sessionevents.exception(session, e);
				else
					Log.Error(e);
			}

			public Settings(int port)
			{
				this.ip = null;
				this.port = port;
				this.backlog = 256;
				this.timeout = Timeout.Infinite;
				this.buffersize = 65536;
				this.sumsending = 16;
			}

			public Settings(string ip, int port)
				: this(port)
			{
				this.ip = ip;
			}
		}
		#endregion

		protected readonly Loop _loop;
		protected readonly ISettings _settings;
		protected readonly IPEndPoint _endpoint;
		private Socket _listener;
		private readonly SocketAsyncEventArgs[] _accepts;
		private bool _started;

		public Loop Loop
		{
			get { return _loop; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="loop"></param>
		/// <param name="settings"></param>
		protected Server(Loop loop, ISettings settings)
		{
			_loop = loop;
			_settings = settings;
			if (string.IsNullOrEmpty(_settings.ip))
				_endpoint = new IPEndPoint(IPAddress.IPv6Any, _settings.port);
			else if (string.Equals(_settings.ip.Trim(), "localhost", StringComparison.OrdinalIgnoreCase))
				_endpoint = new IPEndPoint(IPAddress.IPv6Loopback, _settings.port);
			else
				_endpoint = new IPEndPoint(IPAddress.Parse(_settings.ip), _settings.port);
			_accepts = new SocketAsyncEventArgs[Math.Min(_settings.backlog, (int)SocketOptionName.MaxConnections)];
			EventHandler<SocketAsyncEventArgs> completed = (sender, args) =>
			{
				ProcessAccept(args);
			};
			for (int i = 0; i < _accepts.Length; ++i)
			{
				SocketAsyncEventArgs args = new SocketAsyncEventArgs();
				args.Completed += completed;
				_accepts[i] = args;
			}
			_started = false;
			Init();
		}

		~Server()
		{
			Stop();
		}

		/// <summary>
		/// 开始侦听连接
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <exception cref="SocketException"></exception>
		public void Start()
		{
			if (_started)
				return;
			_listener.Bind(_endpoint);
			_listener.Listen(_accepts.Length);
			_started = true;
			_loop.Retain();
			for (int i = 0; i < _accepts.Length; ++i)
			{
				PostAccept(_accepts[i]);
			}
		}

		/// <summary>
		/// 停止侦听
		/// </summary>
		public void Stop()
		{
			if (!_started)
				return;
			_loop.Release();
			_started = false;
			_listener.Close();
			Init();
		}

		internal abstract Session OnAccept(Socket socket);

		#region 内部执行Loop Action
		private void Init()
		{
			_listener = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { ExclusiveAddressUse = true };
			_listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
			_listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, new LingerOption(false, 0));
		}

		private class HandlerAction
		{
			public Action emit
			{
				get;
				private set;
			}
			public Server server;
			public Socket socket;
			public Exception exception;
			public HandlerAction()
			{
				emit = () =>
				{
					server.ExecAction(this);
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

		private void AddAction(Socket socket)
		{
			HandlerAction action = HandlerAction.Acquire();
			action.server = this;
			action.socket = socket;
			_loop.Execute(action.emit);
		}

		private void AddAction(Exception e)
		{
			HandlerAction action = HandlerAction.Acquire();
			action.server = this;
			action.exception = e;
			_loop.Execute(action.emit);
		}

		private void ExecAction(HandlerAction action)
		{
			if (action.exception != null)
			{
				_settings.exception(this, action.exception);
			}
			else
			{
				Session session = OnAccept(action.socket);
				session.Start();
				_settings.accept(this, session);
			}
			action.server = null;
			action.socket = null;
			action.exception = null;
			action.Release();
		}
		#endregion

		#region 处理接受连接
		private void PostAccept(SocketAsyncEventArgs args)
		{
			if (!_started)
				return;
			try
			{
				if (!_listener.AcceptAsync(args))
				{
					ProcessAccept(args);
				}
			}
			catch (Exception e)
			{
				AddAction(e);
			}
		}

		private void ProcessAccept(SocketAsyncEventArgs args)
		{
			if (args.SocketError != SocketError.Success)
			{
				if (args.AcceptSocket != null)
				{
					try
					{
						args.AcceptSocket.Close();
					}
					catch
					{
					}
				}
				if (_started)
					AddAction(new SocketException((int)args.SocketError));
			}
			else
			{
				args.AcceptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, new LingerOption(false, 0));
				args.AcceptSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
				AddAction(args.AcceptSocket);
			}
			args.AcceptSocket = null;
			PostAccept(args);
		}
		#endregion
	}

	public sealed class Server<T> : Server
		where T : Request, new()
	{
		private readonly Pool<T> pool;

		public Server(Loop loop, ISettings settings, Pool<T> pool)
			: base(loop, settings)
		{
			this.pool = pool;
		}
		public Server(ISettings settings, Pool<T> pool)
			: this(Loop.Current, settings, pool) { }

		public Server(Loop loop, ISettings settings)
			: this(loop, settings, Pool<T>.Default) { }
		public Server(ISettings settings)
			: this(Loop.Current, settings, Pool<T>.Default) { }

		internal override Session OnAccept(Socket socket)
		{
			return new Session<T>(_loop, _settings, socket, pool);
		}
	}
}
