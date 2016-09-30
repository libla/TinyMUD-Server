using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace TinyMUD
{
	public static class DataEntity
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

		#region OpCode表
		private static readonly Dictionary<TypeCode, int> codes = new Dictionary<TypeCode, int>
		{
			{TypeCode.SByte, 0},
			{TypeCode.Byte, 1},
			{TypeCode.Int16, 2},
			{TypeCode.UInt16, 3},
			{TypeCode.Int32, 4},
			{TypeCode.UInt32, 5},
			{TypeCode.Int64, 6},
			{TypeCode.UInt64, 7},
			{TypeCode.Single, 8},
			{TypeCode.Double, 9},
		};

		private static readonly OpCode[,] converts = {
			/*    SByte            Byte             Int16           UInt16            Int32           UInt32            Int64           UInt64           Single           Double    */
/*SByte*/	{OpCodes.Nop,     OpCodes.Conv_I1, OpCodes.Conv_I1, OpCodes.Conv_I1, OpCodes.Conv_I1, OpCodes.Conv_I1, OpCodes.Conv_I1, OpCodes.Conv_I1, OpCodes.Conv_I1, OpCodes.Conv_I1},
/*Byte*/	{OpCodes.Conv_U1, OpCodes.Conv_U1, OpCodes.Conv_U1, OpCodes.Conv_U1, OpCodes.Conv_U1, OpCodes.Conv_U1, OpCodes.Conv_U1, OpCodes.Conv_U1, OpCodes.Conv_U1, OpCodes.Conv_U1},
/*Int16*/	{OpCodes.Nop,     OpCodes.Nop,     OpCodes.Nop,     OpCodes.Conv_I2, OpCodes.Conv_I2, OpCodes.Conv_I2, OpCodes.Conv_I2, OpCodes.Conv_I2, OpCodes.Conv_I2, OpCodes.Conv_I2},
/*UInt16*/	{OpCodes.Nop,     OpCodes.Nop,     OpCodes.Conv_U2, OpCodes.Nop,     OpCodes.Conv_U2, OpCodes.Conv_U2, OpCodes.Conv_U2, OpCodes.Conv_U2, OpCodes.Conv_U2, OpCodes.Conv_U2},
/*Int32*/	{OpCodes.Nop,     OpCodes.Nop,     OpCodes.Nop,     OpCodes.Nop,     OpCodes.Nop,     OpCodes.Conv_I4, OpCodes.Conv_I4, OpCodes.Conv_I4, OpCodes.Conv_I4, OpCodes.Conv_I4},
/*UInt32*/	{OpCodes.Nop,     OpCodes.Nop,     OpCodes.Nop,     OpCodes.Nop,     OpCodes.Conv_U4, OpCodes.Nop,     OpCodes.Conv_U4, OpCodes.Conv_U4, OpCodes.Conv_U4, OpCodes.Conv_U4},
/*Int64*/	{OpCodes.Conv_I8, OpCodes.Conv_I8, OpCodes.Conv_I8, OpCodes.Conv_I8, OpCodes.Conv_I8, OpCodes.Conv_I8, OpCodes.Nop,     OpCodes.Conv_I8, OpCodes.Conv_I8, OpCodes.Conv_I8},
/*UInt64*/	{OpCodes.Conv_U8, OpCodes.Conv_U8, OpCodes.Conv_U8, OpCodes.Conv_U8, OpCodes.Conv_U8, OpCodes.Conv_U8, OpCodes.Conv_U8, OpCodes.Nop,     OpCodes.Conv_U8, OpCodes.Conv_U8},
/*Single*/	{OpCodes.Conv_R4, OpCodes.Conv_R4, OpCodes.Conv_R4, OpCodes.Conv_R4, OpCodes.Conv_R4, OpCodes.Conv_R4, OpCodes.Conv_R4, OpCodes.Conv_R4, OpCodes.Nop,     OpCodes.Conv_R4},
/*Double*/	{OpCodes.Conv_R8, OpCodes.Conv_R8, OpCodes.Conv_R8, OpCodes.Conv_R8, OpCodes.Conv_R8, OpCodes.Conv_R8, OpCodes.Conv_R8, OpCodes.Conv_R8, OpCodes.Conv_R8, OpCodes.Nop},
		};
		#endregion

		private static OpCode ConvertFrom(this TypeCode to, TypeCode from)
		{
			int index1;
			if (!codes.TryGetValue(to, out index1))
				return OpCodes.Throw;
			int index2;
			if (!codes.TryGetValue(from, out index2))
				return OpCodes.Throw;
			return converts[index1, index2];
		}

		private static Action<object, T> EmitSet<T>(FieldInfo field)
		{
			DynamicMethod dm = new DynamicMethod(string.Format("Set_{0}.{1}", field.DeclaringType.FullName, field.Name), typeof(void),
												new Type[] { typeof(object), typeof(T) }, field.DeclaringType, true);
			ILGenerator il = dm.GetILGenerator();
			if (!field.DeclaringType.IsValueType)
			{
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldarg_1);
				if (field.FieldType != typeof(T))
				{
					if (!field.FieldType.IsAssignableFrom(typeof(T)))
					{
						TypeCode code = Type.GetTypeCode(field.FieldType);
						if (code == TypeCode.Boolean)
						{
							switch (Type.GetTypeCode(field.FieldType))
							{
							case TypeCode.UInt64:
							case TypeCode.Int64:
								il.Emit(OpCodes.Ldc_I4_0);
								il.Emit(OpCodes.Conv_I8);
								break;
							case TypeCode.SByte:
							case TypeCode.Byte:
							case TypeCode.Int16:
							case TypeCode.UInt16:
							case TypeCode.Int32:
							case TypeCode.UInt32:
								il.Emit(OpCodes.Ldc_I4_0);
								break;
							default:
								throw new DataSet.TypeMismatchException();
							}
							il.Emit(OpCodes.Ceq);
							il.Emit(OpCodes.Ldc_I4_0);
							il.Emit(OpCodes.Ceq);
						}
						else
						{
							OpCode op = code.ConvertFrom(Type.GetTypeCode(typeof(T)));
							if (op == OpCodes.Throw)
								throw new DataSet.TypeMismatchException();
							if (op != OpCodes.Nop)
								il.Emit(op);
						}
					}
				}
				il.Emit(OpCodes.Stfld, field);
			}
			il.Emit(OpCodes.Ret);
			return (Action<object, T>)dm.CreateDelegate(typeof(Action<object, T>));
		}

		private static Func<object, T> EmitGet<T>(FieldInfo field)
		{
			DynamicMethod dm = new DynamicMethod(string.Format("Get_{0}.{1}", field.DeclaringType.FullName, field.Name), typeof(T),
												new Type[] { typeof(object) }, field.DeclaringType, true);
			ILGenerator il = dm.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			if (field.DeclaringType.IsValueType)
				il.Emit(OpCodes.Unbox_Any, field.DeclaringType);
			il.Emit(OpCodes.Ldfld, field);
			if (field.FieldType != typeof(T))
			{
				if (!typeof(T).IsAssignableFrom(field.FieldType))
				{
					TypeCode code = Type.GetTypeCode(typeof(T));
					if (code == TypeCode.Boolean)
					{
						switch (Type.GetTypeCode(field.FieldType))
						{
						case TypeCode.UInt64:
						case TypeCode.Int64:
							il.Emit(OpCodes.Ldc_I4_0);
							il.Emit(OpCodes.Conv_I8);
							break;
						case TypeCode.SByte:
						case TypeCode.Byte:
						case TypeCode.Int16:
						case TypeCode.UInt16:
						case TypeCode.Int32:
						case TypeCode.UInt32:
							il.Emit(OpCodes.Ldc_I4_0);
							break;
						default:
							throw new DataSet.TypeMismatchException();
						}
						il.Emit(OpCodes.Ceq);
						il.Emit(OpCodes.Ldc_I4_0);
						il.Emit(OpCodes.Ceq);
					}
					else
					{
						OpCode op = code.ConvertFrom(Type.GetTypeCode(field.FieldType));
						if (op == OpCodes.Throw)
							throw new DataSet.TypeMismatchException();
						if (op != OpCodes.Nop)
							il.Emit(op);
					}
				}
			}
			il.Emit(OpCodes.Ret);
			return (Func<object, T>)dm.CreateDelegate(typeof(Func<object, T>));
		}
	}
}