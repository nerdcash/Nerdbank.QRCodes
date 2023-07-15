// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.Drawing;
using System.IO;
using ZXing;
#if WINDOWS
using ZXing.Windows.Compatibility;
#endif

namespace Nerdbank.Barcodes.Tool;

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
		command.SetHandler(ctxt => new DecodeCommand
		{
			InputPath = ctxt.ParseResult.GetValueForArgument(inputFile),
			Console = ctxt.Console,
		}.Execute());
		return command;
	}

	/// <summary>
	/// Executes the command.
	/// </summary>
	public void Execute()
	{
#if WINDOWS
		using Bitmap bitmap = (Bitmap)Image.FromFile(this.InputPath.FullName);
		BarcodeReader reader = new BarcodeReader();
		Result result = reader.Decode(bitmap);
		this.Console?.WriteLine($"Text:   {result.Text}");
		this.Console?.WriteLine($"Format: {result.BarcodeFormat}");

#else
		this.Console?.WriteLine("This command is only supported on Windows.");
#endif
	}
}
