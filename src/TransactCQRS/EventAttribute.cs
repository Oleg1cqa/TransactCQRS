// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace TransactCQRS
{
	/// <summary>
	/// Attribute that marked method as event.
	/// Method should be abstract or virtual.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	public class EventAttribute : Attribute
	{
	}
}
