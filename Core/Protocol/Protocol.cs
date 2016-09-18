using System;
using System.Collections.Generic;
using System.IO;

namespace TinyMUD
{
	public static partial class Protocol
	{
		public interface Value
		{
			void Read(Format format, int key);
			void Write(Format format);
			void Reset();
			Dictionary<int, string> Keys();
		}

		public class Option<T> where T : struct
		{
			public T Value;

			public static implicit operator T(Option<T> option)
			{
				return option.Value;
			}

			public static implicit operator Option<T>(T value)
			{
				Option<T> option = new Option<T> {Value = value};
				return option;
			}
		}

		public struct literal
		{
			public string Value;

			public static implicit operator string(literal l)
			{
				return l.Value;
			}

			public static implicit operator literal(string s)
			{
				literal l = new literal { Value = s };
				return l;
			}
		}

		private static readonly Dictionary<Type, Dictionary<int, string>> AllKeys = new Dictionary<Type, Dictionary<int, string>>();
		public static string NameOfKey(Type type, int key)
		{
			Dictionary<int, string> names;
			if (!AllKeys.TryGetValue(type, out names))
			{
				Value value = Activator.CreateInstance(type) as Value;
				if (value == null)
					throw new ArgumentException();
				names = value.Keys();
				AllKeys.Add(type, names);
			}
			string result;
			if (!names.TryGetValue(key, out result))
			{
				result = string.Format("~unknown({0})", key);
				names.Add(key, result);
			}
			return result;
		}

		public abstract class Format
		{
			private readonly Stack<bool> empty = new Stack<bool>();
			private readonly HashSet<object> writed = new HashSet<object>();

			public T Read<T>(Stream stream) where T : Value, new()
			{
				PrepareRead(stream);
				T t = new T();
				t.Reset();
				ReadValue(ref t);
				FlushRead();
				return t;
			}

			public void Write<T>(Stream stream, T t) where T : Value
			{
				writed.Clear();
				PrepareWrite(stream);
				WriteValue(t);
				writed.Clear();
				FlushWrite();
			}

			#region 重载读取处理
			public abstract void Skip();
			protected abstract void ReadTable(Action<Format, int> readtype);
			protected abstract void ReadArray(Action<Format> readarray);
			protected abstract bool ReadBool();
			protected abstract int ReadInt();
			protected abstract double ReadFloat();
			protected abstract string ReadString();
			protected abstract void PrepareRead(Stream stream);
			protected virtual void FlushRead() { }
			#endregion

			#region 重载写入处理
			protected virtual void WriteTableOpen(Type type) { }
			protected virtual void WriteTableNext() { }
			protected virtual void WriteTableClose() { }
			protected virtual void WriteArrayOpen() { }
			protected virtual void WriteArrayNext() { }
			protected virtual void WriteArrayClose() { }
			protected abstract void WriteKey(int key);
			protected abstract void WriteBool(bool b);
			protected abstract void WriteInt(int i);
			protected abstract void WriteFloat(double d);
			protected abstract void WriteString(string s);
			protected abstract void PrepareWrite(Stream stream);
			protected virtual void FlushWrite() { }
			#endregion

			#region 优化GC
			private class HandlerAction<T> where T : Value
			{
				public T handler;
				public readonly Action<Format, int> action;

				public HandlerAction()
				{
					action = (parser, k) => handler.Read(parser, k);
				}
			}

			private class ArrayHandlerAction<T> where T : Value, new()
			{
				public List<T> list;
				public readonly Action<Format> action;

				public ArrayHandlerAction()
				{
					action = parser =>
					{
						T t = new T();
						t.Reset();
						parser.ReadValue(ref t);
						list.Add(t);
					};
				}
			}

			private class BoolArrayHandlerAction
			{
				public List<bool> list;
				public readonly Action<Format> action;

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
				public readonly Action<Format> action;

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
				public readonly Action<Format> action;

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
				public readonly Action<Format> action;

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

			#region 读取字段
			public void ReadValue<T>(ref T t) where T : Value
			{
				HandlerAction<T> handleract = Pool<HandlerAction<T>>.Default.Acquire();
				handleract.handler = t;
				ReadTable(handleract.action);
				t = handleract.handler;
				Pool<HandlerAction<T>>.Default.Release(handleract);
			}

			public void ReadValue<T>(ref T? t) where T : struct, Value
			{
				T tt = default(T);
				ReadValue(ref tt);
				t = tt;
			}

			public void ReadValue<T>(ref Option<T> t) where T : struct, Value
			{
				T tt = default(T);
				ReadValue(ref tt);
				t = tt;
			}

			public void ReadValue(ref bool b)
			{
				b = ReadBool();
			}

			public void ReadValue(ref bool? b)
			{
				bool bb = default(bool);
				ReadValue(ref bb);
				b = bb;
			}

			public void ReadValue(ref Option<bool> b)
			{
				bool bb = default(bool);
				ReadValue(ref bb);
				b = bb;
			}

			public void ReadValue(ref int i)
			{
				i = ReadInt();
			}

			public void ReadValue(ref int? i)
			{
				int ii = default(int);
				ReadValue(ref ii);
				i = ii;
			}

			public void ReadValue(ref Option<int> i)
			{
				int ii = default(int);
				ReadValue(ref ii);
				i = ii;
			}

			public void ReadValue(ref double d)
			{
				d = ReadFloat();
			}

