using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyMUD
{
	public static class DataEntity
	{
		public interface Key
		{
			bool Input(int index, ref bool b);
			bool Input(int index, ref int i);
			bool Input(int index, ref long l);
			bool Input(int index, ref double d);
			bool Input(int index, ref string str);
			bool Input(int index, ref byte[] bytes);

			bool Output(int index, bool b);
			bool Output(int index, int i);
			bool Output(int index, long l);
			bool Output(int index, double d);
			bool Output(int index, string str);
			bool Output(int index, byte[] bytes);
		}

		public interface Column
		{
			bool Input(string field, ref bool b);
			bool Input(string field, ref int i);
			bool Input(string field, ref long l);
			bool Input(string field, ref double d);
			bool Input(string field, ref string str);
			bool Input(string field, ref byte[] bytes);

			bool Output(string field, bool b);
			bool Output(string field, int i);
			bool Output(string field, long l);
			bool Output(string field, double d);
			bool Output(string field, string str);
			bool Output(string field, byte[] bytes);
		}

		public enum DataType
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

			public IReadOnlyList<DataType> Keys
			{
				get { return _keys; }
			}

			public IReadOnlyList<KeyValuePair<string, DataType>> Columns
			{
				get { return _columns; }
			}

			private readonly List<DataType> _keys = new List<DataType>();
			private readonly List<KeyValuePair<string, DataType>> _columns = new List<KeyValuePair<string, DataType>>();

			public Define(string name)
			{
				Name = name;
			}

			public Define AddKey(DataType type)
			{
				_keys.Add(type);
				return this;
			}

			public Define AddColumn(string name, DataType type)
			{
				_columns.Add(new KeyValuePair<string, DataType>(name, type));
				return this;
			}

			public int CompareTo(Define other)
			{
				return String.Compare(Name, other.Name, StringComparison.Ordinal);
			}
		}

		public abstract class Writer : IDisposable
		{
			public abstract void Write(Define define, Key key, Column column);
			public abstract void Remove(Define define, Key key);
			public abstract Task Submit();
			public abstract void Close();
			public void Dispose()
			{
				Close();
			}
		}

		public abstract class Selector : IDisposable
		{
			public abstract Task Select(Define define, Key key, Column column);
			public abstract void Select(Define define, Key key);
			public abstract Task<Reader> Execute();
			public abstract Task<Reader> Scan(Define define);
			public abstract void Close();
			public void Dispose()
			{
				Close();
			}
		}

		public abstract class Reader
		{
			public abstract Task<bool> Read(Key key, Column column);
		}
	}
}
