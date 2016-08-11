using System;
using System.IO;
using System.Text;
using System.Runtime.Serialization;

namespace TinyMUD.Extension
{
	public static partial class Protocol
	{
		public static Format Binary()
		{
			return new BinaryFormat();
		}

		private class BinaryFormat : Format
		{
			private Stream stream;
			private int token;

			private static readonly Action<Format, int> SkipTable = (reader, i) => reader.Skip();
			private static readonly Action<Format> SkipArray = reader => reader.Skip();

			#region 数据类型
			private enum TypeTag
			{
				True = 0x09,
				False = 0x0a,
				String = 0x0b,
				Int = 0x0c,
				Float = 0x0d,
				Array = 0x0e,
				Object = 0x0f,
				End = 0x08,
			}
			#endregion

			#region 字符串字节数组缓存
			private byte[] stringbuffer = new byte[16];

			private void CheckStringBuffer(int len)
			{
				if (len > stringbuffer.Length)
				{
					int l = stringbuffer.Length;
					while (l < len)
						l = l * 2;
					stringbuffer = new byte[l];
				}
			}
			#endregion

			#region 读取处理实现
			public override void Skip()
			{
				if (token == -1)
					throw new SerializationException();
				if (token > 0x7f)
				{
					int high = token >> 4;
					switch ((TypeTag)high)
					{
					case TypeTag.True:
					case TypeTag.False:
						ReadBool();
						break;
					case TypeTag.String:
						ReadString();
						break;
					case TypeTag.Int:
						ReadInt();
						break;
					case TypeTag.Float:
						ReadFloat();
						break;
					case TypeTag.Array:
						ReadArray(SkipArray);
						break;
					case TypeTag.Object:
						ReadTable(SkipTable);
						break;
					default:
						throw new SerializationException();
					}
				}
				else
				{
					Step();
				}
			}

			protected override void ReadTable(Action<Format, int> readtype)
			{
				if (token != (int)TypeTag.Object << 4)
					throw new SerializationException();
				Next();
				while (true)
				{
					int key = ReadInt();
					readtype(this, key);
					if (token == (int)TypeTag.End << 4)
					{
						Step();
						break;
					}
				}
			}

			protected override void ReadArray(Action<Format> readarray)
			{
				if (token != (int)TypeTag.Array << 4)
					throw new SerializationException();
				Next();
				while (true)
				{
					readarray(this);
					if (token == (int)TypeTag.End << 4)
					{
						Step();
						break;
					}
				}
			}

			protected override bool ReadBool()
			{
				if (token == (int)TypeTag.True << 4)
				{
					Step();
					return true;
				}
				if (token == (int)TypeTag.False << 4)
				{
					Step();
					return false;
				}
				throw new SerializationException();
			}

			protected override int ReadInt()
			{
				if (token == -1)
					throw new SerializationException();
				if (token <= 0x7f)
				{
					int n = token;
					Step();
					return n;
				}
				int high = token >> 4;
				if (high != (int)TypeTag.Int)
					throw new SerializationException();
				int low = token & 0x0f;
				uint u = 0;
				for (int i = 0; i < low + 1; ++i)
				{
					u = ((uint)Next() << (i * 8)) | u;
				}
				Step();
				return ZigZag(u);
			}

			protected override double ReadFloat()
			{
				if (token == -1)
					throw new SerializationException();
				int high = token >> 4;
				if (high == (int)TypeTag.Float)
				{
					int low = token & 0x0f;
					int x = (low >> 1) + 1;
					int y = (low & 0x1) + 1;
					ulong m = 0;
					uint e = 0;
					for (int i = 0; i < x; ++i)
					{
						m = ((uint)Next() << (i * 8)) | m;
					}
					for (int i = 0; i < y; ++i)
					{
						e = ((uint)Next() << (i * 8)) | e;
					}
					long mantissa = ZigZag(m);
					int exp = ZigZag(e);
					Step();
					return mantissa * Math.Pow(2, exp);
				}
				return ReadInt();
			}

			protected override string ReadString()
			{
				if (token == -1)
					throw new SerializationException();
				int high = token >> 4;
				if (high != (int)TypeTag.String)
					throw new SerializationException();
				int low = token & 0x0f;
				uint u = 0;
				for (int i = 0; i < low + 1; ++i)
				{
					u = ((uint)Next() << (i * 8)) | u;
				}
				int len = (int)u;
				if (len > stream.Length - stream.Position)
					throw new SerializationException();
				CheckStringBuffer(len);
				stream.ReadBytes(stringbuffer, 0, len);
				Step();
				return Encoding.UTF8.GetString(stringbuffer, 0, len);
			}

			protected override void PrepareRead(Stream stream)
			{
				this.stream = stream;
				this.token = stream.ReadByte();
			}

