using System;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace TinyMUD.Extension
{
	public static partial class Protocol
	{
		public static Reader JSONReader()
		{
			return new JsonReader();
		}

		public static Writer JSONWriter()
		{
			return new JsonWriter();
		}

		private class JsonReader : Reader
		{
			public override void Skip(Stream stream)
			{
				throw new NotImplementedException();
			}

			protected override void ReadTable(Stream stream, Action<Reader, int> readtype)
			{
				throw new NotImplementedException();
			}

			protected override void ReadArray(Stream stream, Action<Reader> readarray)
			{
				throw new NotImplementedException();
			}

			protected override bool ReadBool(Stream stream)
			{
				throw new NotImplementedException();
			}

			protected override int ReadInt(Stream stream)
			{
				throw new NotImplementedException();
			}

			protected override double ReadFloat(Stream stream)
			{
				throw new NotImplementedException();
			}

			protected override string ReadString(Stream stream)
			{
				throw new NotImplementedException();
			}
		}

		private class JsonWriter : Writer
		{
			private Stream stream;

			#region 转义字符表
			private static readonly char[] escapes = {
				/*
					This array maps the 128 ASCII characters into character escape.
					'u' indicate must transform to \u00xx.
				*/
				'u', 'u', 'u', 'u', 'u', 'u', 'u', 'u', 'b', 't', 'n', 'u', 'f',  'r', 'u', 'u',
				'u', 'u', 'u', 'u', 'u', 'u', 'u', 'u', 'u', 'u', 'u', 'u', 'u',  'u', 'u', 'u',
				'#', '#', '"', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#',  '#', '#', '#',
				'#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#',  '#', '#', '#',
				'#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#',  '#', '#', '#',
				'#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '\\', '#', '#', '#',
				'#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#',  '#', '#', '#',
				'#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#',  '#', '#', '#',
			};
			#endregion

			private static readonly byte[] truebytes = { (byte)'t', (byte)'r', (byte)'u', (byte)'e' };
			private static readonly byte[] falsebytes = { (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e' };
			private static readonly byte[] unicodebytes = { (byte)'\\', (byte)'u', (byte)'0', (byte)'0' };

			protected override void WriteTable(Cursor cursor)
			{
				stream.WriteByte((byte)'{');
				bool result = cursor.MoveNext();
				if (result)
				{
					cursor.Execute(this);
					while (cursor.MoveNext())
					{
						stream.WriteByte((byte)',');
						cursor.Execute(this);
					}
				}
				stream.WriteByte((byte)'}');
			}

			protected override void WriteArray(Cursor cursor)
			{
				stream.WriteByte((byte)'[');
				bool result = cursor.MoveNext();
				if (result)
				{
					cursor.Execute(this);
					while (cursor.MoveNext())
					{
						stream.WriteByte((byte)',');
						cursor.Execute(this);
					}
				}
				stream.WriteByte((byte)']');
			}

			protected override void WriteKey(int key)
			{
				WriteString("@" + key.ToString());
				stream.WriteByte((byte)':');
			}

			protected override void WriteBool(bool b)
			{
				stream.WriteBytes(b ? truebytes : falsebytes);
			}

			protected override void WriteInt(int i)
			{
				string s = i.ToString(CultureInfo.InvariantCulture);
				for (int m = 0, n = s.Length; m < n; ++m)
				{
					stream.WriteByte((byte)s[m]);
				}
			}

			protected override void WriteFloat(double d)
			{
				if (double.IsNaN(d) || double.IsInfinity(d))
					throw new ArgumentException();
				string s = d.ToString(CultureInfo.InvariantCulture);
				for (int m = 0, n = s.Length; m < n; ++m)
				{
					stream.WriteByte((byte)s[m]);
				}
			}

			protected override void WriteString(string s)
			{
				stream.WriteByte((byte)'"');
				for (int i = 0, j = s.Length; i < j; ++i)
				{
					char c = s[i];
					if (c < 128)
					{
						char esc = escapes[c];
						if (esc == '#')
						{
							stream.WriteByte((byte)c);
						}
						else if (esc == 'u')
						{
							stream.WriteBytes(unicodebytes, 0, 4);
							int n = c;
							if (n < 16)
							{
								stream.WriteByte((byte)'0');
								stream.WriteByte(HexChar(n));
							}
							else
							{
								stream.WriteByte(HexChar(n / 16));
								stream.WriteByte(HexChar(n % 16));
							}
						}
						else
						{
							stream.WriteByte((byte)'\\');
							stream.WriteByte((byte)esc);
						}
					}
					else
					{
						stream.WriteBytes(unicodebytes, 0, 2);
						int n = c;
						if (n < 4096)
						{
							stream.WriteByte((byte)'0');
						}
						else
						{
							stream.WriteByte(HexChar(n / 4096));
							n = n % 4096;
						}
						stream.WriteByte(HexChar(n / 256));
						stream.WriteByte(HexChar((n / 16) % 16));
						stream.WriteByte(HexChar(n % 16));
					}
				}
				stream.WriteByte((byte)'"');
			}

			protected override void Prepare(Stream stream)
			{
				this.stream = stream;
			}

			protected override void Flush()
			{
				stream = null;
			}

			private static byte HexChar(int c)
			{
				if (c < 10)
					return (byte)('0' + c);
				return (byte)('A' + c - 10);
			}
		}
	}
}
