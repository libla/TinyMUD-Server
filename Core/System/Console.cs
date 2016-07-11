using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TinyMUD
{
	[InitializeOnLoad]
	public static class Console
	{
		private static bool inputing = false;
		private static readonly BlockingCollection<string> list = new BlockingCollection<string>(new ConcurrentQueue<string>());

		private static readonly WeakTable<Loop, Action<string>> actions = new WeakTable<Loop, Action<string>>();
		private static readonly List<Tuple<Loop, Action<string>>> actionstmp = new List<Tuple<Loop, Action<string>>>();

		static Console()
		{
			object signal = new object();
			object locker = new object();
			System.Console.CancelKeyPress += (sender, args) =>
			{
				args.Cancel = true;
				if (args.SpecialKey == ConsoleSpecialKey.ControlC)
				{
					Monitor.Enter(signal);
					Monitor.Pulse(signal);
					Monitor.Exit(signal);
				}
			};
			new Thread(() =>
			{
				while (true)
				{
					Monitor.Enter(signal);
					do
					{
						if (Application.RunState.IsCancellationRequested)
							return;
					} while (!Monitor.Wait(signal, 1000));
					Monitor.Exit(signal);
					Monitor.Enter(locker);
					inputing = true;
					Monitor.Exit(locker);
					string result = System.Console.In.ReadLine();
					if (Application.RunState.IsCancellationRequested)
						return;
					Monitor.Enter(locker);
					inputing = false;
					Monitor.Pulse(locker);
					Monitor.Exit(locker);
					lock (actions)
					{
						foreach (var kv in actions)
							actionstmp.Add(Tuple.Create(kv.Key, kv.Value));
					}
					for (int i = 0; i < actionstmp.Count; ++i)
					{
						Action<string> action = actionstmp[i].Item2;
						actionstmp[i].Item1.Execute(() =>
						{
							action(result);
						});
					}
					actionstmp.Clear();
				}
			}).Start();
			new Thread(() =>
			{
				while (true)
				{
					string text;
					try
					{
						text = list.Take(Application.RunState);
					}
					catch (OperationCanceledException)
					{
						return;
					}
					Monitor.Enter(locker);
					while (inputing)
					{
						Monitor.Wait(locker, 1000);
						if (Application.RunState.IsCancellationRequested)
							return;
					}
					System.Console.WriteLine(text);
					Monitor.Exit(locker);
				}
			}).Start();
		}

		public static void WriteLine(string text)
		{
			list.Add(text);
		}

		public static void OnInput(Loop loop, Action<string> action)
		{
			lock (actions)
			{
				Action<string> value;
				if (actions.TryGetValue(loop, out value))
					value += action;
				else
					value = action;
				actions[loop] = value;
			}
		}

		public static void OnInput(Action<string> action)
		{
			OnInput(Loop.Current, action);
		}
	}
}
