using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TinyMUD
{
	public abstract class Connection : IDisposable
	{
		public async Task VerifyTable(params DataEntity.Define[] defines)
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

		public abstract DataEntity.Writer CreateWriter();
		public abstract DataEntity.Selector CreateSelector();

		protected abstract Task VerifyTable(DataEntity.Define define);
		public abstract void Close();
	}
}
