using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace TinyMUD
{
	[InitializeOnLoad]
	public static class Console
	{
		private static bool inputing = false;
		private static readonly BlockingCollection<string> list = new BlockingCollection<string>(new ConcurrentQueue<string>());
		private static readonly Loop loop;

		public static event Action<string> OnInput;

		static Console()
		{
			loop = Loop.Current;
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
					Task<string> task = System.Console.In.ReadLineAsync();
					try
					{
						task.Wait(Application.RunState);
					}
					catch (OperationCanceledException)
					{
						return;
					}
					Monitor.Enter(locker);
					inputing = false;
					Monitor.Pulse(locker);
					Monitor.Exit(locker);
					string result = task.Result;
					loop.Execute(() =>
					{
						OnInput(result);
					});
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
	}
}
