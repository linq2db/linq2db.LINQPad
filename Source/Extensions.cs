using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace LinqToDB.LINQPad
{
	static class Extensions
	{
		public static bool MaybeEqualTo(this Type type1, Type type2)
		{
			return type1.FullName == type2.FullName;
		}

		public static bool MaybeChildOf(this Type type, Type parent)
		{
			do
			{
				if (type.IsGenericType)
				{
					var gtype = type.GetGenericTypeDefinition();

					if (gtype.MaybeEqualTo(parent))
						return true;
				}

				foreach (var inf in type.GetInterfaces())
					if (inf.MaybeChildOf(parent))
						return true;

				type = type.BaseType;
				
			} while(type != null);

			return false;
		}

		public static dynamic GetCustomAttributeLike<T>(this MemberInfo memberInfo)
		{
			return memberInfo.GetCustomAttributes().FirstOrDefault(a => a.GetType().MaybeEqualTo(typeof(T)));
		}

		public static bool HasProperty(dynamic obj, string name)
		{
			Type objType = obj.GetType();

			if (objType == typeof (ExpandoObject))
				return ((IDictionary<string, object>)obj).ContainsKey(name);

			return objType.GetProperty(name) != null;
		}
	}
}
