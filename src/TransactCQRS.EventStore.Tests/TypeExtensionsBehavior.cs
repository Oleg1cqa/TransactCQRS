// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace TransactCQRS.EventStore.Tests
{
	public class TypeExtensionsBehavior
	{
		[Fact]
		public void ShouldCorrectGetCsName()
		{
			Assert.Equal("System.Int32", typeof(int).ToCsDeclaration());
			Assert.Equal("TransactCQRS.EventStore.IReference<TransactCQRS.EventStore.Tests.TypeExtensionsBehavior>",
				typeof(IReference<TypeExtensionsBehavior>).ToCsDeclaration());
			Assert.Equal("System.Collections.Generic.Dictionary<System.String,System.Object>",
				typeof(Dictionary<string,object>).ToCsDeclaration());
			Assert.Equal("TransactCQRS.EventStore.Tests.TypeExtensionsBehavior.Test", typeof(Test).ToCsDeclaration());
			Assert.Equal("TransactCQRS.EventStore.Tests.TypeExtensionsBehavior.Test.MyStruct", typeof(Test.MyStruct).ToCsDeclaration());
			Assert.Equal("System.Byte[]", typeof(byte[]).ToCsDeclaration());
			Assert.Equal("System.String", typeof(string).ToCsDeclaration());
		}

		public class Test
		{
			public struct MyStruct
			{
				
			}
		}
	}
}
