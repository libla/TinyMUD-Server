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
					Config logs = config["Log"];
					if (logs.IsArray())
					{
						foreach (Config log in logs)
						{
							Log.Sink sink = Application.CreateType(log) as Log.Sink;
							if (sink == null)
								throw new TypeLoadException();
							Log.Add(sink);
						}
					}
					else
					{
						Log.Sink sink = Application.CreateType(logs) as Log.Sink;
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
