using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

		public static int ToInt(string val, int defaultVal)
		{
			if (val == null)
				return defaultVal;
			return int.Parse(val);
		}

		public static double ToDouble(string val, double defaultVal)
		{
			if (val == null)
				return defaultVal;
			return double.Parse(val);
		}

		public static bool ToBool(string val, bool defaultVal)
		{
			switch (val)
			{
			case null:
				return defaultVal;
			case "True":
			case "true":
			case "Yes":
			case "yes":
				return true;
			case "False":
			case "false":
			case "No":
			case "no":
				return false;
			default:
				throw new FormatException();
			}
		}

		public int ToInt(int defaultVal)
		{
			return ToInt(Value, defaultVal);
		}

		public double ToDouble(double defaultVal)
		{
			return ToDouble(Value, defaultVal);
		}

		public bool ToBool(bool defaultVal)
		{
			return ToBool(Value, defaultVal);
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

		public virtual Config Select(params string[] keys)
		{
			return Null;
		}

		public virtual Config Select(int lower, int upper)
		{
			return Null;
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

		public static bool operator !(Config node)
		{
			return !node.IsAny();
		}

		public static explicit operator string(Config node)
		{
			if (!node.IsString())
				throw new InvalidCastException();
			return node.Value;
		}

		public static Config Combine(params Config[] configs)
		{
			ArrayNode array = new ArrayNode();
			List<Config> tables = new List<Config>();
			for (int i = 0; i < configs.Length; ++i)
			{
				if (configs[i].IsTable())
				{
					tables.Add(configs[i]);
				}
				else if (configs[i].IsArray())
				{
					foreach (Config config in configs[i])
						array.list.Add(config);
				}
				else
				{
					array.list.Add(configs[i]);
				}
			}
			TableNode table = null;
			if (tables.Count > 0)
			{
				table = new TableNode();
				for (int i = 0; i < tables.Count; ++i)
				{
					Config config = tables[i];
					foreach (string key in config.Keys)
					{
						AddNode(table, key, config[key]);
					}
				}
			}
			if (table != null)
				array.list.Add(table);
			if (array.list.Count > 1)
				return array;
			return array[0];
		}

		public static Config Load(string input)
		{
			return Load(Encoding.UTF8.GetBytes(input));
		}

		public static Config Load(byte[] input)
		{
			return Load(input, 0, input.Length);
		}

		public static Config Load(byte[] input, int start)
		{
			return Load(input, start, input.Length - start);
		}

		public static Config Load(byte[] input, int start, int count)
		{
			return LoadXML(new MemoryStream(input, start, count, false));
		}

		private static Config LoadXML(Stream stream)
		{
			XmlDocument xml = new XmlDocument();
			try
			{
				xml.Load(stream);
			}
			catch (XmlException e)
			{
				throw new Exception(e.LineNumber, e.LinePosition);
			}
			XmlNode rootnode = xml.FirstChild;
			while (rootnode.NodeType != XmlNodeType.Element)
				rootnode = rootnode.NextSibling;
			string rootname = rootnode.Name;
			if (rootname != "Config" && rootname != "config" &&
				rootname != "Root" && rootname != "root")
				return null;
			TableNode root = new TableNode();
			for (XmlNode child = rootnode.FirstChild; child != null; child = child.NextSibling)
			{
				if (child.NodeType == XmlNodeType.Element)
					AddNode(root, child);
			}
			return root;
		}

		private static void AddNode(TableNode parent, string name, Config node)
		{
			Config config;
			if (parent.dict.TryGetValue(name, out config))
			{
				ArrayNode array = config as ArrayNode;
				if (array == null)
				{
					array = new ArrayNode();
					array.list.Add(config);
					parent.dict[name] = array;
				}
				array.list.Add(node);
			}
			else
			{
				parent.dict[name] = node;
			}
		}

		private static void AddNode(TableNode parent, XmlNode node)
		{
			string name = node.Name;
			if (name == "Value" || name == "value")
			{
				var attrs = node.Attributes;
				for (int i = 0; i < attrs.Count; i++)
				{
					var attr = attrs[i];
					AddNode(parent, attr.Name, new ValueNode(attr.Value));
				}
			}
			else
			{
				TableNode table = new TableNode();
				AddNode(parent, name, table);
				var attrs = node.Attributes;
				for (int i = 0; i < attrs.Count; i++)
				{
					var attr = attrs[i];
					table.dict[attr.Name] = new ValueNode(attr.Value);
				}
				for (XmlNode child = node.FirstChild; child != null; child = child.NextSibling)
				{
					if (child.NodeType == XmlNodeType.Element)
						AddNode(table, child);
				}
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

			public override Config Select(params string[] keys)
			{
				return list.Count == 0 ? base.Select(keys) : list[0].Select(keys);
			}

			public override Config Select(int lower, int upper)
			{
				ArrayNode node = new ArrayNode();
				for (int i = lower, j = Math.Min(upper, list.Count); i < j; ++i)
				{
					node.list.Add(list[i]);
				}
				return node;
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

			public override Config Select(params string[] keys)
			{
				TableNode node = new TableNode();
				for (int i = 0; i < keys.Length; ++i)
				{
					string key = keys[i];
					Config value;
					if (dict.TryGetValue(key, out value))
						node.dict[key] = value;
				}
				return node;
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
