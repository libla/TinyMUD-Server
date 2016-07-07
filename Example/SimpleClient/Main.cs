using System;

namespace TinyMUD
{
    public class SimpleClient
    {
		static Session s;
		public static void Load()
		{
			Console.WriteLine("load");
			var task = Session<UTF8StringRequest>.Connect(new Session.Settings("localhost", 12306), 3000);
			try
			{
				task.Wait();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
			s = task.Result;
			UTF8StringRequest request = new UTF8StringRequest();
			Console.OnInput(text =>
			{
				request.Value = text;
				request.Send(s);
				s.Flush();
			});
		}

		public static void Unload()
		{
			Console.WriteLine("unload");
			s.Close();
		}
    }
}
