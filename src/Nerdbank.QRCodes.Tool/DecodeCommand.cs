// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.Drawing;

namespace Nerdbank.QRCodes.Tool;

/// <summary>
/// Reads the text from an image of a barcode.
/// </summary>
public class DecodeCommand
{
	/// <summary>
	/// Gets or sets the path to the image to decode.
	/// </summary>
	public required FileInfo InputPath { get; set; }

	/// <summary>
	/// Gets or sets the console.
	/// </summary>
	public IConsole? Console { get; set; }

	/// <summary>
	/// Creates the command.
	/// </summary>
	/// <returns>The decoded text from the image.</returns>
	public static Command CreateCommand()
	{
		Argument<FileInfo> inputFile = new("imagePath", "The path to the image to decode. Must be a .bmp file.");

		Command command = new("decode", "Reads the text from an image of a QR code.")
		{
			inputFile,
		};
		command.SetHandler(ctxt => ctxt.ExitCode = new DecodeCommand
		{
			InputPath = ctxt.ParseResult.GetValueForArgument(inputFile),
			Console = ctxt.Console,
		}.Execute());
		return command;
	}

	/// <summary>
	/// Executes the command.
	/// </summary>
	/// <returns>The exit code.</returns>
	public int Execute()
	{
#if WINDOWS
		using Bitmap bitmap = (Bitmap)Image.FromFile(this.InputPath.FullName);
		if (QRDecoder.TryDecode(bitmap, out string? data))
		{
			this.Console?.WriteLine(data);
			return 0;
		}
		else
		{
			this.Console?.WriteLine("Failed to discover a QR code.");
			return 1;
		}
#else
		this.Console?.WriteLine("This command is only supported on Windows.");
		return 2;
#endif
	}
}
