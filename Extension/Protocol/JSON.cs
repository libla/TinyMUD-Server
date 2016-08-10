using System;
using System.IO;
using System.Globalization;
using System.Runtime.Serialization;
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
			private int token;
			private Stream stream;
			private readonly StringBuilder builder = new StringBuilder();
			private readonly byte[] utf8bits = new byte[8];

			private static readonly byte[] utf8heads = {0xff, 0xfe, 0xfc, 0xf8};

			private static readonly Action<Reader, int> SkipTable = (reader, i) => reader.Skip();
			private static readonly Action<Reader> SkipArray = reader => reader.Skip();

			#region 加速Pow运算
			private static readonly double[] e = { // 1e-0...1e308: 309 * 8 bytes = 2472 bytes
				1e+0,  
				1e+1,  1e+2,  1e+3,  1e+4,  1e+5,  1e+6,  1e+7,  1e+8,  1e+9,  1e+10, 1e+11, 1e+12, 1e+13, 1e+14, 1e+15, 1e+16, 1e+17, 1e+18, 1e+19, 1e+20, 1e+21, 1e+22, 1e+23, 1e+24, 1e+25, 1e+26, 1e+27, 1e+28, 
				1e+29, 1e+30, 1e+31, 1e+32, 1e+33, 1e+34, 1e+35, 1e+36, 1e+37, 1e+38, 1e+39, 1e+40, 1e+41, 1e+42, 1e+43, 1e+44, 1e+45, 1e+46, 1e+47, 1e+48, 1e+49, 1e+50, 1e+51, 1e+52, 1e+53, 1e+54, 1e+55, 1e+56, 
				1e+57, 1e+58, 1e+59, 1e+60, 1e+61, 1e+62, 1e+63, 1e+64, 1e+65, 1e+66, 1e+67, 1e+68, 1e+69, 1e+70, 1e+71, 1e+72, 1e+73, 1e+74, 1e+75, 1e+76, 1e+77, 1e+78, 1e+79, 1e+80, 1e+81, 1e+82, 1e+83, 1e+84, 
				1e+85, 1e+86, 1e+87, 1e+88, 1e+89, 1e+90, 1e+91, 1e+92, 1e+93, 1e+94, 1e+95, 1e+96, 1e+97, 1e+98, 1e+99, 1e+100,1e+101,1e+102,1e+103,1e+104,1e+105,1e+106,1e+107,1e+108,1e+109,1e+110,1e+111,1e+112,
				1e+113,1e+114,1e+115,1e+116,1e+117,1e+118,1e+119,1e+120,1e+121,1e+122,1e+123,1e+124,1e+125,1e+126,1e+127,1e+128,1e+129,1e+130,1e+131,1e+132,1e+133,1e+134,1e+135,1e+136,1e+137,1e+138,1e+139,1e+140,
				1e+141,1e+142,1e+143,1e+144,1e+145,1e+146,1e+147,1e+148,1e+149,1e+150,1e+151,1e+152,1e+153,1e+154,1e+155,1e+156,1e+157,1e+158,1e+159,1e+160,1e+161,1e+162,1e+163,1e+164,1e+165,1e+166,1e+167,1e+168,
				1e+169,1e+170,1e+171,1e+172,1e+173,1e+174,1e+175,1e+176,1e+177,1e+178,1e+179,1e+180,1e+181,1e+182,1e+183,1e+184,1e+185,1e+186,1e+187,1e+188,1e+189,1e+190,1e+191,1e+192,1e+193,1e+194,1e+195,1e+196,
				1e+197,1e+198,1e+199,1e+200,1e+201,1e+202,1e+203,1e+204,1e+205,1e+206,1e+207,1e+208,1e+209,1e+210,1e+211,1e+212,1e+213,1e+214,1e+215,1e+216,1e+217,1e+218,1e+219,1e+220,1e+221,1e+222,1e+223,1e+224,
				1e+225,1e+226,1e+227,1e+228,1e+229,1e+230,1e+231,1e+232,1e+233,1e+234,1e+235,1e+236,1e+237,1e+238,1e+239,1e+240,1e+241,1e+242,1e+243,1e+244,1e+245,1e+246,1e+247,1e+248,1e+249,1e+250,1e+251,1e+252,
				1e+253,1e+254,1e+255,1e+256,1e+257,1e+258,1e+259,1e+260,1e+261,1e+262,1e+263,1e+264,1e+265,1e+266,1e+267,1e+268,1e+269,1e+270,1e+271,1e+272,1e+273,1e+274,1e+275,1e+276,1e+277,1e+278,1e+279,1e+280,
				1e+281,1e+282,1e+283,1e+284,1e+285,1e+286,1e+287,1e+288,1e+289,1e+290,1e+291,1e+292,1e+293,1e+294,1e+295,1e+296,1e+297,1e+298,1e+299,1e+300,1e+301,1e+302,1e+303,1e+304,1e+305,1e+306,1e+307,1e+308,
			};

			private static double Pow10(int x)
			{
				if (x < -308)
					return 0;
				if (x > 308)
					return double.PositiveInfinity;
				return x >= 0 ? e[x] : 1.0 / e[-x];
			}
			#endregion

			public override void Skip()
			{
				SkipWhitespace();
				if (token == -1)
					throw new SerializationException();
				switch ((byte)token)
				{
				case (byte)'{':
					ReadTable(SkipTable);
					break;
				case (byte)'[':
					ReadArray(SkipArray);
					break;
				case (byte)'t':
				case (byte)'f':
					ReadBool();
					break;
				case (byte)'"':
					ReadString();
					break;
				default:
					ReadFloat();
					break;
				}
			}

			protected override void ReadTable(Action<Reader, int> readtype)
			{
				SkipWhitespace();
				if (token != (byte)'{')
					throw new SerializationException();
				while (true)
				{
					Next();
					int key = ReadKey();
					SkipWhitespace();
					if (token != (byte)':')
						throw new SerializationException();
					Next();
					readtype(this, key);
					SkipWhitespace();
					if (token == (byte)'}')
					{
						Step();
						break;
					}
					if (token != (byte)',')
						throw new SerializationException();
				}
			}

			protected override void ReadArray(Action<Reader> readarray)
			{
				SkipWhitespace();
				if (token != (byte)'[')
					throw new SerializationException();
				while (true)
				{
					Next();
					readarray(this);
					SkipWhitespace();
					if (token == (byte)']')
					{
						Step();
						break;
					}
					if (token != (byte)',')
						throw new SerializationException();
				}
			}

			protected override bool ReadBool()
			{
				SkipWhitespace();
				if (token == (byte)'t')
				{
					if (Next() == (byte)'r' || Next() == (byte)'u' || Next() == (byte)'e')
					{
						Step();
						return true;
					}
				}
				else if (token == (byte)'f')
				{
					if (Next() == (byte)'a' || Next() == (byte)'l' || Next() == (byte)'s' || Next() == (byte)'e')
					{
						Step();
						return false;
					}
				}
				throw new SerializationException();
			}

			protected override int ReadInt()
			{
				SkipWhitespace();
				bool minus = false;
				if (token == (byte)'-')
				{
					minus = true;
					Next();
				}
				if (token < (byte)'0' || token > (byte)'9')
					throw new SerializationException();
				uint u = 0;
				while (token >= (byte)'0' && token <= (byte)'9')
				{
					u = u * 10 + (uint)(token - (byte)'0');
					if (u > (uint)int.MaxValue + 1 || (!minus && u > int.MaxValue))
						throw new OverflowException();
					Step();
				}
				return minus ? (int)-u : (int)u;
			}

			protected override double ReadFloat()
			{
				SkipWhitespace();
				bool minus = false;
				if (token == (byte)'-')
				{
					minus = true;
					Next();
				}
				if (token < (byte)'0' || token > (byte)'9')
					throw new SerializationException();
				double d = 0;
				while (token >= (byte)'0' && token <= (byte)'9')
				{
					if (d >= double.MaxValue / 10.0)
						throw new OverflowException();
					d = d * 10 + (token - (byte)'0');
					Step();
				}
				int dot = 0;
				if (token == (byte)'.')
				{
					Next();
					while (token >= (byte)'0' && token <= (byte)'9')
					{
						d = d * 10 + (token - (byte)'0');
						--dot;
						Step();
					}
					if (dot == 0)
						throw new SerializationException();
				}
				int exp = 0;
				if (token == (byte)'E' || token == (byte)'e')
				{
					Next();
					bool expMinus = false;
					if (token == (byte)'+')
					{
						Next();
					}
					else if (token == (byte)'-')
					{
						expMinus = true;
						Next();
					}
					if (token < (byte)'0' || token > (byte)'9')
						throw new SerializationException();
					while (token >= (byte)'0' && token <= (byte)'9')
					{
						exp = exp * 10 + (token - (byte)'0');
						Step();
					}
					if (expMinus)
						exp = -exp;
				}
				return (minus ? -d : d) * Pow10(dot + exp);
			}

			protected override string ReadString()
			{
				if (token != (byte)'"')
					throw new SerializationException();
				while (true)
				{
					Next();
					if (token == (byte)'"')
						break;
					if (token == (byte)'\\')
					{
						Next();
						switch ((byte)token)
						{
						case (byte)'b':
							builder.Append('\b');
							break;
						case (byte)'f':
							builder.Append('\f');
							break;
						case (byte)'n':
							builder.Append('\n');
							break;
						case (byte)'r':
							builder.Append('\r');
							break;
						case (byte)'t':
							builder.Append('\t');
							break;
						case (byte)'"':
							builder.Append('"');
							break;
						case (byte)'\\':
							builder.Append('\\');
							break;
						case (byte)'/':
							builder.Append('/');
							break;
						case (byte)'u':
							{
								int unicode = 0;
								for (int i = 0; i < 4; ++i)
								{
									byte c = Next();
									if (c >= (byte)'a')
										c -= (byte)'a' - 10;
									else if (c >= (byte)'A')
										c -= (byte)'A' - 10;
									else
										c -= (byte)'0';
									unicode <<= 4;
									unicode |= c;
								}
								builder.Append((char)unicode);
							}
							break;
						default:
							throw new SerializationException();
						}
					}
					else if (token >= 0xf0)
					{
						int bits = 4;
						for (int i = 0; i < utf8heads.Length; ++i)
						{
							if (token >= utf8heads[i])
							{
								bits = 8 - i;
								break;
							}
						}
						utf8bits[0] = (byte)token;
						for (int i = 1; i < bits; ++i)
						{
							utf8bits[i] = Next();
						}
						builder.Append(Encoding.UTF8.GetString(utf8bits, 0, bits));
					}
					else if (token >= 0x80)
					{
						int unicode;
						int bits;
						if (token >= 0xe0)
						{
							unicode = token & 0x0f;
							bits = 3;
						}
						else if (token >= 0xc0)
						{
							unicode = token & 0x1f;
							bits = 2;
						}
						else
						{
							throw new SerializationException();
						}
						for (int i = 1; i < bits; ++i)
						{
							unicode = (unicode << 6) | (Next() & 0x3f);
						}
						builder.Append((char)unicode);
					}
					else
					{
						builder.Append((char)token);
					}
				}
				Step();
				string result = builder.ToString();
				builder.Length = 0;
				return result;
			}

			protected override void Prepare(Stream stream)
			{
				this.stream = stream;
				this.token = stream.ReadByte();
			}

			protected override void Flush()
			{
				SkipWhitespace();
				if (token != -1 && token != (byte)'\0')
					throw new SerializationException();
			}

			private int ReadKey()
			{
				SkipWhitespace();
				if (token != (byte)'"')
					throw new SerializationException();
				if (Next() != (byte)'@')
					throw new SerializationException();
				int key = 0;
				while (true)
				{
					byte c = Next();
					if (c == (byte)'"')
					{
						Step();
						return key;
					}
					key = key * 10 + c - (byte)'0';
				}
			}

			private void SkipWhitespace()
			{
				if (token == (byte)' ' || token == (byte)'\n' || token == (byte)'\r' || token == (byte)'\t')
					token = stream.ReadByte();
			}

			private byte Next()
			{
				token = stream.ReadByte();
				if (token == -1)
					throw new SerializationException();
				return (byte)token;
			}

			private void Step()
			{
				token = stream.ReadByte();
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
				stream.WriteByte((byte)'"');
				stream.WriteByte((byte)'@');
				WriteInt(key);
				stream.WriteByte((byte)'"');
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
