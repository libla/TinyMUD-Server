using System;
using System.IO;
using System.Text;
using System.Runtime.Serialization;

namespace TinyMUD
{
	public static partial class Protocol
	{
		public static Format Dummy()
		{
			return new DummyFormat();
		}

		private class DummyFormat : Format
		{
			#region 读取处理实现
			public override void Skip()
			{
			}

			protected override void ReadTable(Action<Format, int> readtype)
			{
			}

			protected override void ReadArray(Action<Format> readarray)
			{
			}

			protected override bool ReadBool()
			{
				return false;
			}

			protected override int ReadInt()
			{
				return 0;
			}

			protected override double ReadFloat()
			{
				return 0;
			}

			protected override string ReadString()
			{
				return "";
			}

			protected override void PrepareRead(Stream stream)
			{
			}

			protected override void FlushRead()
			{
			}
			#endregion

			#region 写入处理实现
			protected override void WriteKey(int key)
			{
			}

			protected override void WriteBool(bool b)
			{
			}

			protected override void WriteInt(int n)
			{
			}

			protected override void WriteFloat(double d)
			{
			}

			protected override void WriteString(string s)
			{
			}

			protected override void PrepareWrite(Stream stream)
			{
			}

			protected override void FlushWrite()
			{
			}
			#endregion
		}
	}
}