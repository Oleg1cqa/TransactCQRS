// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace TransactCQRS.EventStore.Tests
{
	public abstract class TestEntity
	{
		private readonly List<IReference<ChildTestEntity>> _childs = new List<IReference<ChildTestEntity>>();

		public string State { get; private set; }
		public int Testparametr { get; private set; }
		public IReference<ChildTestEntity> Additional { get; private set; }
		public virtual string Name { get; private set; }

		public virtual IEnumerable<IReference<ChildTestEntity>> Childs => _childs;

		protected TestEntity(string name)
		{
			Name = name;
		}

		[Event]
		public virtual void MakeOperation1()
		{
			State = "AfterMakeOperation1";
		}

		[Event]
		public virtual void MakeOperation1(int testparametr)
		{
			State = "AfterMakeOperation1";
			Testparametr = testparametr;
		}

		[Event]
		public virtual void MakeOperation2(int testparametr)
		{
			State = "AfterMakeOperation2";
			Testparametr = testparametr;
		}

		[Event]
		public virtual void MakeOperation2(int testparametr, IReference<ChildTestEntity> childTestEntity)
		{
			State = "After MakeOperation2";
			Testparametr = testparametr;
			Additional = childTestEntity;
		}

		[Event]
		public abstract ChildTestEntity CreateChild(string name);

		[Event]
		public virtual void AddChild(IReference<ChildTestEntity> child)
		{
			State = "After AddChild";
			_childs.Add(child);
		}

		[Event]
		public virtual void DeleteChild(IReference<ChildTestEntity> child)
		{
			State = "After DeleteChild";
			_childs.Remove(child);
		}
	}

	public class ChildTestEntity
	{
		public string Name { get; private set; }

		public ChildTestEntity(string name)
		{
			Name = name;
		}
	}
}