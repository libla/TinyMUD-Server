using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace TinyMUD
{
	static class Startup
	{
		class Options
		{
			[ValueList(typeof(List<string>), MaximumElements = 1)]
			public IList<string> Configs { get; set; }

			[OptionList('i', "import", Separator = ':', DefaultValue = new string[0], HelpText = "Import Librarys.")]
			public IList<string> Imports { get; set; }

			[Option('d', "dir", DefaultValue = ".", HelpText = "File search path.")]
			public string Directory { get; set; }

			[HelpOption]
			public string GetUsage()
			{
				Assembly assembly = Assembly.GetExecutingAssembly();
				AssemblyProductAttribute product = (AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute));
				HelpText help = new HelpText(string.Format("{0} {1}", product.Product, assembly.GetName().Version))
				{
					AddDashesToOption = true,
				};
				help.AddOptions(this);
				return help.ToString();
			}

		}
		static void Main(string[] args)
		{
			Options options = new Options();
			{
				Parser parser = new Parser(settings =>
				{
					settings.CaseSensitive = false;
					settings.IgnoreUnknownArguments = false;
					settings.HelpWriter = System.Console.Out;
				});
				if (!parser.ParseArguments(args, options))
					return;
			}
			if (!string.IsNullOrEmpty(options.Directory) && options.Directory != ".")
				Environment.CurrentDirectory = options.Directory;
			if (options.Configs.Count > 0)
			{
				Config config = Config.Load(File.ReadAllBytes(options.Configs[0]));
				string[] imports = new string[options.Imports.Count];
				options.Imports.CopyTo(imports, 0);
				Application.Startup(config, imports);
			}
			else
			{
				string[] imports = new string[options.Imports.Count];
				options.Imports.CopyTo(imports, 0);
				Application.Startup(Config.Empty, imports);
			}
			Console.OnInput(s =>
			{
				if (s == "q" || s == "quit")
					Environment.Exit(0);
			});
			Loop.Current.Run();
			Application.Exit();
		}
	}
}