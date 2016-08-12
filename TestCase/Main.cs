using System;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
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
				settings.HelpWriter = System.Console.Out;
			});
			return parser.ParseArguments(args, this);
		}
	}

	static Tuple<string, Action<int>>[] FindTestCase(IList<string> filters)
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
		List<Tuple<string, Action<int>>> actions = new List<Tuple<string, Action<int>>>();
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
								actions.Add(Tuple.Create(attr.Name, action));
								break;
							}
						}
					}
				}
			}
		}
		return actions.ToArray();
	}

	static void Main(string[] args)
	{
		Options options = new Options();
		if (!options.Parse(args))
			return;
		Tuple<string, Action<int>>[] testcases;
		try
		{
			testcases = FindTestCase(options.Filters);
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
			return;
		}
		Stopwatch sw = new Stopwatch();
		GC.Collect();
		for (int i = 0; i < testcases.Length; ++i)
		{
			GC.Collect();
			var testcase = testcases[i];
			Console.WriteLine("--------TestCase {0} Enter", testcase.Item1);
			try
			{
				sw.Restart();
				testcase.Item2(options.Count);
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
				Console.WriteLine("--------TestCase {0} Exit, Cost {1} ms", testcase.Item1, sw.ElapsedMilliseconds);
			}
			else
			{
				Console.WriteLine("--------TestCase {0} Exit", testcase.Item1);
			}
		}
	}
}
