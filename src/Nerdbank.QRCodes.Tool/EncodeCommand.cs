// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.IO;
using ZXing;
#if WINDOWS
using ZXing.Windows.Compatibility;
#endif

namespace Nerdbank.QRCodes.Tool;

/// <summary>
/// Creates bar codes.
/// </summary>
public class EncodeCommand
{
	private const int DefaultWidth = 400;
	private const int DefaultHeight = 400;

	private static readonly Dictionary<string, Func<BarcodeWriterGeneric>> BarcodeFactories = new(StringComparer.OrdinalIgnoreCase)
	{
		[".svg"] = () => new BarcodeWriterSvg(),
#if WINDOWS
		[".bmp"] = () => new BarcodeWriterWriteableBitmap(),
#endif
	};

	/// <summary>
	/// Gets or sets the console.
	/// </summary>
	public IConsole? Console { get; set; }

	/// <summary>
	/// Gets or sets the text to encode.
	/// </summary>
	public required string Text { get; set; }

	/// <summary>
	/// Gets or sets the file to write the barcode to.
	/// </summary>
	public required FileInfo OutputFile { get; set; }

	/// <summary>
	/// Gets or sets the width of the output image in pixels.
	/// </summary>
	public int Width { get; set; } = DefaultWidth;

	/// <summary>
	/// Gets or sets the height of the output image in pixels.
	/// </summary>
	public int Height { get; set; } = DefaultHeight;

	private static string SupportedFormatsHelpString => $"Supported formats: {string.Join(", ", BarcodeFactories.Keys)}";

	/// <summary>
	/// Creates a <see cref="Command"/> instance for the <c>encode</c> command.
	/// </summary>
	/// <returns>The command instance.</returns>
	public static Command CreateCommand()
	{
		Argument<string> textArg = new("text", "The text to be encoded.");
		Argument<FileInfo> outputFileArg = new("outputFile", $"The path to the file to be written with the encoded image. {SupportedFormatsHelpString}");
		Option<int> widthOption = new("--width", () => DefaultWidth, "The width in pixels of the output.");
		Option<int> heightOption = new("--height", () => DefaultHeight, "The height in pixels of the output.");

		outputFileArg.AddValidator(result =>
		{
			string extension = result.GetValueForArgument(outputFileArg).Extension;
			if (!BarcodeFactories.ContainsKey(extension))
			{
				result.ErrorMessage = $"Unsupported output format: {extension}. {SupportedFormatsHelpString}";
			}
		});

		Command command = new("encode", "Creates a QR code.")
		{
			textArg,
			outputFileArg,
			widthOption,
			heightOption,
		};
		command.SetHandler(ctxt => new EncodeCommand
		{
			Console = ctxt.Console,
			Text = ctxt.ParseResult.GetValueForArgument(textArg),
			OutputFile = ctxt.ParseResult.GetValueForArgument(outputFileArg),
			Width = ctxt.ParseResult.GetValueForOption(widthOption),
			Height = ctxt.ParseResult.GetValueForOption(heightOption),
		}.Execute());
		return command;
	}

	/// <summary>
	/// Executes the command.
	/// </summary>
	public void Execute()
	{
		BarcodeWriterGeneric writer = this.CreateBarcodeWriter();
		writer.Format = BarcodeFormat.QR_CODE;
		writer.Options.Height = this.Height;
		writer.Options.Width = this.Width;
		this.Write(writer);
		this.Console?.WriteLine($"Encoding written to: \"{this.OutputFile.FullName}\"");
	}

	private BarcodeWriterGeneric CreateBarcodeWriter()
	{
		string extension = Path.GetExtension(this.OutputFile.FullName);
		if (BarcodeFactories.TryGetValue(extension, out Func<BarcodeWriterGeneric>? factory))
		{
			return factory();
		}

		throw new NotSupportedException($"Unsupported output file format: {extension}");
	}

	private void Write(BarcodeWriterGeneric writer)
	{
		switch (writer)
		{
			case IBarcodeWriterSvg svg:
				File.WriteAllText(this.OutputFile.FullName, svg.Write(this.Text).ToString());
				break;
#if WINDOWS
			case BarcodeWriterWriteableBitmap bitmapWriter:
				bitmapWriter.WriteAsBitmap(this.Text).Save(this.OutputFile.FullName);
				break;
#endif
			default:
				throw new NotSupportedException();
		}
	}
}
