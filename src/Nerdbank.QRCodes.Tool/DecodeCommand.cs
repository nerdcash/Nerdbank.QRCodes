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
	/// Gets or sets the output writer.
	/// </summary>
	public TextWriter? Output { get; set; }

	/// <summary>
	/// Creates the command.
	/// </summary>
	/// <returns>The decoded text from the image.</returns>
	public static Command CreateCommand()
	{
		Argument<FileInfo> inputFile = new("imagePath");
		inputFile.Description = "The path to the image to decode. Must be a .bmp file.";

		Command command = new("decode", "Reads the text from an image of a QR code.")
		{
			inputFile,
		};
		command.SetAction(ctxt =>
		{
			int exitCode = new DecodeCommand
			{
				InputPath = ctxt.GetValue(inputFile),
				Output = ctxt.InvocationConfiguration.Output,
			}.Execute();
			return Task.FromResult(exitCode);
		});
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
			this.Output?.WriteLine(data);
			return 0;
		}
		else
		{
			this.Output?.WriteLine("Failed to discover a QR code.");
			return 1;
		}
#else
		this.Output?.WriteLine("This command is only supported on Windows.");
		return 2;
#endif
	}
}
