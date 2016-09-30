using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TinyMUD
{
	public abstract class Connection : IDisposable
	{
		public async Task VerifyTable(params DataSet.Define[] defines)
		{
			for (int i = 0; i < defines.Length; ++i)
			{
				await VerifyTable(defines[i]);
			}
		}

		public void Dispose()
		{
			Close();
		}

		public abstract DataSet.Writer CreateWriter();
		public abstract DataSet.Selector CreateSelector();

		protected abstract Task VerifyTable(DataSet.Define define);
		public abstract void Close();
	}
}
