using System;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TinyMUD
{
	[AttributeUsage(AttributeTargets.Class)]
	public class InitializeOnLoad : Attribute
	{
		public readonly Type[] types;
		public InitializeOnLoad(params Type[] types)
		{
			this.types = types;
		}
	}

	[AttributeUsage(AttributeTargets.Class)]
	public class ModuleAttribute : Attribute
	{
		public readonly string Load;
		public readonly string Unload;

		public ModuleAttribute()
		{
			Load = null;
			Unload = null;
		}
	}

	public static class Application
	{
		public static readonly DateTime UTCTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
		private static readonly Stopwatch sinceStartup = new Stopwatch();
		private static double timeStartup;
		private static readonly CancellationTokenSource cts = new CancellationTokenSource();
		private static readonly List<Action> unloads = new List<Action>();

		public static Loop MainLoop { get; private set; }
		public static long Now
		{
			get { return (long)((timeStartup + sinceStartup.Elapsed.TotalMilliseconds) * 1000); }
		}

		public static long Elapsed
		{
			get { return (long)(sinceStartup.Elapsed.TotalMilliseconds * 1000); }
		}

		public static CancellationToken RunState
		{
			get { return cts.Token; }
		}

		private static void SortLoad(Dictionary<Type, InitializeOnLoad> loads, Dictionary<Type, bool> pendings, Type[] orders, Type type, ref int index)
		{
			if (pendings.ContainsKey(type))
				throw new ArgumentException("Cycle.");
			pendings.Add(type, true);
			InitializeOnLoad attr;
			if (loads.TryGetValue(type, out attr))
			{
				for (int i = 0; i < attr.types.Length; ++i)
				{
					SortLoad(loads, pendings, orders, attr.types[i], ref index);
				}
				loads.Remove(type);
				orders[index++] = type;
			}
			pendings.Remove(type);
		}

		public static event Action<Config> Load;
		public static event Action Unload;

		public static void Startup(Config config)
		{
			sinceStartup.Start();
			TimeSpan ts = DateTime.UtcNow - UTCTime;
			timeStartup = ts.TotalMilliseconds;
			Thread.CurrentThread.Name = "TinyMUD.Main";
			MainLoop = Loop.Current;
			Dictionary<Type, InitializeOnLoad> loadtypes = new Dictionary<Type, InitializeOnLoad>();
			List<Type> loadlist = new List<Type>();
			List<Action> loads = new List<Action>();
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				foreach (Type type in assembly.GetTypes())
				{
					object[] attrs = type.GetCustomAttributes(false);
					for (int i = 0; i < attrs.Length; ++i)
					{
						ModuleAttribute attr = attrs[i] as ModuleAttribute;
						if (attr != null)
						{
							if (attr.Load == null)
							{
								try
								{
									Action load = Delegate.CreateDelegate(typeof(Action), type, "Load") as Action;
									if (load != null)
										loads.Add(load);
								}
								catch (MissingMethodException)
								{
								}
								catch (MethodAccessException)
								{
								}
							}
							else
							{
								Action load = Delegate.CreateDelegate(typeof(Action), type, attr.Load) as Action;
								if (load == null)
									throw new MissingMethodException(type.FullName, attr.Load);
								loads.Add(load);
							}
							if (attr.Unload == null)
							{
								try
								{
									Action unload = Delegate.CreateDelegate(typeof(Action), type, "Unload") as Action;
									if (unload != null)
										unloads.Add(unload);
								}
								catch (MissingMethodException)
								{
								}
								catch (MethodAccessException)
								{
								}
							}
							else
							{
								Action unload = Delegate.CreateDelegate(typeof(Action), type, attr.Unload) as Action;
								if (unload == null)
									throw new MissingMethodException(type.FullName, attr.Unload);
								unloads.Add(unload);
							}
							break;
						}
					}
					for (int i = 0; i < attrs.Length; ++i)
					{
						InitializeOnLoad attr = attrs[i] as InitializeOnLoad;
						if (attr != null)
						{
							if (!loadtypes.ContainsKey(type))
							{
								loadtypes.Add(type, attr);
								loadlist.Add(type);
							}
							break;
						}
					}
				}
			}
			loadlist.Sort();
			Type[] loadorders = new Type[loadlist.Count];
			int index = 0;
			Dictionary<Type, bool> pendings = new Dictionary<Type, bool>();
			for (int i = 0; i < loadlist.Count; ++i)
			{
				SortLoad(loadtypes, pendings, loadorders, loadlist[i], ref index);
			}
			for (int i = 0; i < loadorders.Length; ++i)
			{
				RuntimeHelpers.RunClassConstructor(loadorders[i].TypeHandle);
			}
			for (int i = 0; i < loads.Count; ++i)
			{
				loads[i]();
			}
			if (Load != null)
				Load(config);
		}

		public static void Exit()
		{
			if (Unload != null)
				Unload();
			for (int i = unloads.Count - 1; i >= 0; --i)
			{
				unloads[i]();
			}
			cts.Cancel();
		}
	}
}
