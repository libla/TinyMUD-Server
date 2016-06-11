using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace TinyMUD
{
	public static class Log
	{
		public enum Level
		{
			Normal,
			Warning,
			Error,
		}

		private static readonly object works_mtx = new object();
		private static List<KeyValuePair<Level, string>> works = new List<KeyValuePair<Level, string>>();
		private static List<KeyValuePair<Level, string>> works_swap = new List<KeyValuePair<Level, string>>();

		private static readonly object working_mtx = new object();
		private static bool working = false;

		private static readonly object sinks_mtx = new object();
		private static Sink sink = null;

		private static readonly ConcurrentBag<Sink> removes = new ConcurrentBag<Sink>();

		static Log()
		{
			debug = true;
			new Thread(() =>
			{
				while (true)
				{
					Monitor.Enter(works_mtx);
					while (works.Count == 0)
					{
						Monitor.Wait(works_mtx, 1000);
						if (Application.RunState.IsCancellationRequested)
							return;
					}
					{
						var tmp = works_swap;
						works_swap = works;
						works = tmp;
					}
					Monitor.Exit(works_mtx);
					lock (working_mtx)
					{
						working = true;
					}
					Sink s;
					lock (sinks_mtx)
					{
						s = sink;
						sink = null;
					}
					for (int i = 0; i != works_swap.Count; ++i)
					{
						var work = works_swap[i];
						for (Sink sk = s; sk != null; sk = sk.next)
						{
							sk.Start(work.Key);
							sk.Write(work.Key, work.Value);
							sk.Flush(work.Key);
						}
					}
					works_swap.Clear();
					lock (sinks_mtx)
					{
						if (sink == null)
						{
							sink = s;
						}
						else if (s != null)
						{
							var tmp = s.prev;
							s.prev = sink.prev;
							sink.prev = tmp;
							s.prev.next = sink;
							sink = s;
						}
					}
					lock (working_mtx)
					{
						working = false;
					}
					while (removes.TryTake(out s))
					{
						Clear(s);
					}
				}
			}).Start();
		}

		public static bool debug
		{
			set;
			private get;
		}

		public static string Prefix
		{
			set
			{

			}
		}

		public static string Suffix
		{
			set
			{

			}
		}

		public static void Add(Sink s)
		{
			if (s.prev != null || s.next != null)
				throw new ArgumentException();
			lock (sinks_mtx)
			{
				if (sink == null)
				{
					sink = s;
					sink.prev = s;
				}
				else
				{
					sink.prev.next = s;
					s.prev = sink.prev;
					sink.prev = s;
				}
			}
		}

		private static void Clear(Sink s)
		{
			lock (sinks_mtx)
			{
				if (sink == s)
				{
					sink = s.next;
				}
				if (s.next != null)
				{
					s.next.prev = s.prev;
				}
				if (s.prev.next != null)
				{
					s.prev.next = s.next;
				}
				s.prev = s.next = null;
			}
		}

		public static void Remove(Sink s)
		{
			lock (working_mtx)
			{
				if (working)
				{
					removes.Add(s);
				}
				else
				{
					Clear(s);
				}
			}
		}

		private static void Write(Level level, string text)
		{
			Monitor.Enter(works_mtx);
			works.Add(new KeyValuePair<Level, string>(level, text));
			Monitor.Pulse(works_mtx);
			Monitor.Exit(works_mtx);
		}
		private static void Write(Level level, string fmt, object arg0)
		{
			Write(level, string.Format(fmt, arg0));
		}
		private static void Write(Level level, string fmt, object arg0, object arg1)
		{
			Write(level, string.Format(fmt, arg0, arg1));
		}
		private static void Write(Level level, string fmt, object arg0, object arg1, object arg2)
		{
			Write(level, string.Format(fmt, arg0, arg1, arg2));
		}
		private static void Write(Level level, string fmt, params object[] args)
		{
			Write(level, string.Format(fmt, args));
		}

		public static void Debug(string text)
		{
			if (debug)
				Write(Level.Normal, text);
		}
		public static void Debug(string fmt, object arg0)
		{
			if (debug)
				Write(Level.Normal, fmt, arg0);
		}
		public static void Debug(string fmt, object arg0, object arg1)
		{
			if (debug)
				Write(Level.Normal, fmt, arg0, arg1);
		}
		public static void Debug(string fmt, object arg0, object arg1, object arg2)
		{
			if (debug)
				Write(Level.Normal, fmt, arg0, arg1, arg2);
		}
		public static void Debug(string fmt, params object[] args)
		{
			if (debug)
				Write(Level.Normal, fmt, args);
		}

		public static void Info(string text)
		{
			Write(Level.Normal, text);
		}
		public static void Info(string fmt, object arg0)
		{
			Write(Level.Normal, fmt, arg0);
		}
		public static void Info(string fmt, object arg0, object arg1)
		{
			Write(Level.Normal, fmt, arg0, arg1);
		}
		public static void Info(string fmt, object arg0, object arg1, object arg2)
		{
			Write(Level.Normal, fmt, arg0, arg1, arg2);
		}
		public static void Info(string fmt, params object[] args)
		{
			Write(Level.Normal, fmt, args);
		}

		public static void Warning(string text)
		{
			Write(Level.Warning, text);
		}
		public static void Warning(string fmt, object arg0)
		{
			Write(Level.Warning, fmt, arg0);
		}
		public static void Warning(string fmt, object arg0, object arg1)
		{
			Write(Level.Warning, fmt, arg0, arg1);
		}
		public static void Warning(string fmt, object arg0, object arg1, object arg2)
		{
			Write(Level.Warning, fmt, arg0, arg1, arg2);
		}
		public static void Warning(string fmt, params object[] args)
		{
			Write(Level.Warning, fmt, args);
		}

		public static void Error(string text)
		{
			Write(Level.Error, text);
		}
		public static void Error(string fmt, object arg0)
		{
			Write(Level.Error, fmt, arg0);
		}
		public static void Error(string fmt, object arg0, object arg1)
		{
			Write(Level.Error, fmt, arg0, arg1);
		}
		public static void Error(string fmt, object arg0, object arg1, object arg2)
		{
			Write(Level.Error, fmt, arg0, arg1, arg2);
		}
		public static void Error(string fmt, params object[] args)
		{
			Write(Level.Error, fmt, args);
		}
		public static void Error(Exception e)
		{
			Write(Level.Error, e.ToString());
		}

		public abstract class Sink
		{
			protected Sink()
			{
				prev = next = null;
			}

			public virtual void Start(Level level) { }
			public virtual void Flush(Level level) { }
			public abstract void Write(Level level, string text);

			internal Sink prev;
			internal Sink next;
		}
	}

	public class ConsoleLog : Log.Sink
	{
		public override void Write(Log.Level level, string text)
		{
			Console.WriteLine(text);
		}
	}
}