			protected override void FlushRead()
			{
				stream = null;
				if (token != -1)
					throw new SerializationException();
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

			private static int ZigZag(uint u)
			{
				if (u % 2 == 0)
					return (int)(u / 2);
				return -(int)((u - 1) / 2) - 1;
			}

			private static long ZigZag(ulong u)
			{
				if (u % 2 == 0)
					return (long)(u / 2);
				return -(long)((u - 1) / 2) - 1;
			}
			#endregion

			#region 写入处理实现
			protected override void WriteTable(Cursor cursor)
			{
				int high = (int)TypeTag.Object << 4;
				stream.WriteByte((byte)high);
				bool result = cursor.MoveNext();
				if (result)
				{
					cursor.Execute(this);
					while (cursor.MoveNext())
					{
						cursor.Execute(this);
					}
				}
				high = (int)TypeTag.End << 4;
				stream.WriteByte((byte)high);
			}

			protected override void WriteArray(Cursor cursor)
			{
				int high = (int)TypeTag.Array << 4;
				stream.WriteByte((byte)high);
				bool result = cursor.MoveNext();
				if (result)
				{
					cursor.Execute(this);
					while (cursor.MoveNext())
					{
						cursor.Execute(this);
					}
				}
				high = (int)TypeTag.End << 4;
				stream.WriteByte((byte)high);
			}

			protected override void WriteKey(int key)
			{
				WriteInt(key);
			}

			protected override void WriteBool(bool b)
			{
				int high = (int)(b ? TypeTag.True : TypeTag.False) << 4;
				stream.WriteByte((byte)high);
			}

			protected override void WriteInt(int n)
			{
				if (n >= 0 && n <= 0x7f)
				{
					stream.WriteByte((byte)n);
				}
				else
				{
					uint u = ZigZag(n);
					int low;
					if (u <= 1 << 8)
						low = 1;
					else if (u <= 1 << 16)
						low = 2;
					else if (u <= 1 << 24)
						low = 3;
					else
						low = 4;
					int high = (int)TypeTag.Int << 4;
					stream.WriteByte((byte)(high | (low - 1)));
					for (int i = 0; i < low; ++i)
					{
						stream.WriteByte((byte)(u & 0xff));
						u >>= 8;
					}
				}
			}

			protected override void WriteFloat(double d)
			{
				if (d <= int.MaxValue && d >= int.MinValue && Math.Abs(Math.Round(d) - d) < double.Epsilon)
				{
					WriteInt((int)Math.Round(d));
				}
				else
				{
					long mantissa;
					int exp;
					{
						long bits = BitConverter.DoubleToInt64Bits(d);
						bool minus = (bits < 0);
						exp = (int)((bits >> 52) & 0x7ffL);
						mantissa = bits & 0xfffffffffffffL;
						if (exp == 0)
							++exp;
						else
							mantissa = mantissa | (1L << 52);
						exp -= 1075;

						if (mantissa == 0)
						{
							exp = 0;
						}
						else
						{
							while ((mantissa & 1) == 0)
							{
								mantissa >>= 1;
								++exp;
							}
							mantissa = minus ? -mantissa : mantissa;
						}
					}
					ulong m = ZigZag(mantissa);
					uint e = ZigZag(exp);
					int x;
					if (m <= 1 << 8)
						x = 1;
					else if (m <= 1 << 16)
						x = 2;
					else if (m <= 1 << 24)
						x = 3;
					else if (m <= 1 << 32)
						x = 4;
					else if (m <= 1 << 40)
						x = 5;
					else if (m <= 1 << 48)
						x = 6;
					else
						x = 7;
					int y = e <= x << 8 ? 1 : 2;
					int low = ((x - 1) << 1) | (y - 1);
					int high = (int)TypeTag.Float << 4;
					stream.WriteByte((byte)(high | low));
					for (int i = 0; i < x; ++i)
					{
						stream.WriteByte((byte)(m & 0xff));
						m >>= 8;
					}
					for (int i = 0; i < y; ++i)
					{
						stream.WriteByte((byte)(e & 0xff));
						e >>= 8;
					}
				}
			}

			protected override void WriteString(string s)
			{
				if (s == null)
					s = "";
				CheckStringBuffer(s.Length * 3);
				int len = Encoding.UTF8.GetBytes(s, 0, s.Length, stringbuffer, 0);
				int low;
				if (len <= 1 << 8)
					low = 1;
				else if (len <= 1 << 16)
					low = 2;
				else if (len <= 1 << 24)
					low = 3;
				else
					low = 4;
				int high = (int)TypeTag.String << 4;
				stream.WriteByte((byte)(high | (low - 1)));
				uint u = (uint)len;
				for (int i = 0; i < low; ++i)
				{
					stream.WriteByte((byte)(u & 0xff));
					u >>= 8;
				}
				stream.WriteBytes(stringbuffer, 0, len);
			}

			protected override void PrepareWrite(Stream stream)
			{
				this.stream = stream;
			}

			protected override void FlushWrite()
			{
				stream = null;
			}

			private static uint ZigZag(int i)
			{
				if (i >= 0)
					return (uint)i * 2;
				return (uint)-(i + 1) * 2 + 1;
			}

			private static ulong ZigZag(long i)
			{
				if (i >= 0)
					return (ulong)i * 2;
				return (ulong)-(i + 1) * 2 + 1;
			}
			#endregion
		}
	}
}