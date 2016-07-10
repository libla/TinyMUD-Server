using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
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
		private static readonly ConcurrentDictionary<string, Type> cachetypes = new ConcurrentDictionary<string, Type>();

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

		public static Type FindType(string name)
		{
			Type value;
			if (!cachetypes.TryGetValue(name, out value))
			{
				int index = name.LastIndexOf('.');
				if (index == -1)
					index = 0;
				else
					++index;
				string typename = name.Substring(index);
				LinkedList<string> namelist = new LinkedList<string>();
				foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					foreach (Type type in assembly.GetTypes())
					{
						if (type.IsGenericType)
							continue;
						if (type.Name == typename)
						{
							bool omit = false;
							for (Type parent = type; parent != null; parent = parent.DeclaringType)
								namelist.AddFirst(parent.Name);
							if (!string.IsNullOrEmpty(type.Namespace))
							{
								Regex regex = new Regex("^TinyMUD(\\..+)?$");
								if (regex.IsMatch(type.Namespace))
									omit = true;
								else
									namelist.AddFirst(type.Namespace);
							}
							string tname = string.Join(".", namelist);
							if (tname == name || (omit && type.Namespace + "." + tname == name))
							{
								cachetypes.TryAdd(name, type);
								return type;
							}
							namelist.Clear();
						}
					}
				}
			}
			return value;
		}

		public static object CreateType(Config config)
		{
			Type type = FindType(config["Type"].Value);
			if (type == null)
				throw new TypeLoadException();
			Config param = config["Params"];
			if (param.Count == 0)
				return Activator.CreateInstance(type);
			object[] args = new object[param.Count];
			for (int i = 0; i < param.Count; ++i)
			{
				Config pm = param[i];
				string stype = pm["Type"].Value ?? "String";
				string svalue = pm["Value"].Value;
				switch (stype)
				{
				case "Integer":
				case "integer":
					args[i] = int.Parse(svalue);
					break;
				case "Number":
				case "number":
					args[i] = double.Parse(svalue);
					break;
				case "Boolean":
				case "boolean":
					if (svalue == null)
						throw new ArgumentNullException();
					switch (svalue)
					{
					case "True":
					case "true":
					case "Yes":
					case "yes":
					case "Y":
					case "y":
						args[i] = true;
						break;
					case "False":
					case "false":
					case "No":
					case "no":
					case "N":
					case "n":
						args[i] = false;
						break;
					default:
						throw new FormatException();
					}
					break;
				case "String":
				case "string":
					args[i] = svalue;
					break;
				default:
					throw new TypeLoadException();
				}
			}
			return Activator.CreateInstance(type, args);
		}

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
