using System;
using System.Collections.Concurrent;

namespace TinyMUD
{
	public abstract class Service
	{
		protected Loop MainLoop;

		public void Run()
		{
			MainLoop = Loop.Current;
			OnInit();
			MainLoop.Run();
			Exit();
		}

		internal abstract void OnInit();
		protected virtual void Exit() { }
	}

	public abstract class Service<T> : Service where T : Service<T>, new()
	{
		internal override void OnInit()
		{
			Init();
		}

		protected abstract void Init();

		public static T Instance()
		{
			return new T();
		}
	}
}
