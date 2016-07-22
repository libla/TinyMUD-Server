using System;
using System.IO;

namespace TinyMUD
{
	public static class Helper
	{
		public static int ReadBytes(this Stream stream, byte[] buffer)
		{
			return ReadBytes(stream, buffer, 0, buffer.Length);
		}

		public static int ReadBytes(this Stream stream, byte[] buffer, int offset)
		{
			return ReadBytes(stream, buffer, offset, buffer.Length - offset);
		}

		public static int ReadBytes(this Stream stream, byte[] buffer, int offset, int count)
		{
			int total = 0;
			try
			{
				while (total < count)
				{
					int size = stream.Read(buffer, offset + total, count - total);
					if (size == 0)
						break;
					total += size;
				}
			}
			catch (Exception)
			{
				if (total == 0)
					throw;
			}
			return total;
		}

		public static void WriteBytes(this Stream stream, byte[] buffer)
		{
			stream.Write(buffer, 0, buffer.Length);
		}

		public static void WriteBytes(this Stream stream, byte[] buffer, int offset)
		{
			stream.Write(buffer, offset, buffer.Length - offset);
		}

		public static void WriteBytes(this Stream stream, byte[] buffer, int offset, int count)
		{
			stream.Write(buffer, offset, count);
		}
	}
}
