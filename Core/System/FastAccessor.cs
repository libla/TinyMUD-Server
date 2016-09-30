using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace TinyMUD
{
	public static class FastAccessor
	{
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

		public static Action<object, T> CreateSetter<P, T>(string name) where P : class
		{
			return CreateSetter<T>(typeof(P), name);
		}

		public static Func<object, T> CreateGetter<P, T>(string name)
		{
			return CreateGetter<T>(typeof(P), name);
		}

		public static Action<object, T> CreateSetter<T>(Type type, string name)
		{
			MemberInfo[] members = type.GetMember(name, MemberTypes.Field | MemberTypes.Property,
														BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (MemberInfo member in members)
			{
				if (member.MemberType == MemberTypes.Field)
					return CreateSetter<T>(member as FieldInfo);
				if (member.MemberType == MemberTypes.Property)
					return CreateSetter<T>(member as PropertyInfo);
			}
			throw new MissingFieldException(type.FullName, name);
		}

		public static Func<object, T> CreateGetter<T>(Type type, string name)
		{
			MemberInfo[] members = type.GetMember(name, MemberTypes.Field | MemberTypes.Property,
														BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (MemberInfo member in members)
			{
				if (member.MemberType == MemberTypes.Field)
					return CreateGetter<T>(member as FieldInfo);
				if (member.MemberType == MemberTypes.Property)
					return CreateGetter<T>(member as PropertyInfo);
			}
			throw new MissingFieldException(type.FullName, name);
		}

		public static Action<object, T> CreateSetter<T>(PropertyInfo property)
		{
			MethodInfo method = property.GetSetMethod(true);
			DynamicMethod dm = new DynamicMethod(string.Format("Set_{0}.{1}", property.DeclaringType.FullName, property.Name),
												typeof(void), new Type[] {typeof(object), typeof(T)}, property.DeclaringType, true);
			ILGenerator il = dm.GetILGenerator();
			if (!property.DeclaringType.IsValueType)
			{
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldarg_1);
				if (property.PropertyType != typeof(T))
				{
					if (!property.PropertyType.IsAssignableFrom(typeof(T)))
					{
						TypeCode code = Type.GetTypeCode(property.PropertyType);
						if (code == TypeCode.Boolean)
						{
							switch (Type.GetTypeCode(property.PropertyType))
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
								throw new ArgumentException();
							}
							il.Emit(OpCodes.Ceq);
							il.Emit(OpCodes.Ldc_I4_0);
							il.Emit(OpCodes.Ceq);
						}
						else
						{
							OpCode op = code.ConvertFrom(Type.GetTypeCode(typeof(T)));
							if (op == OpCodes.Throw)
								throw new ArgumentException();
							if (op != OpCodes.Nop)
								il.Emit(op);
						}
					}
				}
				il.Emit(OpCodes.Call, method);
			}
			il.Emit(OpCodes.Ret);
			return (Action<object, T>)dm.CreateDelegate(typeof(Action<object, T>));
		}

		public static Func<object, T> CreateGetter<T>(PropertyInfo property)
		{
			MethodInfo method = property.GetGetMethod(true);
			DynamicMethod dm = new DynamicMethod(string.Format("Get_{0}.{1}", property.DeclaringType.FullName, property.Name),
												typeof(T), new Type[] { typeof(object) }, property.DeclaringType, true);
			ILGenerator il = dm.GetILGenerator();
			il.Emit(OpCodes.Ldarg_0);
			if (property.DeclaringType.IsValueType)
				il.Emit(OpCodes.Unbox_Any, property.DeclaringType);
			il.Emit(OpCodes.Call, method);
			if (property.PropertyType != typeof(T))
			{
				if (!typeof(T).IsAssignableFrom(property.PropertyType))
				{
					TypeCode code = Type.GetTypeCode(typeof(T));
					if (code == TypeCode.Boolean)
					{
						switch (Type.GetTypeCode(property.PropertyType))
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
							throw new ArgumentException();
						}
						il.Emit(OpCodes.Ceq);
						il.Emit(OpCodes.Ldc_I4_0);
						il.Emit(OpCodes.Ceq);
					}
					else
					{
						OpCode op = code.ConvertFrom(Type.GetTypeCode(property.PropertyType));
						if (op == OpCodes.Throw)
							throw new ArgumentException();
						if (op != OpCodes.Nop)
							il.Emit(op);
					}
				}
			}
			il.Emit(OpCodes.Ret);
			return (Func<object, T>)dm.CreateDelegate(typeof(Func<object, T>));
		}

		public static Action<object, T> CreateSetter<T>(FieldInfo field)
		{
			DynamicMethod dm = new DynamicMethod(string.Format("Set_{0}.{1}", field.DeclaringType.FullName, field.Name),
												typeof(void), new Type[] { typeof(object), typeof(T) }, field.DeclaringType, true);
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
								throw new ArgumentException();
							}
							il.Emit(OpCodes.Ceq);
							il.Emit(OpCodes.Ldc_I4_0);
							il.Emit(OpCodes.Ceq);
						}
						else
						{
							OpCode op = code.ConvertFrom(Type.GetTypeCode(typeof(T)));
							if (op == OpCodes.Throw)
								throw new ArgumentException();
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

		public static Func<object, T> CreateGetter<T>(FieldInfo field)
		{
			DynamicMethod dm = new DynamicMethod(string.Format("Get_{0}.{1}", field.DeclaringType.FullName, field.Name),
												typeof(T), new Type[] { typeof(object) }, field.DeclaringType, true);
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
							throw new ArgumentException();
						}
						il.Emit(OpCodes.Ceq);
						il.Emit(OpCodes.Ldc_I4_0);
						il.Emit(OpCodes.Ceq);
					}
					else
					{
						OpCode op = code.ConvertFrom(Type.GetTypeCode(field.FieldType));
						if (op == OpCodes.Throw)
							throw new ArgumentException();
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
