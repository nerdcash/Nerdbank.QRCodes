// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.Diagnostics;

namespace Nerdbank.QRCodes.Tool;

/// <summary>
/// An adapter so that a <see cref="TraceSource"/> may log to an <see cref="IConsole"/>.
/// </summary>
internal class TraceSourceIConsoleListener : TraceListener
{
	private readonly IConsole console;

	/// <summary>
	/// Initializes a new instance of the <see cref="TraceSourceIConsoleListener"/> class.
	/// </summary>
	/// <param name="console">The console to log to.</param>
	internal TraceSourceIConsoleListener(IConsole console)
	{
		this.console = console;
	}

	/// <inheritdoc/>
	public override void Write(string? message) => this.console.Write(message ?? string.Empty);

	/// <inheritdoc/>
	public override void WriteLine(string? message) => this.console.WriteLine(message ?? string.Empty);
}
