using System;
using System.IO;
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

		private static Action FindMethod(Type type, string name, string def)
		{
			if (name != null)
			{
				Action load = Delegate.CreateDelegate(typeof(Action), type, name) as Action;
				if (load == null)
					throw new MissingMethodException(type.FullName, name);
				return load;
			}
			try
			{
				return Delegate.CreateDelegate(typeof(Action), type, def) as Action;
			}
			catch (MissingMethodException)
			{
			}
			catch (MethodAccessException)
			{
			}
			return null;
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
			Dictionary<Assembly, int> assemblies = new Dictionary<Assembly, int>();
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				assemblies[assembly] = 0;
			}
			Dictionary<string, string> imports = new Dictionary<string, string>();
			foreach (Config import in config["Import"])
			{
				string name = import["Name"].Value ?? Guid.NewGuid().ToString();
				if (imports.ContainsKey(name))
					continue;
				string path = import["Path"].Value;
				if (File.Exists(path))
				{
					try
					{
						Assembly assembly = Assembly.LoadFrom(path);
						imports.Add(name, path);
						assemblies[assembly] = imports.Count;
					}
					catch (FileLoadException)
					{
					}
					catch (BadImageFormatException)
					{
					}
				}
			}
			Dictionary<Type, InitializeOnLoad> loadtypes = new Dictionary<Type, InitializeOnLoad>();
			List<Tuple<Type, Assembly>> loadlist = new List<Tuple<Type, Assembly>>();
			List<Tuple<Type, ModuleAttribute, Assembly>> modules = new List<Tuple<Type, ModuleAttribute, Assembly>>();
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
							modules.Add(Tuple.Create(type, attr, assembly));
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
								loadlist.Add(Tuple.Create(type, assembly));
							}
							break;
						}
					}
				}
			}
			loadlist.Sort((x, y) =>
			{
				int prix;
				if (!assemblies.TryGetValue(x.Item2, out prix))
					prix = int.MaxValue;
				int priy;
				if (!assemblies.TryGetValue(y.Item2, out priy))
					priy = int.MaxValue;
				if (prix == priy)
					return string.CompareOrdinal(x.Item1.FullName, y.Item1.FullName);
				return prix - priy;
			});
			modules.Sort((x, y) =>
			{
				int prix;
				if (!assemblies.TryGetValue(x.Item3, out prix))
					prix = int.MaxValue;
				int priy;
				if (!assemblies.TryGetValue(y.Item3, out priy))
					priy = int.MaxValue;
				if (prix == priy)
					return string.CompareOrdinal(x.Item1.FullName, y.Item1.FullName);
				return prix - priy;
			});
			Type[] loadorders = new Type[loadlist.Count];
			int index = 0;
			Dictionary<Type, bool> pendings = new Dictionary<Type, bool>();
			for (int i = 0; i < loadlist.Count; ++i)
			{
				SortLoad(loadtypes, pendings, loadorders, loadlist[i].Item1, ref index);
			}
			for (int i = 0; i < loadorders.Length; ++i)
			{
				RuntimeHelpers.RunClassConstructor(loadorders[i].TypeHandle);
			}
			for (int i = 0; i < modules.Count; ++i)
			{
				Type type = modules[i].Item1;
				ModuleAttribute attr = modules[i].Item2;
				Action load = FindMethod(type, attr.Load, "Load");
				if (load != null)
					load();
				Action unload = FindMethod(type, attr.Unload, "Unload");
				if (unload != null)
					unloads.Add(unload);
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
