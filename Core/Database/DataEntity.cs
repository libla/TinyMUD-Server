using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace TinyMUD
{
	public static class DataEntity
	{
		public class KeyAttribute : Attribute
		{
			public DataSet.Type? Type;
			public readonly uint Index;

			public KeyAttribute(uint index)
			{
				Index = index;
			}

			public KeyAttribute(uint index, DataSet.Type type)
			{
				Index = index;
				Type = type;
			}
		}

		public class ColumnAttribute : Attribute
		{
			public DataSet.Type? Type;
			public readonly string Name;
			public readonly bool Auto;

			public ColumnAttribute()
			{
				Auto = true;
			}

			public ColumnAttribute(DataSet.Type type)
			{
				Auto = true;
				Type = type;
			}

			public ColumnAttribute(string name)
			{
				Name = name;
				Auto = false;
			}

			public ColumnAttribute(string name, DataSet.Type type)
			{
				Name = name;
				Auto = false;
				Type = type;
			}
		}

		#region 非标准类型适配接口
		public interface FormatBoolean
		{
			bool Value { get; set; }
		}

		public interface FormatInteger
		{
			int Value { get; set; }
		}

		public interface FormatLong
		{
			long Value { get; set; }
		}

		public interface FormatDouble
		{
			double Value { get; set; }
		}

		public interface FormatString
		{
			string Value { get; set; }
		}

		public interface FormatBytes
		{
			byte[] Value { get; set; }
		}
		#endregion

		#region 实现DataSet.Key接口
		public class Key<T> : DataSet.Key
		{
			public static readonly MemberInfo[] Members;

			static Key()
			{
				Dictionary<uint, MemberInfo> dict = new Dictionary<uint, MemberInfo>();
				foreach (MemberInfo member in typeof(T).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				{
					if ((member.MemberType & (MemberTypes.Field | MemberTypes.Property)) != 0)
					{
						foreach (Attribute attribute in member.GetCustomAttributes(false))
						{
							KeyAttribute keyattr = attribute as KeyAttribute;
							if (keyattr != null)
							{
								dict.Add(keyattr.Index, member);
								break;
							}
						}
					}
				}
				Members = new MemberInfo[dict.Count];
				foreach (var kv in dict)
				{
					Members[kv.Key] = kv.Value;
				}
			}

			private Func<object, V> Getter<V>(int index)
			{
				Delegate getter;
				if (!getters.TryGetValue(Members[index], out getter))
				{
					getter = getters.GetOrAdd(Members[index], member =>
					{
						if (member.MemberType == MemberTypes.Field)
							return CreateGetter<V>(member as FieldInfo);
						if (member.MemberType == MemberTypes.Property)
							return CreateGetter<V>(member as PropertyInfo);
						throw new MissingFieldException(member.DeclaringType.FullName, member.Name);
					});
				}
				return (Func<object, V>)getter;
			}

			private Action<object, V> Setter<V>(int index)
			{
				Delegate setter;
				if (!setters.TryGetValue(Members[index], out setter))
				{
					setter = setters.GetOrAdd(Members[index], member =>
					{
						if (member.MemberType == MemberTypes.Field)
							return CreateSetter<V>(member as FieldInfo);
						if (member.MemberType == MemberTypes.Property)
							return CreateSetter<V>(member as PropertyInfo);
						throw new MissingFieldException(member.DeclaringType.FullName, member.Name);
					});
				}
				return (Action<object, V>)setter;
			}

			public bool Read(int index, ref bool b)
			{
				try
				{
					b = Getter<bool>(index)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(int index, ref int i)
			{
				try
				{
					i = Getter<int>(index)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(int index, ref long l)
			{
				try
				{
					l = Getter<long>(index)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(int index, ref double d)
			{
				try
				{
					d = Getter<double>(index)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(int index, ref string str)
			{
				try
				{
					str = Getter<string>(index)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(int index, ref byte[] bytes)
			{
				try
				{
					bytes = Getter<byte[]>(index)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(int index, bool b)
			{
				try
				{
					Setter<bool>(index)(this, b);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(int index, int i)
			{
				try
				{
					Setter<int>(index)(this, i);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(int index, long l)
			{
				try
				{
					Setter<long>(index)(this, l);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(int index, double d)
			{
				try
				{
					Setter<double>(index)(this, d);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(int index, string str)
			{
				try
				{
					Setter<string>(index)(this, str);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(int index, byte[] bytes)
			{
				try
				{
					Setter<byte[]>(index)(this, bytes);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}
		}
		#endregion

		#region 实现DataSet.Column接口
		public class Column<T> : DataSet.Column
		{
			public static readonly Dictionary<string, MemberInfo> Members;

			static Column()
			{
				Members = new Dictionary<string, MemberInfo>();
				foreach (MemberInfo member in typeof(T).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				{
					if ((member.MemberType & (MemberTypes.Field | MemberTypes.Property)) != 0)
					{
						foreach (Attribute attribute in member.GetCustomAttributes(false))
						{
							ColumnAttribute columnattr = attribute as ColumnAttribute;
							if (columnattr != null)
							{
								Members.Add(columnattr.Auto ? member.Name : columnattr.Name, member);
								break;
							}
						}
					}
				}
			}

			private Func<object, V> Getter<V>(string name)
			{
				Delegate getter;
				if (!getters.TryGetValue(Members[name], out getter))
				{
					getter = getters.GetOrAdd(Members[name], member =>
					{
						if (member.MemberType == MemberTypes.Field)
							return CreateGetter<V>(member as FieldInfo);
						if (member.MemberType == MemberTypes.Property)
							return CreateGetter<V>(member as PropertyInfo);
						throw new MissingFieldException(member.DeclaringType.FullName, member.Name);
					});
				}
				return (Func<object, V>)getter;
			}

			private Action<object, V> Setter<V>(string name)
			{
				Delegate setter;
				if (!setters.TryGetValue(Members[name], out setter))
				{
					setter = setters.GetOrAdd(Members[name], member =>
					{
						if (member.MemberType == MemberTypes.Field)
							return CreateSetter<V>(member as FieldInfo);
						if (member.MemberType == MemberTypes.Property)
							return CreateSetter<V>(member as PropertyInfo);
						throw new MissingFieldException(member.DeclaringType.FullName, member.Name);
					});
				}
				return (Action<object, V>)setter;
			}

			public bool Read(string field, ref bool b)
			{
				try
				{
					b = Getter<bool>(field)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(string field, ref int i)
			{
				try
				{
					i = Getter<int>(field)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(string field, ref long l)
			{
				try
				{
					l = Getter<long>(field)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(string field, ref double d)
			{
				try
				{
					d = Getter<double>(field)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(string field, ref string str)
			{
				try
				{
					str = Getter<string>(field)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Read(string field, ref byte[] bytes)
			{
				try
				{
					bytes = Getter<byte[]>(field)(this);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(string field, bool b)
			{
				try
				{
					Setter<bool>(field)(this, b);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(string field, int i)
			{
				try
				{
					Setter<int>(field)(this, i);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(string field, long l)
			{
				try
				{
					Setter<long>(field)(this, l);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(string field, double d)
			{
				try
				{
					Setter<double>(field)(this, d);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(string field, string str)
			{
				try
				{
					Setter<string>(field)(this, str);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			public bool Write(string field, byte[] bytes)
			{
				try
				{
					Setter<byte[]>(field)(this, bytes);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}
		}
		#endregion

		public static DataSet.Define GetDefine<TKey, TColumn>(string name)
			where TKey : Key<TKey>
			where TColumn : Column<TColumn>
		{
			DefineIndex index = new DefineIndex
			{
				Name = name,
				Key = typeof(TKey),
				Column = typeof(TColumn)
			};
			DataSet.Define define;
			if (defines.TryGetValue(index, out define))
				return define;
			return defines.GetOrAdd(index, idx =>
			{
				DataSet.Define def = new DataSet.Define(idx.Name);
				Dictionary<uint, DataSet.Type> keytypes = new Dictionary<uint, DataSet.Type>();
				foreach (MemberInfo member in Key<TKey>.Members)
				{
					foreach (Attribute attribute in member.GetCustomAttributes(false))
					{
						KeyAttribute keyattr = attribute as KeyAttribute;
						if (keyattr != null)
						{
							if (keyattr.Type.HasValue)
							{
								keytypes.Add(keyattr.Index, keyattr.Type.Value);
							}
							else
							{
								Type type = member.MemberType == MemberTypes.Field
									? (member as FieldInfo).FieldType
									: (member as PropertyInfo).PropertyType;
								keytypes.Add(keyattr.Index, SelectType(type));
							}
							break;
						}
					}
				}
				Dictionary<string, DataSet.Type> columntypes = new Dictionary<string, DataSet.Type>();
				foreach (MemberInfo member in Column<TColumn>.Members.Values)
				{
					foreach (Attribute attribute in member.GetCustomAttributes(false))
					{
						ColumnAttribute columnattr = attribute as ColumnAttribute;
						if (columnattr != null)
						{
							string columnname = columnattr.Auto ? member.Name : columnattr.Name;
							if (columnattr.Type.HasValue)
							{
								columntypes.Add(columnname, columnattr.Type.Value);
							}
							else
							{
								Type type = member.MemberType == MemberTypes.Field
									? (member as FieldInfo).FieldType
									: (member as PropertyInfo).PropertyType;
								columntypes.Add(columnname, SelectType(type));
							}
							break;
						}
					}
				}
				for (uint i = 0; i < keytypes.Count; ++i)
				{
					def.AddKey(keytypes[i]);
				}
				foreach (var kv in columntypes)
				{
					def.AddColumn(kv.Key, kv.Value);
				}
				return def;
			});
		}

		#region 自动根据字段类型确定数据类型
		private static DataSet.Type SelectType(Type type)
		{
			DataSet.Type datatype = DataSet.Type.Blob;
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
				datatype = DataSet.Type.Bool;
				break;
			case TypeCode.SByte:
			case TypeCode.Byte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.UInt32:
				datatype = DataSet.Type.Integer;
				break;
			case TypeCode.Int64:
			case TypeCode.UInt64:
				datatype = DataSet.Type.Long;
				break;
			case TypeCode.Single:
			case TypeCode.Double:
			case TypeCode.Decimal:
				datatype = DataSet.Type.Double;
				break;
			case TypeCode.Char:
			case TypeCode.String:
				datatype = DataSet.Type.String;
				break;
			}
			return datatype;
		}
		#endregion

		#region DataSet.Define内部缓存，以确保名字、Key、Column组合总是指向同一个DataSet.Define
		private static readonly ConcurrentDictionary<DefineIndex, DataSet.Define> defines =
			new ConcurrentDictionary<DefineIndex, DataSet.Define>(DefineIndexComparer.Default);

		private struct DefineIndex
		{
			public string Name;
			public Type Key;
			public Type Column;
		}

		private class DefineIndexComparer : IEqualityComparer<DefineIndex>
		{
			private DefineIndexComparer() { }

			public bool Equals(DefineIndex x, DefineIndex y)
			{
				return x.Name == y.Name && x.Key == y.Key && x.Column == y.Column;
			}

			public int GetHashCode(DefineIndex obj)
			{
				return obj.Name.GetHashCode() ^ obj.Key.GetHashCode() ^ obj.Column.GetHashCode();
			}

			public static readonly DefineIndexComparer Default = new DefineIndexComparer();
		}
		#endregion

		#region 创建访问器
		private static readonly ConcurrentDictionary<MemberInfo, Delegate> getters =
				new ConcurrentDictionary<MemberInfo, Delegate>();
		private static readonly ConcurrentDictionary<MemberInfo, Delegate> setters =
				new ConcurrentDictionary<MemberInfo, Delegate>();

		private static Delegate CreateGetter<T>(FieldInfo field)
		{
			switch (Type.GetTypeCode(typeof(T)))
			{
			case TypeCode.Boolean:
				if (typeof(FormatBoolean).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatBoolean> fn = FastAccessor.CreateGetter<FormatBoolean>(field);
					Func<object, bool> ret = o => fn(o).Value;
					return ret;
				}
				break;
			case TypeCode.Int32:
				if (typeof(FormatInteger).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatInteger> fn = FastAccessor.CreateGetter<FormatInteger>(field);
					Func<object, int> ret = o => fn(o).Value;
					return ret;
				}
				break;
			case TypeCode.Int64:
				if (typeof(FormatLong).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatLong> fn = FastAccessor.CreateGetter<FormatLong>(field);
					Func<object, long> ret = o => fn(o).Value;
					return ret;
				}
				break;
			case TypeCode.Double:
				if (typeof(FormatDouble).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatDouble> fn = FastAccessor.CreateGetter<FormatDouble>(field);
					Func<object, double> ret = o => fn(o).Value;
					return ret;
				}
				break;
			case TypeCode.String:
				if (typeof(FormatString).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatString> fn = FastAccessor.CreateGetter<FormatString>(field);
					Func<object, string> ret = o => fn(o).Value;
					return ret;
				}
				break;
			default:
				if (typeof(FormatBytes).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatBytes> fn = FastAccessor.CreateGetter<FormatBytes>(field);
					Func<object, byte[]> ret = o => fn(o).Value;
					return ret;
				}
				break;
			}
			return FastAccessor.CreateGetter<T>(field);
		}

		private static Delegate CreateGetter<T>(PropertyInfo property)
		{
			switch (Type.GetTypeCode(typeof(T)))
			{
			case TypeCode.Boolean:
				if (typeof(FormatBoolean).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatBoolean> fn = FastAccessor.CreateGetter<FormatBoolean>(property);
					Func<object, bool> ret = o => fn(o).Value;
					return ret;
				}
				break;
			case TypeCode.Int32:
				if (typeof(FormatInteger).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatInteger> fn = FastAccessor.CreateGetter<FormatInteger>(property);
					Func<object, int> ret = o => fn(o).Value;
					return ret;
				}
				break;
			case TypeCode.Int64:
				if (typeof(FormatLong).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatLong> fn = FastAccessor.CreateGetter<FormatLong>(property);
					Func<object, long> ret = o => fn(o).Value;
					return ret;
				}
				break;
			case TypeCode.Double:
				if (typeof(FormatDouble).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatDouble> fn = FastAccessor.CreateGetter<FormatDouble>(property);
					Func<object, double> ret = o => fn(o).Value;
					return ret;
				}
				break;
			case TypeCode.String:
				if (typeof(FormatString).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatString> fn = FastAccessor.CreateGetter<FormatString>(property);
					Func<object, string> ret = o => fn(o).Value;
					return ret;
				}
				break;
			default:
				if (typeof(FormatBytes).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatBytes> fn = FastAccessor.CreateGetter<FormatBytes>(property);
					Func<object, byte[]> ret = o => fn(o).Value;
					return ret;
				}
				break;
			}
			return FastAccessor.CreateGetter<T>(property);
		}

		private static Delegate CreateSetter<T>(FieldInfo field)
		{
			switch (Type.GetTypeCode(typeof(T)))
			{
			case TypeCode.Boolean:
				if (typeof(FormatBoolean).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatBoolean> fn = FastAccessor.CreateGetter<FormatBoolean>(field);
					Action<object, bool> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			case TypeCode.Int32:
				if (typeof(FormatInteger).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatInteger> fn = FastAccessor.CreateGetter<FormatInteger>(field);
					Action<object, int> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			case TypeCode.Int64:
				if (typeof(FormatLong).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatLong> fn = FastAccessor.CreateGetter<FormatLong>(field);
					Action<object, long> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			case TypeCode.Double:
				if (typeof(FormatDouble).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatDouble> fn = FastAccessor.CreateGetter<FormatDouble>(field);
					Action<object, double> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			case TypeCode.String:
				if (typeof(FormatString).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatString> fn = FastAccessor.CreateGetter<FormatString>(field);
					Action<object, string> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			default:
				if (typeof(FormatBytes).IsAssignableFrom(field.FieldType))
				{
					Func<object, FormatBytes> fn = FastAccessor.CreateGetter<FormatBytes>(field);
					Action<object, byte[]> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			}
			return FastAccessor.CreateSetter<T>(field);
		}

		private static Delegate CreateSetter<T>(PropertyInfo property)
		{
			switch (Type.GetTypeCode(typeof(T)))
			{
			case TypeCode.Boolean:
				if (typeof(FormatBoolean).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatBoolean> fn = FastAccessor.CreateGetter<FormatBoolean>(property);
					Action<object, bool> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			case TypeCode.Int32:
				if (typeof(FormatInteger).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatInteger> fn = FastAccessor.CreateGetter<FormatInteger>(property);
					Action<object, int> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			case TypeCode.Int64:
				if (typeof(FormatLong).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatLong> fn = FastAccessor.CreateGetter<FormatLong>(property);
					Action<object, long> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			case TypeCode.Double:
				if (typeof(FormatDouble).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatDouble> fn = FastAccessor.CreateGetter<FormatDouble>(property);
					Action<object, double> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			case TypeCode.String:
				if (typeof(FormatString).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatString> fn = FastAccessor.CreateGetter<FormatString>(property);
					Action<object, string> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			default:
				if (typeof(FormatBytes).IsAssignableFrom(property.PropertyType))
				{
					Func<object, FormatBytes> fn = FastAccessor.CreateGetter<FormatBytes>(property);
					Action<object, byte[]> ret = (o, v) => { fn(o).Value = v; };
					return ret;
				}
				break;
			}
			return FastAccessor.CreateSetter<T>(property);
		}
		#endregion
	}
}