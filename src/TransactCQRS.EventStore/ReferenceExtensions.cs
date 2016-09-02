// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace TransactCQRS.EventStore
{
	public static class ReferenceExtensions
	{
		public static IReference<TEntity> GetReference<TEntity>(this TEntity source) where TEntity : class
		{
			var result = source as IReference<TEntity>;
			if (result == null)
				throw new InvalidOperationException("Unsupported type of entity.");
			return result;

		}
	}
}
