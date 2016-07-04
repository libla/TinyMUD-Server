using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

namespace TinyMUD
{
	public abstract class Config : IEnumerable<Config>
	{
		#region 空实现返回优化
		protected static readonly NullNode Null = new NullNode();
		protected static readonly string[] EmptyKeys = new string[0];
		protected static readonly Config[] EmptyValues = new Config[0];
		#endregion

		#region 解析异常类
		public class Exception : System.Exception
		{
			public int LineNumber { get; private set; }
			public int LinePosition { get; private set; }
			public Exception(int row, int column)
			{
				LineNumber = row;
				LinePosition = column;
			}

			public override string Message
			{
				get { return string.Format("Error in line {0}({1})", LineNumber, LinePosition); }
			}
		}
		#endregion

		protected virtual bool IsAny()
		{
			return true;
		}

		protected virtual IEnumerator<Config> GetGenericEnumerator()
		{
			return ((IList<Config>)EmptyValues).GetEnumerator();
		}

		protected virtual IEnumerator GetObjectEnumerator()
		{
			return EmptyValues.GetEnumerator();
		}

		public virtual string Value
		{
			get { return null; }
		}

		public virtual bool IsString()
		{
			return false;
		}

		public virtual bool IsArray()
		{
			return false;
		}

		public virtual bool IsTable()
		{
			return false;
		}

		public virtual int Count
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public virtual Config this[int index]
		{
			get
			{
				return Null;
			}
		}

		public virtual Config this[string key]
		{
			get
			{
				return Null;
			}
		}

		public virtual IEnumerable<string> Keys
		{
			get { return EmptyKeys; }
		}

		public virtual IEnumerable<Config> Values
		{
			get { return EmptyValues; }
		}

		public IEnumerator<Config> GetEnumerator()
		{
			return GetGenericEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetObjectEnumerator();
		}

		public static bool operator true(Config node)
		{
			return node.IsAny();
		}

		public static bool operator false(Config node)
		{
			return !node.IsAny();
		}

		public static explicit operator string(Config node)
		{
			if (!node.IsString())
				throw new InvalidCastException();
			return node.Value;
		}

		public static Config Load(string input)
		{
			return LoadImpl(Encoding.UTF8.GetBytes(input), 0);
		}

		public static Config Load(byte[] input)
		{
			if (input.Length >= 3 && input[0] == 0xEF && input[1] == 0xBB && input[2] == 0xBF)
				return LoadImpl(input, 3);
			return LoadImpl(input, 0);
		}

		private static Config LoadImpl(byte[] input, int start)
		{
			MemoryStream stream = new MemoryStream(input, start, input.Length - start);
			XmlDocument xml = new XmlDocument();
			try
			{
				xml.Load(stream);
			}
			catch (XmlException e)
			{
				throw new Exception(e.LineNumber, e.LinePosition);
			}
			XmlNode rootnode = xml.LastChild;
			if (rootnode.Name != "Config")
				return null;
			TableNode root = new TableNode();
			for (XmlNode child = rootnode.FirstChild; child != null; child = child.NextSibling)
			{
				AddNode(root, child);
			}
			return root;
		}

		private static void AddNode(TableNode parent, XmlNode node)
		{
			string name = node.Name;
			Config config;
			if (!parent.dict.TryGetValue(name, out config))
			{
				config = new ArrayNode();
				parent.dict.Add(name, config);
			}
			ArrayNode array = config as ArrayNode;
			if (array == null)
				throw new Exception(0, 0);
			TableNode table = new TableNode();
			array.list.Add(table);
			var attrs = node.Attributes;
			for (int i = 0; i < attrs.Count; i++)
			{
				var attr = attrs[i];
				table.dict[attr.Name] = new ValueNode(attr.Value);
			}
			for (XmlNode child = node.FirstChild; child != null; child = child.NextSibling)
			{
				AddNode(table, child);
			}
		}

		#region 具体结点类型实现
		protected class NullNode : Config
		{
			protected override bool IsAny()
			{
				return false;
			}
		}

		private class ValueNode : Config
		{
			private readonly string str;
			public ValueNode(string s)
			{
				str = s ?? "";
			}

			public override bool IsString()
			{
				return true;
			}

			public override string Value
			{
				get { return str; }
			}
		}

		private class ArrayNode : Config
		{
			public readonly List<Config> list;
			public ArrayNode()
			{
				list = new List<Config>();
			}

			protected override IEnumerator<Config> GetGenericEnumerator()
			{
				return list.GetEnumerator();
			}

			protected override IEnumerator GetObjectEnumerator()
			{
				return ((IList)list).GetEnumerator();
			}

			public override string Value
			{
				get { return list.Count == 0 ? base.Value : list[0].Value; }
			}

			public override bool IsArray()
			{
				return true;
			}

			public override int Count
			{
				get { return list.Count; }
			}

			public override Config this[int index]
			{
				get
				{
					try
					{
						return list[index];
					}
					catch (ArgumentOutOfRangeException)
					{
						return base[index];
					}
				}
			}

			public override Config this[string key]
			{
				get { return list.Count == 0 ? base[key] : list[0][key]; }
			}

			public override IEnumerable<Config> Values
			{
				get { return list.ToArray(); }
			}
		}

		private class TableNode : Config
		{
			public readonly Dictionary<string, Config> dict;
			public TableNode()
			{
				dict = new Dictionary<string, Config>();
			}

			protected override IEnumerator<Config> GetGenericEnumerator()
			{
				return dict.Values.GetEnumerator();
			}

			protected override IEnumerator GetObjectEnumerator()
			{
				return ((IEnumerable)dict.Values).GetEnumerator();
			}

			public override bool IsTable()
			{
				return true;
			}

			public override int Count
			{
				get { return dict.Count; }
			}

			public override Config this[string key]
			{
				get
				{
					Config value;
					return dict.TryGetValue(key, out value) ? value : base[key];
				}
			}

			public override IEnumerable<string> Keys
			{
				get { return dict.Keys; }
			}

			public override IEnumerable<Config> Values
			{
				get { return dict.Values; }
			}
		}
		#endregion
	}
}
