﻿// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace TransactCQRS.EventStore.Builders
{
	public interface ITransactionFactory
	{
		string RootEventName { get; }
		AbstractTransaction Create(AbstractRepository repository, string description);
		AbstractTransaction Load(AbstractRepository repository, IEnumerable<AbstractRepository.EventData> events);
	}
}