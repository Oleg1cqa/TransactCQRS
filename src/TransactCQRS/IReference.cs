// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace TransactCQRS
{
	/// <summary>
	/// Reference to entity.
	/// </summary>
	public interface IReference<out TEntity> where TEntity : class
	{
		/// <summary>
		/// Define load object in transaction or not.
		/// </summary>
		bool IsLoaded { get; }

		/// <summary>
		/// Load entity in transaction scope.
		/// </summary>
		TEntity Load();

		/// <summary>
		/// Get Identity of Entity.
		/// </summary>
		string Identity { get; }
	}
}
