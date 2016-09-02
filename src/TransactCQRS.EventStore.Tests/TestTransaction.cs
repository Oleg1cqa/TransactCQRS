// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace TransactCQRS.EventStore.Tests
{
	public abstract class TestTransaction : AbstractTransaction
	{
		public string Creator { get; private set; }

		[Event]
		public abstract TestEntity CreateTestEntity(string name);

		[Event]
		public virtual void SetCreator(string value)
		{
			Creator = value;
		}
	}
}