// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;

namespace TransactCQRS.EventStore
{
	public static class Extensions
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
			return value.GetType().IsSupportedClass();
		}

		public static bool IsSupportedClass(this Type type)
		{
			return type != typeof(string) && type.GetTypeInfo().IsClass;
		}

		public static bool IsIReference(this object value)
		{
			return value.GetType().IsIReference();
		}
		public static bool IsIReference(this Type type)
		{
			return type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(IReference<>);
		}

		/// <summary>
		/// Make reference to Entity.
		/// </summary>
		public static IReference<TEntity> GetReference<TEntity>(this TEntity source) where TEntity : class
		{
			var result = source as IReference<TEntity>;
			if (result == null)
				throw new InvalidOperationException(Resources.TextResource.UnsupportedTypeOfEntity);
			return result;
		}

		/// <summary>
		/// Get Identity of Entity.
		/// </summary>
		public static string GetIdentity<TEntity>(this TEntity source) where TEntity : class
		{
			var result = source as IReference<TEntity>;
			if (result == null)
				throw new InvalidOperationException(Resources.TextResource.UnsupportedTypeOfEntity);
			return result.Identity;
		}

		/// <summary>
		/// Get Identity of Entity.
		/// </summary>
		public static string GetIdentity(this object source)
		{
			if (!source.IsIReference()) throw new ArgumentOutOfRangeException(nameof(source));
			return (string)source.GetType()
				.GetRuntimeProperty(nameof(IReference<object>.Identity))
				.GetValue(source);
		}
	}
}
