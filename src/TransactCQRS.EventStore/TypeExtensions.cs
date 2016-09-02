// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;

namespace TransactCQRS.EventStore
{
	public static class TypeExtensions
	{
		public static string ToCsDeclaration(this Type source)
		{
			return source.GetTypeInfo().ToCsDeclaration();
		}

		public static string ToCsDeclaration(this TypeInfo source)
		{
			if (!source.IsGenericType) return source.ToString().Replace("+", ".");
			var arguments = source.GenericTypeArguments.Select(ToCsDeclaration).Aggregate((result, item) => $"{result},{item}");
			return $"{new string(source.ToString().TakeWhile(item => item != '`').ToArray())}<{arguments}>";
		}

		public static bool IsSupportedClass(this object value)
		{
			return !(value is string) && value.GetType().GetTypeInfo().IsClass;
		}
	}
}
