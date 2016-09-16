// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using TransactCQRS.BlockChain;

namespace TransactCQRS.Tests.BlockChain
{
	public class TestTransaction : ITransaction
	{
		public string Identity { get; set; }
		public DateTimeOffset CreateDate { get; set; }
		public bool IsCommitted { get; set; }
	}
}
