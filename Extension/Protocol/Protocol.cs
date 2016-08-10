using System;
using System.Collections.Generic;
using System.IO;

namespace TinyMUD.Extension
{
	public static partial class Protocol
	{
		public interface Value
		{
			void Read(Reader reader, int key);
			IEnumerator<Action<Writer>> Write();
		}

		public static class Union
		{

		}

		public abstract class Reader
		{
			#region 优化GC
			private class HandlerAction<T> where T : Value
			{
				public T handler;
				public readonly Action<Reader, int> action;
				
				public HandlerAction()
				{
					action = (parser, k) => handler.Read(parser, k);
				}
			}

			private class ArrayHandlerAction<T> where T : Value, new()
			{
				public List<T> list;
				public readonly Action<Reader> action;

				public ArrayHandlerAction()
				{
					action = parser =>
					{
						T t = new T();
						parser.ReadValue(ref t);
						list.Add(t);
					};
				}
			}

			private class BoolArrayHandlerAction
			{
				public List<bool> list;
				public readonly Action<Reader> action;

				public BoolArrayHandlerAction()
				{
					action = parser =>
					{
						bool b = default(bool);
						parser.ReadValue(ref b);
						list.Add(b);
					};
				}
			}

			private class IntArrayHandlerAction
			{
				public List<int> list;
				public readonly Action<Reader> action;

				public IntArrayHandlerAction()
				{
					action = parser =>
					{
						int i = default(int);
						parser.ReadValue(ref i);
						list.Add(i);
					};
				}
			}

			private class FloatArrayHandlerAction
			{
				public List<double> list;
				public readonly Action<Reader> action;

				public FloatArrayHandlerAction()
				{
					action = parser =>
					{
						double d = default(double);
						parser.ReadValue(ref d);
						list.Add(d);
					};
				}
			}

			private class StringArrayHandlerAction
			{
				public List<string> list;
				public readonly Action<Reader> action;

				public StringArrayHandlerAction()
				{
					action = parser =>
					{
						string s = default(string);
						parser.ReadValue(ref s);
						list.Add(s);
					};
				}
			}
			#endregion

			public T Load<T>(Stream stream) where T : Value, new()
			{
				Prepare(stream);
				T t = new T();
				ReadValue(ref t);
				Flush();
				return t;
			}

			#region 读取字段

			public void ReadValue<T>(ref T t) where T : Value
			{
				HandlerAction<T> handleract = Pool<HandlerAction<T>>.Default.Acquire();
				handleract.handler = t;
				ReadTable(handleract.action);
				t = handleract.handler;
				Pool<HandlerAction<T>>.Default.Release(handleract);
			}

			public void ReadValue(ref bool b)
			{
				b = ReadBool();
			}

			public void ReadValue(ref int i)
			{
				i = ReadInt();
			}

			public void ReadValue(ref double d)
			{
				d = ReadFloat();
			}

			public void ReadValue(ref string s)
			{
				s = ReadString();
			}

			public void ReadValue<T>(ref List<T> list) where T : Value, new()
			{
				ArrayHandlerAction<T> handleract = Pool<ArrayHandlerAction<T>>.Default.Acquire();
				if (list == null)
					list = new List<T>();
				else
					list.Clear();
				handleract.list = list;
				ReadArray(handleract.action);
				handleract.list = null;
				Pool<ArrayHandlerAction<T>>.Default.Release(handleract);
			}

			public void ReadValue(ref List<bool> list)
			{
				BoolArrayHandlerAction handleract = Pool<BoolArrayHandlerAction>.Default.Acquire();
				if (list == null)
					list = new List<bool>();
				else
					list.Clear();
				handleract.list = list;
				ReadArray(handleract.action);
				handleract.list = null;
				Pool<BoolArrayHandlerAction>.Default.Release(handleract);
			}

			public void ReadValue(ref List<int> list)
			{
				IntArrayHandlerAction handleract = Pool<IntArrayHandlerAction>.Default.Acquire();
				if (list == null)
					list = new List<int>();
				else
					list.Clear();
				handleract.list = list;
				ReadArray(handleract.action);
				handleract.list = null;
				Pool<IntArrayHandlerAction>.Default.Release(handleract);
			}

			public void ReadValue(ref List<double> list)
			{
				FloatArrayHandlerAction handleract = Pool<FloatArrayHandlerAction>.Default.Acquire();
				if (list == null)
					list = new List<double>();
				else
					list.Clear();
				handleract.list = list;
				ReadArray(handleract.action);
				handleract.list = null;
				Pool<FloatArrayHandlerAction>.Default.Release(handleract);
			}

