using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace TinyMUD.Extension
{
	internal static class SQL
	{
		public interface Command
		{
			void AddParam(string str);
			void AddParam(byte[] bytes);
		}

		private static readonly Regex KeyPattern = new Regex("@\\d+",
													RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);
		private static readonly Regex ColumnPattern = new Regex("\\<[^\\>]+\\>",
													RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);
		private static readonly Regex KeyColumnPattern = new Regex("(@\\d+)|(\\<[^\\>]+\\>)",
													RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);

		private class KeyReplacement
		{
			public DataSet.Define define;
			public DataSet.Key key;
			public readonly MatchEvaluator Execute;

			public KeyReplacement()
			{
				Execute = match =>
				{
					string value = match.Value;
					int index = int.Parse(value.Substring(1));
					switch (define.Keys[index])
					{
					case DataSet.Type.Bool:
						{
							bool b = false;
							if (!key.Read(index, ref b))
								throw new DataSet.TypeMismatchException();
							return b ? "1" : "0";
						}
					case DataSet.Type.Integer:
						{
							int i = 0;
							if (!key.Read(index, ref i))
								throw new DataSet.TypeMismatchException();
							return i.ToString();
						}
					case DataSet.Type.Long:
						{
							long l = 0;
							if (!key.Read(index, ref l))
								throw new DataSet.TypeMismatchException();
							return l.ToString();
						}
					case DataSet.Type.Double:
						{
							double d = 0;
							if (!key.Read(index, ref d))
								throw new DataSet.TypeMismatchException();
							return d.ToString(CultureInfo.InvariantCulture);
						}
					case DataSet.Type.String:
						{
							string str = null;
							if (!key.Read(index, ref str))
								throw new DataSet.TypeMismatchException();
							str = str ?? "";
							uint hash = xxHash.Compute(Encoding.UTF8.GetBytes(str));
							return hash.ToString();
						}
					case DataSet.Type.Blob:
						{
							byte[] bytes = null;
							if (!key.Read(index, ref bytes))
								throw new DataSet.TypeMismatchException();
							uint hash = xxHash.Compute(bytes);
							return hash.ToString();
						}
					}
					throw new DataSet.TypeMismatchException();
				};
			}
		}

		private class ColumnReplacement
		{
			public DataSet.Define define;
			public DataSet.Column column;
			public readonly MatchEvaluator Execute;

			public ColumnReplacement()
			{
				Execute = match =>
				{
					string value = match.Value;
					string name = value.Substring(1, value.Length - 2);
					switch (define[name])
					{
					case DataSet.Type.Bool:
						{
							bool b = false;
							if (!column.Read(name, ref b))
								throw new DataSet.TypeMismatchException();
							return b ? "1" : "0";
						}
					case DataSet.Type.Integer:
						{
							int i = 0;
							if (!column.Read(name, ref i))
								throw new DataSet.TypeMismatchException();
							return i.ToString();
						}
					case DataSet.Type.Long:
						{
							long l = 0;
							if (!column.Read(name, ref l))
								throw new DataSet.TypeMismatchException();
							return l.ToString();
						}
					case DataSet.Type.Double:
						{
							double d = 0;
							if (!column.Read(name, ref d))
								throw new DataSet.TypeMismatchException();
							return d.ToString(CultureInfo.InvariantCulture);
						}
					case DataSet.Type.String:
						{
							return "error";
						}
					case DataSet.Type.Blob:
						{
							return "error";
						}
					}
					throw new DataSet.TypeMismatchException();
				};
			}
		}

		private class KeyColumnReplacement
		{
			public DataSet.Define define;
			public DataSet.Key key;
			public DataSet.Column column;
			public readonly MatchEvaluator Execute;

			public KeyColumnReplacement()
			{
				Execute = match =>
				{
					string value = match.Value;
					if (value[0] == '@')
					{
						int index = int.Parse(value.Substring(1));
						switch (define.Keys[index])
						{
						case DataSet.Type.Bool:
							{
								bool b = false;
								if (!key.Read(index, ref b))
									throw new DataSet.TypeMismatchException();
								return b ? "1" : "0";
							}
						case DataSet.Type.Integer:
							{
								int i = 0;
								if (!key.Read(index, ref i))
									throw new DataSet.TypeMismatchException();
								return i.ToString();
							}
						case DataSet.Type.Long:
							{
								long l = 0;
								if (!key.Read(index, ref l))
									throw new DataSet.TypeMismatchException();
								return l.ToString();
							}
						case DataSet.Type.Double:
							{
								double d = 0;
								if (!key.Read(index, ref d))
									throw new DataSet.TypeMismatchException();
								return d.ToString(CultureInfo.InvariantCulture);
							}
						case DataSet.Type.String:
							{
								string str = null;
								if (!key.Read(index, ref str))
									throw new DataSet.TypeMismatchException();
								str = str ?? "";
								uint hash = xxHash.Compute(Encoding.UTF8.GetBytes(str));
								return hash.ToString();
							}
						case DataSet.Type.Blob:
							{
								byte[] bytes = null;
								if (!key.Read(index, ref bytes))
									throw new DataSet.TypeMismatchException();
								uint hash = xxHash.Compute(bytes);
								return hash.ToString();
							}
						}
					}
					else
					{
						string name = value.Substring(1, value.Length - 2);
						switch (define[name])
						{
						case DataSet.Type.Bool:
							{
								bool b = false;
								if (!column.Read(name, ref b))
									throw new DataSet.TypeMismatchException();
								return b ? "1" : "0";
							}
						case DataSet.Type.Integer:
							{
								int i = 0;
								if (!column.Read(name, ref i))
									throw new DataSet.TypeMismatchException();
								return i.ToString();
							}
						case DataSet.Type.Long:
							{
								long l = 0;
								if (!column.Read(name, ref l))
									throw new DataSet.TypeMismatchException();
								return l.ToString();
							}
						case DataSet.Type.Double:
							{
								double d = 0;
								if (!column.Read(name, ref d))
									throw new DataSet.TypeMismatchException();
								return d.ToString(CultureInfo.InvariantCulture);
							}
						case DataSet.Type.String:
							{
								return "error";
							}
						case DataSet.Type.Blob:
							{
								return "error";
							}
						}
					}
					throw new DataSet.TypeMismatchException();
				};
			}
		}

		public static string BindKey(this Command command, DataSet.Define define, DataSet.Key key, string sql)
		{
			KeyReplacement replacement = Pool<KeyReplacement>.Default.Acquire();
			replacement.define = define;
			replacement.key = key;
			string result = KeyPattern.Replace(sql, replacement.Execute);
			replacement.define = null;
			replacement.key = null;
			Pool<KeyReplacement>.Default.Release(replacement);
			var keys = define.Keys;
			for (int i = 0; i < keys.Count; ++i)
			{
				var type = keys[i];
				if (type == DataSet.Type.String)
				{
					string str = null;
					if (!key.Read(i, ref str))
						throw new DataSet.TypeMismatchException();
					str = str ?? "";
					command.AddParam(str);
				}
				else if (type == DataSet.Type.Blob)
				{
					byte[] bytes = null;
					if (!key.Read(i, ref bytes))
						throw new DataSet.TypeMismatchException();
					command.AddParam(bytes);
				}
			}
			return result;
		}

		public static string BindColumn(this Command command, DataSet.Define define, DataSet.Column column, string sql)
		{
			ColumnReplacement replacement = Pool<ColumnReplacement>.Default.Acquire();
			replacement.define = define;
			replacement.column = column;
			string result = ColumnPattern.Replace(sql, replacement.Execute);
			replacement.define = null;
			replacement.column = null;
			Pool<ColumnReplacement>.Default.Release(replacement);
			var columns = define.Columns;
			for (int i = 0; i < columns.Count; ++i)
			{
				var c = columns[i];
				if (c.Value == DataSet.Type.String)
				{
					string str = null;
					if (!column.Read(c.Key, ref str))
						throw new DataSet.TypeMismatchException();
					str = str ?? "";
					command.AddParam(str);
				}
				else if (c.Value == DataSet.Type.Blob)
				{
					byte[] bytes = null;
					if (!column.Read(c.Key, ref bytes))
						throw new DataSet.TypeMismatchException();
					command.AddParam(bytes);
				}
			}
			return result;
		}

		public static string BindKeyColumn(this Command command, DataSet.Define define, DataSet.Key key, DataSet.Column column, string sql)
		{
			KeyColumnReplacement replacement = Pool<KeyColumnReplacement>.Default.Acquire();
			replacement.define = define;
			replacement.key = key;
			replacement.column = column;
			string result = KeyColumnPattern.Replace(sql, replacement.Execute);
			replacement.define = null;
			replacement.key = null;
			replacement.column = null;
			Pool<KeyColumnReplacement>.Default.Release(replacement);
			var keys = define.Keys;
			for (int i = 0; i < keys.Count; ++i)
			{
				var type = keys[i];
				if (type == DataSet.Type.String)
				{
					string str = null;
					if (!key.Read(i, ref str))
						throw new DataSet.TypeMismatchException();
					str = str ?? "";
					command.AddParam(str);
				}
				else if (type == DataSet.Type.Blob)
				{
					byte[] bytes = null;
					if (!key.Read(i, ref bytes))
						throw new DataSet.TypeMismatchException();
					command.AddParam(bytes);
				}
			}
			var columns = define.Columns;
			for (int i = 0; i < columns.Count; ++i)
			{
				var c = columns[i];
				if (c.Value == DataSet.Type.String)
				{
					string str = null;
					if (!column.Read(c.Key, ref str))
						throw new DataSet.TypeMismatchException();
					str = str ?? "";
					command.AddParam(str);
				}
				else if (c.Value == DataSet.Type.Blob)
				{
					byte[] bytes = null;
					if (!column.Read(c.Key, ref bytes))
						throw new DataSet.TypeMismatchException();
					command.AddParam(bytes);
				}
			}
			return result;
		}

		public static int ReadKey(this DbDataReader reader, DataSet.Define define, DataSet.Key key, int start = 0)
		{
			int index = start;
			for (int i = 0; i < define.Keys.Count; ++i)
			{
				switch (define.Keys[i])
				{
				case DataSet.Type.Bool:
					{
						int result = reader.GetInt32(index);
						if (!key.Write(i, result != 0))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.Integer:
					{
						int result = reader.GetInt32(index);
						if (!key.Write(i, result))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.Long:
					{
						long result = reader.GetInt64(index);
						if (!key.Write(i, result))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.Double:
					{
						double result = reader.GetDouble(index);
						if (!key.Write(i, result))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.String:
					{
						++index;
						string result = reader.GetString(index);
						if (!key.Write(i, result ?? ""))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.Blob:
					{
						++index;
						using (Stream stream = reader.GetStream(index))
						{
							int length = (int)stream.Length;
							byte[] result = new byte[length];
							stream.Read(result, 0, length);
							if (!key.Write(i, result))
								throw new DataSet.TypeMismatchException();
						}
					}
					break;
				}
				++index;
			}
			return index;
		}

		public static int ReadColumn(this DbDataReader reader, DataSet.Define define, DataSet.Column column, int start = 0)
		{
			int index = start;
			foreach (var kv in define.Columns)
			{
				switch (kv.Value)
				{
				case DataSet.Type.Bool:
					{
						int result = reader.GetInt32(index);
						if (!column.Write(kv.Key, result != 0))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.Integer:
					{
						int result = reader.GetInt32(index);
						if (!column.Write(kv.Key, result))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.Long:
					{
						long result = reader.GetInt64(index);
						if (!column.Write(kv.Key, result))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.Double:
					{
						double result = reader.GetDouble(index);
						if (!column.Write(kv.Key, result))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.String:
					{
						string result = reader.GetString(index);
						if (!column.Write(kv.Key, result ?? ""))
							throw new DataSet.TypeMismatchException();
					}
					break;
				case DataSet.Type.Blob:
					{
						using (Stream stream = reader.GetStream(index))
						{
							int length = (int)stream.Length;
							byte[] result = new byte[length];
							stream.Read(result, 0, length);
							if (!column.Write(kv.Key, result))
								throw new DataSet.TypeMismatchException();
						}
					}
					break;
				}
				++index;
			}
			return index;
		}

		#region 缓存表
		private static readonly ConditionalWeakTable<DataSet.Define, string> WhereCache = new ConditionalWeakTable<DataSet.Define, string>();
		private static readonly ConditionalWeakTable<DataSet.Define, string> KeysNameCache = new ConditionalWeakTable<DataSet.Define, string>();
		private static readonly ConditionalWeakTable<DataSet.Define, string> ColumnsNameCache = new ConditionalWeakTable<DataSet.Define, string>();
		private static readonly ConditionalWeakTable<DataSet.Define, string> KeysValueCache = new ConditionalWeakTable<DataSet.Define, string>();
		private static readonly ConditionalWeakTable<DataSet.Define, string> ColumnssValueCache = new ConditionalWeakTable<DataSet.Define, string>();
		#endregion

		public static string Where(this DataSet.Define define)
		{
			string result;
			if (WhereCache.TryGetValue(define, out result))
				return result;
			StringBuilder builder = new StringBuilder();
			builder.Append(" WHERE 1 = 1");
			for (int i = 0; i < define.Keys.Count; ++i)
			{
				builder.AppendFormat(" AND Key{0} = @{0}", i);
				DataSet.Type type = define.Keys[i];
				if (type == DataSet.Type.String || type == DataSet.Type.Blob)
				{
					builder.AppendFormat(" AND RealKey{0} = ?", i);
				}
			}
			result = builder.ToString();
			try
			{
				WhereCache.Add(define, result);
			}
			catch (Exception)
			{
			}
			return result;
		}

		public static string KeysName(this DataSet.Define define)
		{
			string result;
			if (KeysNameCache.TryGetValue(define, out result))
				return result;
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < define.Keys.Count; ++i)
			{
				builder.AppendFormat(i == 0 ? "Key{0}" : ", Key{0}", i);
				DataSet.Type type = define.Keys[i];
				if (type == DataSet.Type.String || type == DataSet.Type.Blob)
				{
					builder.AppendFormat(", RealKey{0}", i);
				}
			}
			result = builder.ToString();
			try
			{
				KeysNameCache.Add(define, result);
			}
			catch (Exception)
			{
			}
			return result;
		}

		public static string ColumnsName(this DataSet.Define define)
		{
			string result;
			if (ColumnsNameCache.TryGetValue(define, out result))
				return result;
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < define.Columns.Count; ++i)
			{
				builder.AppendFormat(i == 0 ? "_{0}_" : ", _{0}_", define.Columns[i].Key);
			}
			result = builder.ToString();
			try
			{
				ColumnsNameCache.Add(define, result);
			}
			catch (Exception)
			{
			}
			return result;
		}

		public static string KeysValue(this DataSet.Define define)
		{
			string result;
			if (KeysValueCache.TryGetValue(define, out result))
				return result;
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < define.Keys.Count; ++i)
			{
				builder.AppendFormat(i == 0 ? "@{0}" : ", @{0}", i);
				DataSet.Type type = define.Keys[i];
				if (type == DataSet.Type.String || type == DataSet.Type.Blob)
					builder.Append(", ?");
			}
			result = builder.ToString();
			try
			{
				KeysValueCache.Add(define, result);
			}
			catch (Exception)
			{
			}
			return result;
		}

		public static string ColumnsValue(this DataSet.Define define)
		{
			string result;
			if (ColumnssValueCache.TryGetValue(define, out result))
				return result;
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < define.Columns.Count; ++i)
			{
				var column = define.Columns[i];
				if (column.Value == DataSet.Type.String || column.Value == DataSet.Type.Blob)
					builder.Append(i == 0 ? "?" : ", ?");
				else
					builder.AppendFormat(i == 0 ? "<{0}>" : ", <{0}>", column.Key);
			}
			result = builder.ToString();
			try
			{
				ColumnssValueCache.Add(define, result);
			}
			catch (Exception)
			{
			}
			return result;
		}

		public static string CreateTable(this DataSet.Define define, IDictionary<DataSet.Type, string> datatypenames)
		{
			StringBuilder builder = new StringBuilder();
			builder.AppendFormat("CREATE TABLE '{0}' (", define.Name);
			for (int i = 0; i < define.Keys.Count; ++i)
			{
				DataSet.Type type = define.Keys[i];
				switch (type)
				{
				case DataSet.Type.String:
				case DataSet.Type.Blob:
					builder.AppendFormat(i == 0 ? "Key{0} {1} NOT NULL" : ", Key{0} {1} NOT NULL", i, datatypenames[DataSet.Type.Integer]);
					builder.AppendFormat(", RealKey{0} {1} NOT NULL", i, datatypenames[type]);
					break;
				case DataSet.Type.Bool:
					builder.AppendFormat(i == 0 ? "Key{0} {1} NOT NULL" : ", Key{0} {1} NOT NULL", i, datatypenames[DataSet.Type.Integer]);
					break;
				default:
					builder.AppendFormat(i == 0 ? "Key{0} {1} NOT NULL" : ", Key{0} {1} NOT NULL", i, datatypenames[type]);
					break;
				}
			}
			foreach (var column in define.Columns)
			{
				builder.AppendFormat(", _{0}_ {1}", column.Key, datatypenames[column.Value]);
			}
			builder.AppendFormat(");CREATE INDEX '{0}#Index' ON '{0}' (", define.Name);
			for (int i = 0; i < define.Keys.Count; ++i)
			{
				builder.AppendFormat(i == 0 ? "Key{0}" : ", Key{0}", i);
			}
			builder.Append(");");
			return builder.ToString();
		}
	}
}
