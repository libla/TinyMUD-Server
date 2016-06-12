using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TinyMUD
{
	public class Loop
	{
		private static readonly ThreadLocal<Loop> _current = new ThreadLocal<Loop>();

		public abstract class Timer
		{
			public int Time;
			public bool Loop;
			public Action Action;
			public abstract void Start();
			public abstract void Stop();
			public abstract bool IsRunning { get; }

			protected Timer()
			{
				Time = 0;
				Loop = false;
			}

			public static Timer operator + (Timer lhs, Action rhs)
			{
				lhs.Action += rhs;
				return lhs;
			}

			public static Timer operator - (Timer lhs, Action rhs)
			{
				lhs.Action -= rhs;
				return lhs;
			}

			public static Timer Create()
			{
				return Current.CreateTimer();
			}

			public static Timer Create(int time, bool loop)
			{
				Timer timer = Current.CreateTimer();
				timer.Time = time;
				timer.Loop = loop;
				return timer;
			}

			public static Timer Create(int time, bool loop, Action action)
			{
				Timer timer = Current.CreateTimer();
				timer.Time = time;
				timer.Loop = loop;
				timer.Action = action;
				return timer;
			}
		}

		public static Loop Current
		{
			get
			{
				Loop loop = _current.Value;
				if (loop == null)
				{
					loop = new Loop();
					_current.Value = loop;
				}
				return loop;
			}
		}

		private readonly int threadId;
		private int taskCount;
		private readonly SortedSet<TimerImpl> timers;
		private readonly List<Action> timerActions;
		private readonly AutoResetEvent signal;
		private readonly ConcurrentQueue<Action> actions;

		private Loop()
		{
			threadId = Thread.CurrentThread.ManagedThreadId;
			taskCount = 0;
			timers = new SortedSet<TimerImpl>();
			timerActions = new List<Action>();
			signal = new AutoResetEvent(false);
			actions = new ConcurrentQueue<Action>();
		}

		public bool IsCurrent
		{
			get { return Thread.CurrentThread.ManagedThreadId == threadId; }
		}

		public void Execute(Action action)
		{
			if (Thread.CurrentThread.ManagedThreadId == threadId)
			{
				action();
			}
			else
			{
				actions.Enqueue(action);
				signal.Set();
			}
		}

		public void Retain()
		{
			Interlocked.Increment(ref taskCount);
		}

		public void Release()
		{
			Interlocked.Decrement(ref taskCount);
		}

		public void Run()
		{
			if (Thread.CurrentThread.ManagedThreadId != threadId)
				throw new InvalidOperationException();
			while (true)
			{
				Action action;
				while (actions.TryDequeue(out action))
				{
					try
					{
						action();
					}
					catch (Exception e)
					{
						Log.Error(e);
					}
				}
				if (taskCount == 0 && timers.Count == 0)
					break;
				long timeout = 0;
				long now = Application.Now;
				while (timers.Count > 0)
				{
					TimerImpl timer = timers.Min;
					if (timer.elapsed > now)
					{
						timeout = timer.elapsed;
						break;
					}
					timerActions.Add(timer.Action);
					timers.Remove(timer);
					if (timer.Loop)
					{
						AddTimer(timer);
					}
					else
					{
						timer.started = false;
					}
				}
				for (int i = 0, j = timerActions.Count; i < j; ++i)
				{
					try
					{
						timerActions[i]();
					}
					catch (Exception e)
					{
						Log.Error(e);
					}
				}
				timerActions.Clear();
				if (timeout == 0)
				{
					signal.WaitOne(Timeout.Infinite);
				}
				else
				{
					timeout -= Application.Now;
					if (timeout > 0)
					{
						signal.WaitOne(TimeSpan.FromMilliseconds(timeout * 0.001));
					}
					else if (timeout < 0)
					{
						Log.Warning("Overload");
					}
				}
			}
		}

		public void Update()
		{
			if (Thread.CurrentThread.ManagedThreadId == threadId)
			{
				Action action;
				while (actions.TryDequeue(out action))
				{
					try
					{
						action();
					}
					catch (Exception e)
					{
						Log.Error(e);
					}
				}
				long now = Application.Now;
				while (timers.Count > 0)
				{
					TimerImpl timer = timers.Min;
					if (timer.elapsed > now)
						break;
					timerActions.Add(timer.Action);
					timers.Remove(timer);
					if (timer.Loop)
					{
						AddTimer(timer);
					}
					else
					{
						timer.started = false;
					}
				}
				for (int i = 0, j = timerActions.Count; i < j; ++i)
				{
					try
					{
						timerActions[i]();
					}
					catch (Exception e)
					{
						Log.Error(e);
					}
				}
				timerActions.Clear();
			}
		}

		#region 计时器
		private class TimerImpl : Timer, IComparable<TimerImpl>
		{
			private static int total = 0;
			private static readonly ConcurrentStack<int> indexs = new ConcurrentStack<int>();
			private readonly int index;

			public long elapsed;
			public Loop loop;
			public bool started;

			public TimerImpl()
			{
				elapsed = -1;
				started = false;
				if (!indexs.TryPop(out index))
					index = Interlocked.Increment(ref total);
			}

			~TimerImpl()
			{
				indexs.Push(index);
			}

			public override void Start()
			{
				if (!loop.IsCurrent)
					throw new InvalidOperationException();
				if (Time <= 0)
					throw new ArgumentException();
				if (!started)
				{
					started = true;
					elapsed = Application.Now;
					loop.AddTimer(this);
				}
			}

			public override void Stop()
			{
				if (!loop.IsCurrent)
					throw new InvalidOperationException();
				if (started)
				{
					started = false;
					loop.RemoveTimer(this);
				}
			}

			public override bool IsRunning
			{
				get { return started; }
			}

			public int CompareTo(TimerImpl rhs)
			{
				if (elapsed == -1 || rhs.elapsed == -1)
					throw new ArgumentException();
				long result = elapsed - rhs.elapsed;
				if (result < 0)
					return -1;
				if (result > 0)
					return 1;
				return index - rhs.index;
			}
		}
		private Timer CreateTimer()
		{
			return new TimerImpl { loop = this };
		}

		private void AddTimer(TimerImpl timer)
		{
			timer.elapsed += timer.Time * 1000;
			timers.Add(timer);
		}

		private void RemoveTimer(TimerImpl timer)
		{
			timers.Remove(timer);
		}
		#endregion
	}
}
