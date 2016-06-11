using System;

namespace TinyMUD
{
	static class Startup
	{
		static void Main(string[] args)
		{
			///
			/// 加载DLL
			System.Reflection.Assembly.LoadFrom("sample\\SimpleServer.dll");
			Type current = null;
			foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (current != null)
					break;
				foreach (Type type in assembly.GetTypes())
				{
					string name = type.Name;
					if (!string.IsNullOrEmpty(type.Namespace))
						name = type.Namespace + "." + name;
					if (name == "TinyMUD.SimpleServer")
					{
						current = type;
						break;
					}
				}
			}
			///
			Application.Startup(args);
			Log.Add(new ConsoleLog());
			///
			/// 运行初始化函数
			Action load = Delegate.CreateDelegate(typeof(Action), current, "Load") as Action;
			load();
			///
			Loop.Current.Run();
			///
			/// 运行清理函数
			Action unload = Delegate.CreateDelegate(typeof(Action), current, "Unload") as Action;
			unload();
			///
			Application.Exit();
		}
	}
}
