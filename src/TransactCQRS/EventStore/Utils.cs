// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace TransactCQRS.EventStore
{
	internal static class Utils
	{
		private static readonly Random Random = new Random();

		public static string ProtectName(string source)
		{
			return $"{source}_{Random.Next(100000)}";
		}
	}
}