			public void ReadValue(ref List<string> list)
			{
				StringArrayHandlerAction handleract = Pool<StringArrayHandlerAction>.Default.Acquire();
				if (list == null)
					list = new List<string>();
				else
					list.Clear();
				handleract.list = list;
				ReadArray(handleract.action);
				handleract.list = null;
				Pool<StringArrayHandlerAction>.Default.Release(handleract);
			}

			public void ReadValue<T>(ref T[] array) where T : Value, new()
			{
				ArrayHandlerAction<T> handleract = Pool<ArrayHandlerAction<T>>.Default.Acquire();
				handleract.list = new List<T>();
				ReadArray(handleract.action);
				array = handleract.list.ToArray();
				handleract.list = null;
				Pool<ArrayHandlerAction<T>>.Default.Release(handleract);
			}

			public void ReadValue(ref bool[] array)
			{
				BoolArrayHandlerAction handleract = Pool<BoolArrayHandlerAction>.Default.Acquire();
				handleract.list = new List<bool>();
				ReadArray(handleract.action);
				array = handleract.list.ToArray();
				handleract.list = null;
				Pool<BoolArrayHandlerAction>.Default.Release(handleract);
			}

			public void ReadValue(ref int[] array)
			{
				IntArrayHandlerAction handleract = Pool<IntArrayHandlerAction>.Default.Acquire();
				handleract.list = new List<int>();
				ReadArray(handleract.action);
				array = handleract.list.ToArray();
				handleract.list = null;
				Pool<IntArrayHandlerAction>.Default.Release(handleract);
			}

			public void ReadValue(ref double[] array)
			{
				FloatArrayHandlerAction handleract = Pool<FloatArrayHandlerAction>.Default.Acquire();
				handleract.list = new List<double>();
				ReadArray(handleract.action);
				array = handleract.list.ToArray();
				handleract.list = null;
				Pool<FloatArrayHandlerAction>.Default.Release(handleract);
			}

			public void ReadValue(ref string[] array)
			{
				StringArrayHandlerAction handleract = Pool<StringArrayHandlerAction>.Default.Acquire();
				handleract.list = new List<string>();
				ReadArray(handleract.action);
				array = handleract.list.ToArray();
				handleract.list = null;
				Pool<StringArrayHandlerAction>.Default.Release(handleract);
			}
			#endregion

			public abstract void Skip();
			protected abstract void ReadTable(Action<Reader, int> readtype);
			protected abstract void ReadArray(Action<Reader> readarray);
			protected abstract bool ReadBool();
			protected abstract int ReadInt();
			protected abstract double ReadFloat();
			protected abstract string ReadString();
			protected abstract void Prepare(Stream stream);
			protected virtual void Flush() { }
		}

		public abstract class Writer
		{
			private readonly HashSet<object> writed = new HashSet<object>();
			#region 迭代写入接口
			protected interface Cursor
			{
				bool MoveNext();
				void Execute(Writer writer);
			}

			private class TableCursor : Cursor
			{
				public IEnumerator<Action<Writer>> enumerator;

				public bool MoveNext()
				{
					if (!enumerator.MoveNext())
						return false;
					while (enumerator.Current == null)
					{
						if (!enumerator.MoveNext())
							return false;
					}
					return true;
				}

				public void Execute(Writer writer)
				{
					enumerator.Current(writer);
				}
			}

			private class ArrayCursor<T> : Cursor where T : Value
			{
				public IList<T> list;
				public int index;

				public void Reset(IList<T> list)
				{
					this.list = list;
					this.index = 0;
				}

				public bool MoveNext()
				{
					if (index >= list.Count)
						return false;
					++index;
					return true;
				}

				public void Execute(Writer writer)
				{
					writer.WriteValue(list[index - 1]);
				}
			}

			private class BoolArrayCursor : Cursor
			{
				public IList<bool> list;
				public int index;

				public void Reset(IList<bool> list)
				{
					this.list = list;
					this.index = 0;
				}

				public bool MoveNext()
				{
					if (index >= list.Count)
						return false;
					++index;
					return true;
				}

				public void Execute(Writer writer)
				{
					writer.WriteValue(list[index - 1]);
				}
			}

			private class IntArrayCursor : Cursor
			{
				public IList<int> list;
				public int index;

				public void Reset(IList<int> list)
				{
					this.list = list;
					this.index = 0;
				}

				public bool MoveNext()
				{
					if (index >= list.Count)
						return false;
					++index;
					return true;
				}

				public void Execute(Writer writer)
				{
					writer.WriteValue(list[index - 1]);
				}
			}

			private class FloatArrayCursor : Cursor
			{
				public IList<double> list;
				public int index;

				public void Reset(IList<double> list)
				{
					this.list = list;
					this.index = 0;
				}

