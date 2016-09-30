using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TinyMUD
{
	public static class DataSet
	{
		public interface Key
		{
			bool Read(int index, ref bool b);
			bool Read(int index, ref int i);
			bool Read(int index, ref long l);
			bool Read(int index, ref double d);
			bool Read(int index, ref string str);
			bool Read(int index, ref byte[] bytes);

			bool Write(int index, bool b);
			bool Write(int index, int i);
			bool Write(int index, long l);
			bool Write(int index, double d);
			bool Write(int index, string str);
			bool Write(int index, byte[] bytes);
		}

		public interface Column
		{
			bool Read(string field, ref bool b);
			bool Read(string field, ref int i);
			bool Read(string field, ref long l);
			bool Read(string field, ref double d);
			bool Read(string field, ref string str);
			bool Read(string field, ref byte[] bytes);

			bool Write(string field, bool b);
			bool Write(string field, int i);
			bool Write(string field, long l);
			bool Write(string field, double d);
			bool Write(string field, string str);
			bool Write(string field, byte[] bytes);
		}

		public enum Type
		{
			Bool,
			Integer,
			Long,
			Double,
			String,
			Blob,
		}

		public class Define : IComparable<Define>
		{
			public readonly string Name;

			public IReadOnlyList<Type> Keys
			{
				get { return _keys; }
			}

			public IReadOnlyList<KeyValuePair<string, Type>> Columns
			{
				get { return _columns; }
			}

			private readonly List<Type> _keys = new List<Type>();
			private readonly List<KeyValuePair<string, Type>> _columns = new List<KeyValuePair<string, Type>>();
			private readonly Dictionary<string, Type> _columndict = new Dictionary<string, Type>();

			public Define(string name)
			{
				Name = name;
			}

			public Define AddKey(Type type)
			{
				_keys.Add(type);
				return this;
			}

			public Define AddColumn(string name, Type type)
			{
				_columndict.Add(name, type);
				_columns.Add(new KeyValuePair<string, Type>(name, type));
				return this;
			}

			public Type this[string name]
			{
				get { return _columndict[name]; }
			}

			public int CompareTo(Define other)
			{
				return String.Compare(Name, other.Name, StringComparison.Ordinal);
			}
		}

		public class TypeMismatchException : Exception { }

		public abstract class Writer : IDisposable
		{
			public abstract void Write(Define define, Key key, Column column);
			public abstract void Remove(Define define, Key key);
			public abstract Task Submit();
			public abstract void Clear();
			public abstract void Close();
			public void Dispose()
			{
				Close();
			}
		}

		public abstract class Selector : IDisposable
		{
			public abstract Task<bool> Select(Define define, Key key, Column column);
			public abstract void Select(Define define, Key key);
			public abstract Task<Reader> Execute();
			public abstract Task<Reader> Scan(Define define);
			public abstract void Clear();
			public abstract void Close();
			public void Dispose()
			{
				Close();
			}
		}

		public abstract class Reader : IDisposable
		{
			public abstract Task<bool> Read(Key key, Column column);
			public abstract void Close();
			public void Dispose()
			{
				Close();
			}
		}
	}
}
