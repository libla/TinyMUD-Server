using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CommandLine;
using CommandLine.Text;

[AttributeUsage(AttributeTargets.Method)]
public class TestAttribute : Attribute
{
	public string Name { get; private set; }

	public TestAttribute() : this("") { }
	public TestAttribute(string s)
	{
		Name = s;
	}
}

[AttributeUsage(AttributeTargets.Method)]
public class PreTestAttribute : Attribute
{
	public string Name { get; private set; }

	public PreTestAttribute() : this("") { }
	public PreTestAttribute(string s)
	{
		Name = s;
	}
}

static class TestCase
{
	class Options
	{
		[ValueList(typeof(List<string>))]
		public IList<string> Filters { get; set; }

		[Option('n', "count", DefaultValue = 1, HelpText = "number of times to repeat.")]
		public int Count { get; set; }

		[Option('p', "performance", DefaultValue = null, HelpText = "display cost time.")]
		public bool? Performance { get; set; }

		[Option('f', "fatal", DefaultValue = false, HelpText = "stop when catch a exception.")]
		public bool StopOnError { get; set; }

		[HelpOption]
		public string GetUsage()
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
			AssemblyProductAttribute product = (AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute));
			HelpText help = new HelpText(string.Format("{0} v{1}", product.Product, assembly.GetName().Version)) {
				AddDashesToOption = true,
			};
			help.AddOptions(this);
			return help.ToString();
		}

		public bool Parse(string[] args)
		{
			Parser parser = new Parser(settings =>
			{
				settings.CaseSensitive = false;
				settings.IgnoreUnknownArguments = false;
				settings.HelpWriter = Console.Out;
			});
			return parser.ParseArguments(args, this);
		}
	}

	struct Testing
	{
		public string Name;
		public Action<int> Action;
		public Action[] Prepares;
	}

	static Testing[] FindTesting(IList<string> filters)
	{
		Regex expr;
		if (filters.Count == 0)
		{
			expr = new Regex("^.*$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
		}
		else
		{
			string patterns = "^(" + string.Join(")|(", filters) + ")$";
			expr = new Regex(patterns, RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);
		}
		List<Testing> actions = new List<Testing>();
		Dictionary<string, List<Action>> prepares = new Dictionary<string, List<Action>>();
		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			foreach (Type type in assembly.GetTypes())
			{
				MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Static);
				for (int i = 0; i < methods.Length; ++i)
				{
					MethodInfo method = methods[i];
					object[] attrs = method.GetCustomAttributes(false);
					for (int j = 0; j < attrs.Length; ++j)
					{
						TestAttribute attr = attrs[j] as TestAttribute;
						if (attr != null)
						{
							if (expr.IsMatch(attr.Name))
							{
								Action<int> action;
								try
								{
									action = Delegate.CreateDelegate(typeof(Action<int>), method) as Action<int>;
								}
								catch (Exception)
								{
									try
									{
										Action action_ = Delegate.CreateDelegate(typeof(Action), method) as Action;
										action = m =>
										{
											for (int n = 0; n < m; ++n)
												action_();
										};
									}
									catch (Exception e)
									{
										throw new ApplicationException(string.Format("{0}.{1}:{2}", type.FullName, method.Name, e.Message));
									}
								}
								actions.Add(new Testing {Name = attr.Name, Action = action});
								break;
							}
						}
						PreTestAttribute attr_ = attrs[j] as PreTestAttribute;
						if (attr_ != null)
						{
							try
							{
								Action action = Delegate.CreateDelegate(typeof(Action), method) as Action;
								List<Action> actions_;
								if (!prepares.TryGetValue(attr_.Name, out actions_))
								{
									actions_ = new List<Action>();
									prepares.Add(attr_.Name, actions_);
								}
								actions_.Add(action);
								break;
							}
							catch (Exception e)
							{
								throw new ApplicationException(string.Format("{0}.{1}:{2}", type.FullName, method.Name, e.Message));
							}
						}
					}
				}
			}
		}
		List<Tuple<Regex, Action[]>> prepares_ = new List<Tuple<Regex, Action[]>>();
		Action[] empty_ = new Action[0];
		foreach (var prepare in prepares)
		{
			prepares_.Add(Tuple.Create(new Regex(string.Format("^{0}$", prepare.Key), RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline), prepare.Value == null ? empty_ : prepare.Value.ToArray()));
		}
		List<Action> list_ = new List<Action>();
		for (int i = 0; i < actions.Count; ++i)
		{
			Testing testing = actions[i];
			for (int j = 0; j < prepares_.Count; ++j)
			{
				var tuple = prepares_[j];
				if (tuple.Item1.IsMatch(testing.Name))
				{
					list_.AddRange(tuple.Item2);
				}
			}
			testing.Prepares = list_.ToArray();
			list_.Clear();
			actions[i] = testing;
		}
		return actions.ToArray();
	}

	static void Main(string[] args)
	{
		Options options = new Options();
		if (!options.Parse(args))
			return;
		Testing[] testings;
		try
		{
			testings = FindTesting(options.Filters);
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
			return;
		}
		HashSet<Action> prepares = new HashSet<Action>();
		Stopwatch sw = new Stopwatch();
		GC.Collect();
		for (int i = 0; i < testings.Length; ++i)
		{
			GC.Collect();
			Testing testing = testings[i];
			Console.WriteLine("--------TestCase {0} Enter", testing.Name);
			try
			{
				for (int j = 0; j < testing.Prepares.Length; ++j)
				{
					Action action = testing.Prepares[j];
					if (!prepares.Contains(action))
					{
						action();
						prepares.Add(action);
					}
				}
				sw.Restart();
				testing.Action(options.Count);
				sw.Stop();
			}
			catch (Exception e)
			{
				Console.WriteLine("Catch Exception : {0}\n{1}", e.Message, e.StackTrace);
				if (options.StopOnError)
					return;
				continue;
			}
			if ((options.Performance.HasValue && options.Performance.Value) || (!options.Performance.HasValue && options.Count > 1))
			{
				Console.WriteLine("--------TestCase {0} Exit, Cost {1} ms", testing.Name, sw.ElapsedMilliseconds);
			}
			else
			{
				Console.WriteLine("--------TestCase {0} Exit", testing.Name);
			}
		}
	}
}