				public bool MoveNext()
				{
					if (index >= list.Count)
						return false;
					++index;
					return true;
				}

				public void Execute(Writer writer)
				{
					writer.WriteValue(list[index - 1]);
				}
			}

			private class StringArrayCursor : Cursor
			{
				public IList<string> list;
				public int index;

				public void Reset(IList<string> list)
				{
					this.list = list;
					this.index = 0;
				}

				public bool MoveNext()
				{
					if (index >= list.Count)
						return false;
					++index;
					return true;
				}

				public void Execute(Writer writer)
				{
					writer.WriteValue(list[index - 1]);
				}
			}
			#endregion

			#region 优化GC
			private class KeyValue<T> where T : Value
			{
				public int key;
				public T value;
				public readonly Action<Writer> action;

				public KeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(value);
						Pool<KeyValue<T>>.Default.Release(this);
					};
				}
			}

			private class BoolKeyValue
			{
				public int key;
				public bool value;
				public readonly Action<Writer> action;

				public BoolKeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(value);
						Pool<BoolKeyValue>.Default.Release(this);
					};
				}
			}

			private class IntKeyValue
			{
				public int key;
				public int value;
				public readonly Action<Writer> action;

				public IntKeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(value);
						Pool<IntKeyValue>.Default.Release(this);
					};
				}
			}

			private class FloatKeyValue
			{
				public int key;
				public double value;
				public readonly Action<Writer> action;

				public FloatKeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(value);
						Pool<FloatKeyValue>.Default.Release(this);
					};
				}
			}

			private class StringKeyValue
			{
				public int key;
				public string value;
				public readonly Action<Writer> action;

				public StringKeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(value);
						Pool<StringKeyValue>.Default.Release(this);
					};
				}
			}

			private class ArrayKeyValue<T> where T : Value
			{
				public int key;
				public IList<T> list;
				public readonly Action<Writer> action;

				public ArrayKeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(list);
						list = null;
						Pool<ArrayKeyValue<T>>.Default.Release(this);
					};
				}
			}

			private class BoolArrayKeyValue
			{
				public int key;
				public IList<bool> list;
				public readonly Action<Writer> action;

				public BoolArrayKeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(list);
						list = null;
						Pool<BoolArrayKeyValue>.Default.Release(this);
					};
				}
			}

			private class IntArrayKeyValue
			{
				public int key;
				public IList<int> list;
				public readonly Action<Writer> action;

				public IntArrayKeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(list);
						list = null;
						Pool<IntArrayKeyValue>.Default.Release(this);
					};
				}
			}

			private class FloatArrayKeyValue
			{
				public int key;
				public IList<double> list;
				public readonly Action<Writer> action;

				public FloatArrayKeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(list);
						list = null;
						Pool<FloatArrayKeyValue>.Default.Release(this);
					};
				}
			}

			private class StringArrayKeyValue
			{
				public int key;
				public IList<string> list;
				public readonly Action<Writer> action;

				public StringArrayKeyValue()
				{
					action = printer =>
					{
						printer.WriteKey(key);
						printer.WriteValue(list);
						list = null;
						Pool<StringArrayKeyValue>.Default.Release(this);
					};
				}
			}
			#endregion

			public void Print<T>(Stream stream, T t) where T : Value
			{
				writed.Clear();
				Prepare(stream);
				WriteValue(t);
				writed.Clear();
				Flush();
			}

			#region 写入字段
			public static Action<Writer> WriteValue<T>(int key, T t) where T : Value
			{
				if (t == null)
					return null;
				var kv = Pool<KeyValue<T>>.Default.Acquire();
				kv.key = key;
				kv.value = t;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, bool b)
			{
				var kv = Pool<BoolKeyValue>.Default.Acquire();
				kv.key = key;
				kv.value = b;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, int i)
			{
				var kv = Pool<IntKeyValue>.Default.Acquire();
				kv.key = key;
				kv.value = i;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, double d)
			{
				var kv = Pool<FloatKeyValue>.Default.Acquire();
				kv.key = key;
				kv.value = d;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, string s)
			{
				var kv = Pool<StringKeyValue>.Default.Acquire();
				kv.key = key;
				kv.value = s;
				return kv.action;
			}

			public static Action<Writer> WriteValue<T>(int key, List<T> list) where T : Value
			{
				if (list == null || list.Count == 0)
					return null;
				var kv = Pool<ArrayKeyValue<T>>.Default.Acquire();
				kv.key = key;
				kv.list = list;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, List<bool> list)
			{
				if (list == null || list.Count == 0)
					return null;
				var kv = Pool<BoolArrayKeyValue>.Default.Acquire();
				kv.key = key;
				kv.list = list;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, List<int> list)
			{
				if (list == null || list.Count == 0)
					return null;
				var kv = Pool<IntArrayKeyValue>.Default.Acquire();
				kv.key = key;
				kv.list = list;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, List<double> list)
			{
				if (list == null || list.Count == 0)
					return null;
				var kv = Pool<FloatArrayKeyValue>.Default.Acquire();
				kv.key = key;
				kv.list = list;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, List<string> list)
			{
				if (list == null || list.Count == 0)
					return null;
				var kv = Pool<StringArrayKeyValue>.Default.Acquire();
				kv.key = key;
				kv.list = list;
				return kv.action;
			}

			public static Action<Writer> WriteValue<T>(int key, T[] array) where T : Value
			{
				if (array == null || array.Length == 0)
					return null;
				var kv = Pool<ArrayKeyValue<T>>.Default.Acquire();
				kv.key = key;
				kv.list = array;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, bool[] array)
			{
				if (array == null || array.Length == 0)
					return null;
				var kv = Pool<BoolArrayKeyValue>.Default.Acquire();
				kv.key = key;
				kv.list = array;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, int[] array)
			{
				if (array == null || array.Length == 0)
					return null;
				var kv = Pool<IntArrayKeyValue>.Default.Acquire();
				kv.key = key;
				kv.list = array;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, double[] array)
			{
				if (array == null || array.Length == 0)
					return null;
				var kv = Pool<FloatArrayKeyValue>.Default.Acquire();
				kv.key = key;
				kv.list = array;
				return kv.action;
			}

			public static Action<Writer> WriteValue(int key, string[] array)
			{
				if (array == null || array.Length == 0)
					return null;
				var kv = Pool<StringArrayKeyValue>.Default.Acquire();
				kv.key = key;
				kv.list = array;
				return kv.action;
			}
			#endregion

			#region 写入字段真正实现
			private void WriteValue<T>(T t) where T : Value
			{
				using (IEnumerator<Action<Writer>> enumerator = t.Write())
				{
					TableCursor cursor = Pool<TableCursor>.Default.Acquire();
					cursor.enumerator = enumerator;
					WriteTable(cursor);
					cursor.enumerator = null;
					Pool<TableCursor>.Default.Release(cursor);
				}
			}

			private void WriteValue(bool b)
			{
				WriteBool(b);
			}

			private void WriteValue(int i)
			{
				WriteInt(i);
			}

			private void WriteValue(double d)
			{
				WriteFloat(d);
			}

			private void WriteValue(string s)
			{
				WriteString(s ?? "");
			}

			private void WriteValue<T>(IList<T> list) where T : Value
			{
				if (writed.Contains(list))
					throw new StackOverflowException();
				writed.Add(list);
				ArrayCursor<T> cursor = Pool<ArrayCursor<T>>.Default.Acquire();
				cursor.Reset(list);
				WriteArray(cursor);
				cursor.Reset(null);
				Pool<ArrayCursor<T>>.Default.Release(cursor);
			}

			private void WriteValue(IList<bool> list)
			{
				BoolArrayCursor cursor = Pool<BoolArrayCursor>.Default.Acquire();
				cursor.Reset(list);
				WriteArray(cursor);
				cursor.Reset(null);
				Pool<BoolArrayCursor>.Default.Release(cursor);
			}

			private void WriteValue(IList<int> list)
			{
				IntArrayCursor cursor = Pool<IntArrayCursor>.Default.Acquire();
				cursor.Reset(list);
				WriteArray(cursor);
				cursor.Reset(null);
				Pool<IntArrayCursor>.Default.Release(cursor);
			}

			private void WriteValue(IList<double> list)
			{
				FloatArrayCursor cursor = Pool<FloatArrayCursor>.Default.Acquire();
				cursor.Reset(list);
				WriteArray(cursor);
				cursor.Reset(null);
				Pool<FloatArrayCursor>.Default.Release(cursor);
			}

			private void WriteValue(IList<string> list)
			{
				StringArrayCursor cursor = Pool<StringArrayCursor>.Default.Acquire();
				cursor.Reset(list);
				WriteArray(cursor);
				cursor.Reset(null);
				Pool<StringArrayCursor>.Default.Release(cursor);
			}
			#endregion

			protected abstract void WriteTable(Cursor cursor);
			protected abstract void WriteArray(Cursor cursor);
			protected abstract void WriteKey(int key);
			protected abstract void WriteBool(bool b);
			protected abstract void WriteInt(int i);
			protected abstract void WriteFloat(double d);
			protected abstract void WriteString(string s);
			protected abstract void Prepare(Stream stream);
			protected virtual void Flush() {}
		}
	}
}
