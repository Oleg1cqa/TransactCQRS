// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Reflection;

namespace TransactCQRS.EventStore
{
	internal class ParamsBuilder
	{
		private readonly ParameterInfo[] _params;

		public ParamsBuilder(MethodBase method)
		{
			_params = method.GetParameters();
		}

		public string GetNameList()
		{
			if (!_params.Any())
				return string.Empty;
			return _params.Select(item => item.Name)
				.Aggregate((result, item) => $"{result}, {item}");
		}

		public string CreateDeclarations()
		{
			if (!_params.Any())
				return string.Empty;
			return _params.Select(item => $"{item.ParameterType.ToCsDeclaration()} {item.Name}")
				.Aggregate((result, item) => $"{result}, {item}");
		}

		public string CreateParamsWith(string eventParams)
		{
			if (!_params.Any())
				return string.Empty;
			return _params.Select(item => $"{item.Name}: ({item.ParameterType.ToCsDeclaration()}){eventParams}[\"{item.Name}\"]")
				.Aggregate((result, item) => $"{result}, {item}");
		}

		public string GetQuotedList()
		{
			if (!_params.Any())
				return string.Empty;
			return ", " + _params.Select(item => $"\"{item.Name}\"")
				.Aggregate((result, item) => $"{result}, {item}");
		}

		public string CreateDictionaryParams()
		{
			if (!_params.Any())
				return string.Empty;
			return _params.Select(item => $"{{\"{item.Name}\", {item.Name}}}")
				.Aggregate((result, item) => $"{result}, {item}");
		}

		internal string BuildParameterConversion(string ownerName)
		{
			var result = _params.Where(item => item.ParameterType.IsIReference())
				.Select(item =>
					$"@event.Params[\"{item.Name}\"] = new LazyLoadEntity<{item.ParameterType.GenericTypeArguments[0].ToCsDeclaration()}>(_{ownerName}, (string)@event.Params[\"{item.Name}\"]);")
				.Concat(_params.Where(item => item.ParameterType.IsSupportedClass())
					.Where(item => !item.ParameterType.IsIReference())
					.Select(item =>
						$"@event.Params[\"{item.Name}\"] = _{ownerName}.GetEntity<{item.ParameterType.ToCsDeclaration()}>((string)@event.Params[\"{item.Name}\"]);"))
				.ToArray();
			return result.Any() ? result.Aggregate((aggregate, item) => $"{aggregate}\r\n{item}") : string.Empty;
		}
	}
}