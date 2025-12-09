// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Nerdbank.QRCodes.Tool;

/// <summary>
/// An adapter so that a <see cref="TraceSource"/> may log to a <see cref="TextWriter"/>.
/// </summary>
internal class TraceSourceTextWriterListener : TraceListener
{
	private readonly TextWriter output;

	/// <summary>
	/// Initializes a new instance of the <see cref="TraceSourceTextWriterListener"/> class.
	/// </summary>
	/// <param name="output">The output writer to log to.</param>
	internal TraceSourceTextWriterListener(TextWriter output)
	{
		this.output = output;
	}

	/// <inheritdoc/>
	public override void Write(string? message) => this.output.Write(message ?? string.Empty);

	/// <inheritdoc/>
	public override void WriteLine(string? message) => this.output.WriteLine(message ?? string.Empty);
}
