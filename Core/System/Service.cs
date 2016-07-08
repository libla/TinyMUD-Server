using System;
using System.Collections.Concurrent;

namespace TinyMUD
{
	public abstract class Service
	{
		protected Loop MainLoop;
		public readonly ConcurrentQueue<Exception> errors;

		protected Service()
		{
			errors = new ConcurrentQueue<Exception>();
		}

		public void Run(Config config)
		{
			MainLoop = Loop.Current;
			Config error = config["Error"];
			if (!error)
				error = config["error"];
			bool fatal = Config.ToBool(error["Fatal"].Value ?? error["fatal"].Value, false);
			if (fatal)
			{
				MainLoop.Catch(e =>
				{
					Log.Error(e);
					throw e;
				});
			}
			else
			{
				int errorlimit = Config.ToInt(error["Limit"].Value ?? error["limit"].Value, 100);
				MainLoop.Catch(e =>
				{
					Log.Error(e);
					errors.Enqueue(e);
					while (errors.Count > errorlimit)
					{
						errors.TryDequeue(out e);
					}
				});
			}
			OnInit(config);
			MainLoop.Run();
			Exit(config);
		}

		internal abstract void OnInit(Config config);
		protected virtual void Exit(Config config) { }
	}

	public abstract class Service<T> : Service where T : Service<T>, new()
	{
		internal override void OnInit(Config config)
		{
			Init(config);
		}

		protected abstract void Init(Config config);

		public static Service Instance()
		{
			return new T();
		}
	}
}
