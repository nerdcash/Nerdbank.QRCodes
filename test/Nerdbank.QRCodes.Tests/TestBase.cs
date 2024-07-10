// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

public abstract class TestBase
{
	protected static Stream GetResourceStream(string name)
	{
		Stream? result = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Resources.{name}");
		return result is not null ? result : throw new ArgumentException($"No resource by the name of \"{name}\" was found.");
	}

	protected static ReadOnlyMemory<byte> GetResourceMemory(string name)
	{
		using Stream stream = GetResourceStream(name);
		MemoryStream ms = new();
		stream.CopyTo(ms);
		return ms.GetBuffer().AsMemory(0, (int)ms.Length);
	}

	protected static string GetResourceFilePath(string name)
	{
		string path = Path.Combine(ThisAssembly.ResourcePath, name);
		return File.Exists(path) ? path : throw new ArgumentException($"No resource by the name of \"{name}\" was found.");
	}
}
