using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace TinyMUD
{
	public class DataEntity
	{
		public class KeyAttribute : Attribute
		{
			public DataSet.Type? Type;
			public readonly int Index;

			public KeyAttribute(int index)
			{
				Index = index;
			}
		}

		public class ColumnAttribute : Attribute
		{
			public DataSet.Type? Type;
			public readonly string Name;

			public ColumnAttribute(string name)
			{
				Name = name;
			}
		}

		#region 实现DataSet.Key接口
		public class Key : DataSet.Key
		{
			public bool Read(int index, ref bool b)
			{
				throw new NotImplementedException();
			}

			public bool Read(int index, ref int i)
			{
				throw new NotImplementedException();
			}

			public bool Read(int index, ref long l)
			{
				throw new NotImplementedException();
			}

			public bool Read(int index, ref double d)
			{
				throw new NotImplementedException();
			}

			public bool Read(int index, ref string str)
			{
				throw new NotImplementedException();
			}

			public bool Read(int index, ref byte[] bytes)
			{
				throw new NotImplementedException();
			}

			public bool Write(int index, bool b)
			{
				throw new NotImplementedException();
			}

			public bool Write(int index, int i)
			{
				throw new NotImplementedException();
			}

			public bool Write(int index, long l)
			{
				throw new NotImplementedException();
			}

			public bool Write(int index, double d)
			{
				throw new NotImplementedException();
			}

			public bool Write(int index, string str)
			{
				throw new NotImplementedException();
			}

			public bool Write(int index, byte[] bytes)
			{
				throw new NotImplementedException();
			}
		}
		#endregion

		#region 实现DataSet.Column接口
		public class Column : DataSet.Column
		{
			public bool Read(string field, ref bool b)
			{
				throw new NotImplementedException();
			}

			public bool Read(string field, ref int i)
			{
				throw new NotImplementedException();
			}

			public bool Read(string field, ref long l)
			{
				throw new NotImplementedException();
			}

			public bool Read(string field, ref double d)
			{
				throw new NotImplementedException();
			}

			public bool Read(string field, ref string str)
			{
				throw new NotImplementedException();
			}

			public bool Read(string field, ref byte[] bytes)
			{
				throw new NotImplementedException();
			}

			public bool Write(string field, bool b)
			{
				throw new NotImplementedException();
			}

			public bool Write(string field, int i)
			{
				throw new NotImplementedException();
			}

			public bool Write(string field, long l)
			{
				throw new NotImplementedException();
			}

			public bool Write(string field, double d)
			{
				throw new NotImplementedException();
			}

			public bool Write(string field, string str)
			{
				throw new NotImplementedException();
			}

			public bool Write(string field, byte[] bytes)
			{
				throw new NotImplementedException();
			}
		}
		#endregion

	}
}