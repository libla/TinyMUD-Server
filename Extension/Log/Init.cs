using System;
using System.IO;
using System.Text;

namespace TinyMUD.Extension
{
	[InitializeOnLoad]
	internal static class Init
	{
		static Init()
		{
			Application.Load += config =>
			{
				if (config)
				{
					foreach (Config log in config["Log"])
					{
						Log.Sink sink = Application.CreateType(log) as Log.Sink;
						if (sink == null)
							throw new TypeLoadException();
						Log.Add(sink);
					}
				}
				else
				{
					Log.Add(new ConsoleLog());
				}
			};
		}
	}
}
