using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace TinyMUD.Extension
{
	public class SQLiteDB : Connection
	{
		private struct Dummy { }
		private readonly SQLiteConnection connect;

		private static readonly Dictionary<DataSet.Type, string> datatypenames = new Dictionary<DataSet.Type, string>
		{
			{DataSet.Type.Bool, "INTEGER"},
			{DataSet.Type.Integer, "INTEGER"},
			{DataSet.Type.Long, "INTEGER"},
			{DataSet.Type.Double, "REAL"},
			{DataSet.Type.String, "TEXT"},
			{DataSet.Type.Blob, "BLOB"},
		};

		public SQLiteDB(string filename) : this(filename, false) { }

		public SQLiteDB(string filename, bool isreadonly)
		{
			connect = new SQLiteConnection(string.Format("Data Source = {0};Read Only = {1};DateTimeKind = Utc", filename, isreadonly ? "True" : "False"));
			connect.Open();
		}

		public override DataSet.Writer CreateWriter()
		{
			return new Writer(connect);
		}

		public override DataSet.Selector CreateSelector()
		{
			return new Selector(connect);
		}

		protected override async Task VerifyTable(DataSet.Define define)
		{
			SQLiteCommand select = new SQLiteCommand(
				string.Format("SELECT count(name) as result FROM 'sqlite_master' WHERE type = 'table' AND name = '{0}';",
							define.Name), connect);
			using (select)
			{
				var reader = await select.ExecuteReaderAsync();
				if (await reader.ReadAsync())
				{
					if (reader.GetInt32(0) != 0)
						return;
				}
			}
			SQLiteCommand create = new SQLiteCommand(define.CreateTable(datatypenames), connect);
			using (create)
			{
				await create.ExecuteNonQueryAsync();
			}
		}

		public override void Close()
		{
			connect.Close();
		}

		private class Writer : DataSet.Writer, SQL.Command
		{
			private readonly SQLiteConnection connect;
			private readonly List<SQLiteParameter> parameters;
			private readonly List<SQLiteCommand> commands;

			public Writer(SQLiteConnection connect)
			{
				this.connect = connect;
				this.parameters = new List<SQLiteParameter>();
				this.commands = new List<SQLiteCommand>();
			}

			public override void Write(DataSet.Define define, DataSet.Key key, DataSet.Column column)
			{
				Remove(define, key);
				string sql = string.Format("INSERT INTO '{0}' ({1}, {2}) VALUES ({3}, {4});", define.Name, define.KeysName(),
									define.ColumnsName(), define.KeysValue(), define.ColumnsValue());
				sql = this.BindKeyColumn(define, key, column, sql);
				SQLiteCommand command = Pool<SQLiteCommand>.Default.Acquire();
				command.Connection = connect;
				command.CommandText = sql;
				for (int i = 0; i < parameters.Count; ++i)
				{
					command.Parameters.Add(parameters[i]);
				}
				parameters.Clear();
				commands.Add(command);
			}

			public override void Remove(DataSet.Define define, DataSet.Key key)
			{
				string sql = string.Format("DELETE FROM '{0}'{1};", define.Name, define.Where());
				sql = this.BindKey(define, key, sql);
				SQLiteCommand command = Pool<SQLiteCommand>.Default.Acquire();
				command.Connection = connect;
				command.CommandText = sql;
				for (int i = 0; i < parameters.Count; ++i)
				{
					command.Parameters.Add(parameters[i]);
				}
				parameters.Clear();
				commands.Add(command);
			}

			public override async Task Submit()
			{
				if (commands.Count == 1)
				{
					await commands[0].ExecuteNonQueryAsync();
				}
				else
				{
					using (SQLiteTransaction trans = connect.BeginTransaction())
					{
						for (int i = 0; i < commands.Count; ++i)
						{
							await commands[i].ExecuteNonQueryAsync();
						}
						trans.Commit();
					}
				}
			}

			public override void Clear()
			{
				foreach (SQLiteParameter parameter in parameters)
				{
					parameter.Value = null;
					Pool<SQLiteParameter>.Default.Release(parameter);
				}
				parameters.Clear();
				foreach (SQLiteCommand command in commands)
				{
					foreach (SQLiteParameter parameter in command.Parameters)
					{
						parameter.Value = null;
						Pool<SQLiteParameter>.Default.Release(parameter);
					}
					command.Parameters.Clear();
					command.Connection = null;
					Pool<SQLiteCommand>.Default.Release(command);
				}
				commands.Clear();
			}

			public override void Close()
			{
				Clear();
			}

			public void AddParam(string str)
			{
				SQLiteParameter parameter = Pool<SQLiteParameter>.Default.Acquire();
				parameter.DbType = DbType.String;
				parameter.Value = str;
				parameters.Add(parameter);
			}

			public void AddParam(byte[] bytes)
			{
				SQLiteParameter parameter = Pool<SQLiteParameter>.Default.Acquire();
				parameter.DbType = DbType.Binary;
				parameter.Value = bytes;
				parameters.Add(parameter);
			}
		}

		private class Selector : DataSet.Selector, SQL.Command
		{
			private readonly SQLiteConnection connect;
			private readonly List<SQLiteParameter> parameters;
			private readonly List<KeyValuePair<SQLiteCommand, DataSet.Define>> commands;

			public Selector(SQLiteConnection connect)
			{
				this.connect = connect;
				this.parameters = new List<SQLiteParameter>();
				this.commands = new List<KeyValuePair<SQLiteCommand, DataSet.Define>>();
			}

			public override async Task<bool> Select(DataSet.Define define, DataSet.Key key, DataSet.Column column)
			{
				string sql = string.Format("SELECT {0} FROM '{1}'{2};", define.ColumnsName(), define.Name, define.Where());
				sql = this.BindKey(define, key, sql);
				SQLiteCommand command = Pool<SQLiteCommand>.Default.Acquire();
				command.Connection = connect;
				command.CommandText = sql;
				for (int i = 0; i < parameters.Count; ++i)
				{
					command.Parameters.Add(parameters[i]);
				}
				parameters.Clear();
				using (var reader = await command.ExecuteReaderAsync())
				{
					if (!await reader.ReadAsync())
					{
						Pool<SQLiteCommand>.Default.Release(command);
						return false;
					}
					reader.ReadColumn(define, column);
				}
				Pool<SQLiteCommand>.Default.Release(command);
				return true;
			}

			public override void Select(DataSet.Define define, DataSet.Key key)
			{
				string sql = string.Format("SELECT {0}, {1} FROM '{2}'{3};", define.KeysName(), define.ColumnsName(), define.Name, define.Where());
				sql = this.BindKey(define, key, sql);
				SQLiteCommand command = Pool<SQLiteCommand>.Default.Acquire();
				command.Connection = connect;
				command.CommandText = sql;
				for (int i = 0; i < parameters.Count; ++i)
				{
					command.Parameters.Add(parameters[i]);
				}
				parameters.Clear();
				commands.Add(new KeyValuePair<SQLiteCommand, DataSet.Define>(command, define));
			}

			public override Task<DataSet.Reader> Execute()
			{
				TaskCompletionSource<DataSet.Reader> source = new TaskCompletionSource<DataSet.Reader>();
				try
				{
					if (commands.Count == 1)
						source.TrySetResult(new SimpleReader(commands[0].Value, commands[0].Key));
					else if (commands.Count == 0)
						source.TrySetResult(new DummyRead());
					else
						source.TrySetResult(new MultiReader(commands));
				}
				catch (Exception e)
				{
					source.SetException(e);
				}
				return source.Task;
			}

			public override Task<DataSet.Reader> Scan(DataSet.Define define)
			{
				TaskCompletionSource<DataSet.Reader> source = new TaskCompletionSource<DataSet.Reader>();
				try
				{
					string sql = string.Format("SELECT {0}, {1} FROM '{2}';", define.KeysName(), define.ColumnsName(), define.Name);
					SQLiteCommand command = Pool<SQLiteCommand>.Default.Acquire();
					command.Connection = connect;
					command.CommandText = sql;
					for (int i = 0; i < parameters.Count; ++i)
					{
						command.Parameters.Add(parameters[i]);
					}
					parameters.Clear();
					commands.Add(new KeyValuePair<SQLiteCommand, DataSet.Define>(command, define));
					source.TrySetResult(new SimpleReader(define, command));
				}
				catch (Exception e)
				{
					source.SetException(e);
				}
				return source.Task;
			}

			public override void Clear()
			{
				foreach (SQLiteParameter parameter in parameters)
				{
					parameter.Value = null;
					Pool<SQLiteParameter>.Default.Release(parameter);
				}
				parameters.Clear();
				foreach (KeyValuePair<SQLiteCommand, DataSet.Define> command in commands)
				{
					foreach (SQLiteParameter parameter in command.Key.Parameters)
					{
						parameter.Value = null;
						Pool<SQLiteParameter>.Default.Release(parameter);
					}
					command.Key.Parameters.Clear();
					command.Key.Connection = null;
					Pool<SQLiteCommand>.Default.Release(command.Key);
				}
				commands.Clear();
			}

			public override void Close()
			{
				Clear();
			}

			public void AddParam(string str)
			{
				SQLiteParameter parameter = Pool<SQLiteParameter>.Default.Acquire();
				parameter.DbType = DbType.String;
				parameter.Value = str;
				parameters.Add(parameter);
			}

			public void AddParam(byte[] bytes)
			{
				SQLiteParameter parameter = Pool<SQLiteParameter>.Default.Acquire();
				parameter.DbType = DbType.Binary;
				parameter.Value = bytes;
				parameters.Add(parameter);
			}
		}

		private class DummyRead : DataSet.Reader
		{
			public override Task<bool> Read(DataSet.Key key, DataSet.Column column)
			{
				TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();
				source.SetResult(false);
				return source.Task;
			}

			public override void Close() {}
		}

		private class SimpleReader : DataSet.Reader
		{
			private readonly DataSet.Define define;
			private readonly SQLiteCommand command;
			private SQLiteDataReader reader;

			public SimpleReader(DataSet.Define define, SQLiteCommand command)
			{
				this.define = define;
				this.command = command;
			}

			public override async Task<bool> Read(DataSet.Key key, DataSet.Column column)
			{
				if (reader == null)
					reader = await command.ExecuteReaderAsync() as SQLiteDataReader;
				if (!await reader.ReadAsync())
					return false;
				reader.ReadColumn(define, column, reader.ReadKey(define, key, 0));
				return true;
			}

			public override void Close()
			{
				if (reader != null)
					reader.Close();
			}
		}

		private class MultiReader : DataSet.Reader
		{
			private readonly List<KeyValuePair<SQLiteCommand, DataSet.Define>> commands;
			private int index;
			private SQLiteDataReader reader;

			public MultiReader(List<KeyValuePair<SQLiteCommand, DataSet.Define>> commands)
			{
				this.commands = commands;
				this.index = 0;
			}

			public override async Task<bool> Read(DataSet.Key key, DataSet.Column column)
			{
				if (reader == null)
					reader = await commands[index].Key.ExecuteReaderAsync() as SQLiteDataReader;
				while (true)
				{
					if (await reader.ReadAsync())
						break;
					++index;
					if (index >= commands.Count)
						return false;
					reader.Close();
					reader = await commands[index].Key.ExecuteReaderAsync() as SQLiteDataReader;
				}
				DataSet.Define define = commands[index].Value;
				reader.ReadColumn(define, column, reader.ReadKey(define, key, 0));
				return true;
			}

			public override void Close()
			{
				if (reader != null)
					reader.Close();
			}
		}
	}
}