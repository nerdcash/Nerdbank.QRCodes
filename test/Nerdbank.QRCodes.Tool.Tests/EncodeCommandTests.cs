// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class EncodeCommandTests
{
	[Fact]
	public void Ctor()
	{
		var command = new EncodeCommand
		{
			Text = "Hello, world!",
			OutputFile = new FileInfo("foo.svg"),
		};
		Assert.Equal("Hello, world!", command.Text);
	}
}
