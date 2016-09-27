using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace TinyMUD
{
	public class Loop
	{
		private static readonly ThreadLocal<Loop> _current = new ThreadLocal<Loop>();

		#region 计时器
		public sealed class Timer : ITimer
		{
			#region 自动转换Action类型
			public struct Callback
			{
				public Action<Timer> Action
				{
					get { return action; }
				}

				private Action<Timer> action;

				public static implicit operator Callback(Action action)
				{
					return new Callback { action = timer => action() };
				}

				public static implicit operator Callback(Action<Timer> action)
				{
					return new Callback { action = action };
				}
			}
			#endregion

			public override int GetHashCode()
			{
				return _index;
			}

			void ITimer.AddElapsed()
			{
				_elapsed += Time * 1000;
			}

			void ITimer.SetStop()
			{
				_loop = null;
			}

			private static int total = 0;
			private static readonly ConcurrentStack<int> indexs = new ConcurrentStack<int>();
			private readonly int _index;
			private long _elapsed;
			private Loop _loop;

			public int Time;
			public bool Loop;
			public object Value;
			public Callback Action;

			public Timer()
			{
				Time = 0;
				Loop = false;

				_elapsed = -1;
				_loop = null;
				if (!indexs.TryPop(out _index))
					_index = Interlocked.Increment(ref total);
			}

			public Timer(int time, bool loop)
				: this()
			{
				Time = time;
				Loop = loop;
			}

			public Timer(int time, bool loop, Action action)
				: this()
			{
				Time = time;
				Loop = loop;
				Action = action;
			}

			public Timer(int time, bool loop, Action<Timer> action)
				: this()
			{
				Time = time;
				Loop = loop;
				Action = action;
			}

			~Timer()
			{
				indexs.Push(_index);
			}

			public void Start()
			{
				if (_loop != null)
				{
					if (!_loop.IsCurrent)
						throw new InvalidOperationException();
					return;
				}
				if (Time <= 0)
					throw new ArgumentException();
				_loop = Current;
				_elapsed = Application.Now;
				_loop.AddTimer(this);
			}

			public void Stop()
			{
				if (_loop == null)
					return;
				if (!_loop.IsCurrent)
					throw new InvalidOperationException();
				_loop.RemoveTimer(this);
				_loop = null;
			}

			public bool IsRunning
			{
				get { return _loop != null; }
			}

			public long Elapsed
			{
				get { return _elapsed; }
			}
		}

		private interface ITimer
		{
			void AddElapsed();
			void SetStop();
		}

		private class TimerComparer : IComparer<Timer>
		{
			public int Compare(Timer x, Timer y)
			{
				long result = x.Elapsed - y.Elapsed;
				if (result < 0)
					return -1;
				if (result > 0)
					return 1;
				return x.GetHashCode() - y.GetHashCode();
			}

			public static IComparer<Timer> Default = new TimerComparer();
		}

		private void AddTimer(Timer timer)
		{
			((ITimer)timer).AddElapsed();
			timers.Add(timer);
		}

		private void RemoveTimer(Timer timer)
		{
			timers.Remove(timer);
		}
		#endregion

		public class Event
		{
			public string Message { get; internal set; }
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
		private Action<Exception> errfn;
		private readonly SortedSet<Timer> timers;
		private readonly List<Timer> expireTimers;
		private readonly List<Action<Timer>> expireTimerActions;
		private readonly AutoResetEvent signal;
		private readonly ConcurrentQueue<Action> actions;
		private readonly Dictionary<string, Dictionary<Delegate, Action<Event>>> events;
		private readonly List<Action<Event>> eventActions;
		private readonly ConcurrentDictionary<EventAction, bool> eventsAsync;

		#region dummy类型
		private struct Dummy { }
		#endregion

		private struct EventAction
		{
			public string Message;
			public Delegate Action;
			public Action<Event> Forward;

			public static readonly EventActionCompare Comparer = new EventActionCompare();
		}

		private class EventActionCompare : IEqualityComparer<EventAction>
		{
			public bool Equals(EventAction a, EventAction b)
			{
				if (a.Action != b.Action)
					return false;
				return a.Message == b.Message;
			}

			public int GetHashCode(EventAction o)
			{
				return o.Action.GetHashCode();
			}
		}

		private class HandleEvent
		{
			public Loop Loop;
			public readonly Action Emit;
			public Event Event;

			public HandleEvent()
			{
				Emit = () =>
				{
					if (Loop.eventsAsync.Count > 0)
					{
						ICollection<EventAction> eventActions = Loop.eventsAsync.Keys;
						foreach (var eventAction in eventActions)
						{
							bool flag;
							if (Loop.eventsAsync.TryRemove(eventAction, out flag))
							{
								Dictionary<Delegate, Action<Event>> actions;
								if (!Loop.events.TryGetValue(eventAction.Message, out actions))
								{
									actions = new Dictionary<Delegate, Action<Event>>();
									Loop.events.Add(eventAction.Message, actions);
								}
								if (flag)
								{
									actions[eventAction.Action] = eventAction.Forward;
								}
								else
								{
									actions.Remove(eventAction.Action);
								}
							}
						}
					}
					{
						Dictionary<Delegate, Action<Event>> actions;
						if (Loop.events.TryGetValue(Event.Message, out actions))
						{
							foreach (var action in actions)
							{
								Loop.eventActions.Add(action.Value);
							}
						}
						for (int i = 0; i < Loop.eventActions.Count; ++i)
						{
							try
							{
								Loop.eventActions[i](Event);
							}
							catch (Exception e)
							{
								Loop.errfn(e);
							}
						}
						Loop.eventActions.Clear();
					}
					Pool<HandleEvent>.Default.Release(this);
				};
			}
		}

		private Loop()
		{
			threadId = Thread.CurrentThread.ManagedThreadId;
			taskCount = 0;
			errfn = Log.Error;
			timers = new SortedSet<Timer>(TimerComparer.Default);
			expireTimers = new List<Timer>();
			expireTimerActions = new List<Action<Timer>>();
			signal = new AutoResetEvent(false);
			actions = new ConcurrentQueue<Action>();
			events = new Dictionary<string, Dictionary<Delegate, Action<Event>>>();
			eventActions = new List<Action<Event>>();
			eventsAsync = new ConcurrentDictionary<EventAction, bool>(EventAction.Comparer);
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

		public void Broadcast(string msg)
		{
			Broadcast(msg, new Event());
		}

		public void Broadcast(string msg, Event evt)
		{
			evt.Message = msg;
			HandleEvent handle = Pool<HandleEvent>.Default.Acquire();
			handle.Loop = this;
			handle.Event = evt;
			Execute(handle.Emit);
		}

		public void Link(string msg, Action action)
		{
			eventsAsync.AddOrUpdate(new EventAction
			{
				Message = msg, Action = action, Forward = evt =>
				{
					action();
				}
			}, true, (key, value) =>
			{
				return true;
			});
		}

		public Task Wait(int time)
		{
			TaskCompletionSource<Dummy> source = new TaskCompletionSource<Dummy>();
			Timer timer = new Timer(time, false, () =>
			{
				source.TrySetResult(new Dummy());
			});
			timer.Start();
			return source.Task;
		}

		public Task Wait(Task task)
		{
			Loop loop = Current;
			TaskCompletionSource<Dummy> source = new TaskCompletionSource<Dummy>();
			loop.Retain();
			task.GetAwaiter().OnCompleted(() =>
			{
				loop.Execute(() =>
				{
					if (task.IsCompleted)
						source.TrySetResult(new Dummy());
					else if (task.IsCanceled)
						source.TrySetCanceled();
					else
						source.TrySetException(task.Exception);
				});
				loop.Release();
			});
			return source.Task;
		}

		public Task<T> Wait<T>(Task<T> task)
		{
			Loop loop = Current;
			TaskCompletionSource<T> source = new TaskCompletionSource<T>();
			loop.Retain();
			task.GetAwaiter().OnCompleted(() =>
			{
				loop.Execute(() =>
				{
					if (task.IsCompleted)
						source.TrySetResult(task.Result);
					else if (task.IsCanceled)
						source.TrySetCanceled();
					else
						source.TrySetException(task.Exception);
				});
				loop.Release();
			});
			return source.Task;
		}

		public void Link<T>(string msg, Action<T> action) where T : Event
		{
			eventsAsync.AddOrUpdate(new EventAction
			{
				Message = msg, Action = action, Forward = evt =>
				{
					T e = evt as T;
					if (e == null)
						throw new InvalidCastException();
					action(e);
				}
			}, true, (key, value) =>
			{
				return true;
			});
		}

		public void Unlink(string msg, Action action)
		{
			eventsAsync.AddOrUpdate(new EventAction { Message = msg, Action = action }, false, (key, value) =>
			{
				return false;
			});
		}

		public void Unlink<T>(string msg, Action<T> action) where T : Event
		{
			eventsAsync.AddOrUpdate(new EventAction { Message = msg, Action = action }, false, (key, value) =>
			{
				return false;
			});
		}

		public void Retain()
		{
			Interlocked.Increment(ref taskCount);
		}

		public void Release()
		{
			Interlocked.Decrement(ref taskCount);
			signal.Set();
		}

		public void Catch(Action<Exception> fn)
		{
			errfn = fn;
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
						errfn(e);
					}
				}
				if (taskCount == 0 && timers.Count == 0)
					break;
				long timeout = 0;
				long now = Application.Now;
				while (timers.Count > 0)
				{
					Timer timer = timers.Min;
					if (timer.Elapsed > now)
					{
						timeout = timer.Elapsed;
						break;
					}
					if (timer.Action.Action != null)
					{
						expireTimers.Add(timer);
						expireTimerActions.Add(timer.Action.Action);
					}
					timers.Remove(timer);
					if (timer.Loop)
					{
						AddTimer(timer);
					}
					else
					{
						((ITimer)timer).SetStop();
					}
				}
				for (int i = 0, j = expireTimerActions.Count; i < j; ++i)
				{
					try
					{
						expireTimerActions[i](expireTimers[i]);
					}
					catch (Exception e)
					{
						errfn(e);
					}
				}
				expireTimers.Clear();
				expireTimerActions.Clear();
				if (timeout == 0)
				{
					if (taskCount == 0 && actions.Count == 0)
						break;
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
						errfn(e);
					}
				}
				long now = Application.Now;
				while (timers.Count > 0)
				{
					Timer timer = timers.Min;
					if (timer.Elapsed > now)
						break;
					if (timer.Action.Action != null)
					{
						expireTimers.Add(timer);
						expireTimerActions.Add(timer.Action.Action);
					}
					timers.Remove(timer);
					if (timer.Loop)
					{
						AddTimer(timer);
					}
					else
					{
						((ITimer)timer).SetStop();
					}
				}
				for (int i = 0, j = expireTimerActions.Count; i < j; ++i)
				{
					try
					{
						expireTimerActions[i](expireTimers[i]);
					}
					catch (Exception e)
					{
						errfn(e);
					}
				}
				expireTimers.Clear();
				expireTimerActions.Clear();
			}
		}
	}
}
