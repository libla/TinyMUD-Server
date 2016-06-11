using System;
using System.IO;
using System.Text;

namespace TinyMUD.Logs
{
	public class DateFile : Log.Sink
	{
		private readonly string name;
		private readonly Encoding encoding;
		private int date;
		private FileStream file;
		private byte[] buffer;
		private int capacity;
		private readonly byte[] newline;

		public DateFile(string path, Encoding e)
		{
			name = path;
			encoding = e;
			date = 0;
			file = null;
			capacity = 0;
			newline = encoding.GetBytes("\n");
		}
		public DateFile(string path) : this(path, Encoding.UTF8) { }

		~DateFile()
		{
			if (file != null)
				file.Close();
		}

		public override void Start(Log.Level level)
		{
			DateTime datetime = DateTime.Today;
			int now = datetime.Year * 10000 + datetime.Month * 100 + datetime.Day;
			if (file != null)
			{
				if (date != now)
				{
					file.Close();
					file = null;
				}
			}
			if (file == null)
			{
				date = now;
				try
				{
					file = new FileStream(string.Format("{0}_{1}.log", name, date), FileMode.Append, FileAccess.Write, FileShare.Read);
				}
				catch (IOException)
				{
				}
			}
		}

		public override void Write(Log.Level level, string text)
		{
			if (file != null)
			{
				int size = encoding.GetMaxByteCount(text.Length);
				if (capacity < size)
				{
					capacity = size;
					buffer = new byte[capacity];
				}
				int length = encoding.GetBytes(text, 0, text.Length, buffer, 0);
				file.Write(buffer, 0, length);
				file.Write(newline, 0, newline.Length);
			}
		}

		public override void Flush(Log.Level level)
		{
			if (file != null)
				file.Flush();
		}
	}
}