			public void ReadValue(ref double? d)
			{
				double dd = default(double);
				ReadValue(ref dd);
				d = dd;
			}

			public void ReadValue(ref Option<double> d)
			{
				double dd = default(double);
				ReadValue(ref dd);
				d = dd;
			}

			public void ReadValue(ref literal l)
			{
				l = ReadString();
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

			#region 写入字段
			public void WriteValue<T>(int key, T t) where T : Value
			{
				if (t == null)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue(t);
			}

			public void WriteValue<T>(int key, T? t) where T : struct, Value
			{
				if (t == null)
					return;
				WriteValue(key, t.Value);
			}

			public void WriteValue<T>(int key, Option<T> t) where T : struct, Value
			{
				if (t == null)
					return;
				WriteValue(key, t.Value);
			}

			public void WriteValue(int key, bool b)
			{
				CheckEmpty();
				WriteKey(key);
				WriteValue(b);
			}

			public void WriteValue(int key, bool? b)
			{
				if (b == null)
					return;
				WriteValue(key, b.Value);
			}

			public void WriteValue(int key, Option<bool> b)
			{
				if (b == null)
					return;
				WriteValue(key, b.Value);
			}

			public void WriteValue(int key, int i)
			{
				CheckEmpty();
				WriteKey(key);
				WriteValue(i);
			}

			public void WriteValue(int key, int? i)
			{
				if (i == null)
					return;
				WriteValue(key, i.Value);
			}

			public void WriteValue(int key, Option<int> i)
			{
				if (i == null)
					return;
				WriteValue(key, i.Value);
			}

			public void WriteValue(int key, double d)
			{
				CheckEmpty();
				WriteKey(key);
				WriteValue(d);
			}

			public void WriteValue(int key, double? d)
			{
				if (d == null)
					return;
				WriteValue(key, d.Value);
			}

			public void WriteValue(int key, Option<double> d)
			{
				if (d == null)
					return;
				WriteValue(key, d.Value);
			}

			public void WriteValue(int key, literal l)
			{
				CheckEmpty();
				WriteKey(key);
				WriteValue(l.Value);
			}

			public void WriteValue(int key, string s)
			{
				if (s == null)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue(s);
			}

			public void WriteValue<T>(int key, List<T> list) where T : Value
			{
				if (list == null || list.Count == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<T>)list);
			}

			public void WriteValue(int key, List<bool> list)
			{
				if (list == null || list.Count == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<bool>)list);
			}

			public void WriteValue(int key, List<int> list)
			{
				if (list == null || list.Count == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<int>)list);
			}

			public void WriteValue(int key, List<double> list)
			{
				if (list == null || list.Count == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<double>)list);
			}

			public void WriteValue(int key, List<literal> list)
			{
				if (list == null || list.Count == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<literal>)list);
			}

			public void WriteValue<T>(int key, T[] array) where T : Value
			{
				if (array == null || array.Length == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<T>)array);
			}

			public void WriteValue(int key, bool[] array)
			{
				if (array == null || array.Length == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<bool>)array);
			}

			public void WriteValue(int key, int[] array)
			{
				if (array == null || array.Length == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<int>)array);
			}

			public void WriteValue(int key, double[] array)
			{
				if (array == null || array.Length == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<double>)array);
			}

			public void WriteValue(int key, literal[] array)
			{
				if (array == null || array.Length == 0)
					return;
				CheckEmpty();
				WriteKey(key);
				WriteValue((IList<literal>)array);
			}
			#endregion

			#region 写入字段真正实现
			private void CheckEmpty()
			{
				if (empty.Peek())
				{
					empty.Pop();
					empty.Push(false);
				}
				else
				{
					WriteTableNext();
				}
			}

			private void WriteValue<T>(T t) where T : Value
			{
				WriteTableOpen(typeof(T));
				empty.Push(true);
				t.Write(this);
				empty.Pop();
				WriteTableClose();
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
				WriteArrayOpen();
				int count = list.Count;
				if (count > 0)
					WriteValue(list[0]);
				for (int i = 1; i < count; ++i)
				{
					WriteArrayNext();
					WriteValue(list[i]);
				}
				WriteArrayClose();
			}

			private void WriteValue(IList<bool> list)
			{
				WriteArrayOpen();
				int count = list.Count;
				if (count > 0)
					WriteValue(list[0]);
				for (int i = 1; i < count; ++i)
				{
					WriteArrayNext();
					WriteValue(list[i]);
				}
				WriteArrayClose();
			}

			private void WriteValue(IList<int> list)
			{
				WriteArrayOpen();
				int count = list.Count;
				if (count > 0)
					WriteValue(list[0]);
				for (int i = 1; i < count; ++i)
				{
					WriteArrayNext();
					WriteValue(list[i]);
				}
				WriteArrayClose();
			}

			private void WriteValue(IList<double> list)
			{
				WriteArrayOpen();
				int count = list.Count;
				if (count > 0)
					WriteValue(list[0]);
				for (int i = 1; i < count; ++i)
				{
					WriteArrayNext();
					WriteValue(list[i]);
				}
				WriteArrayClose();
			}

			private void WriteValue(IList<literal> list)
			{
				WriteArrayOpen();
				int count = list.Count;
				if (count > 0)
					WriteValue(list[0].Value);
				for (int i = 1; i < count; ++i)
				{
					WriteArrayNext();
					WriteValue(list[i].Value);
				}
				WriteArrayClose();
			}
			#endregion
		}
	}
}
