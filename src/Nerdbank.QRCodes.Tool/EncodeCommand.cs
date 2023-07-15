// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.IO;
using System.Text;
using QRCoder;

namespace Nerdbank.QRCodes.Tool;

/// <summary>
/// Creates bar codes.
/// </summary>
public class EncodeCommand
{
	private const int ModuleSize = 20;
	private const int DefaultWidth = 400;
	private const int DefaultHeight = 400;
	private const QRCodeGenerator.ECCLevel DefaultECCLevel = QRCodeGenerator.ECCLevel.Q;

	private static readonly Dictionary<string, Func<QRCodeData, byte[]>> GraphicFormatWriters = new(StringComparer.OrdinalIgnoreCase)
	{
		[".txt"] = data => Encoding.UTF8.GetBytes(new AsciiQRCode(data).GetGraphic(1)),
		[".bmp"] = data => new BitmapByteQRCode(data).GetGraphic(ModuleSize),
		[".png"] = data => new PngByteQRCode(data).GetGraphic(ModuleSize),
#if WINDOWS
		[".svg"] = data => Encoding.ASCII.GetBytes(new SvgQRCode(data).GetGraphic(ModuleSize)),
		[".pdf"] = data => new PdfByteQRCode(data).GetGraphic(ModuleSize),
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
	public required FileInfo? OutputFile { get; set; }

	/// <summary>
	/// Gets or sets the ECC level.
	/// </summary>
	/// <remarks>
	/// Higher correction level allows more of the QR code to be corrupted, but may require a larger graphic.
	/// </remarks>
	public QRCodeGenerator.ECCLevel ECCLevel { get; set; } = DefaultECCLevel;

	/// <summary>
	/// Gets or sets the width of the output image in pixels.
	/// </summary>
	public int Width { get; set; } = DefaultWidth;

	/// <summary>
	/// Gets or sets the height of the output image in pixels.
	/// </summary>
	public int Height { get; set; } = DefaultHeight;

	private static string SupportedFormatsHelpString => $"Supported formats: {string.Join(", ", GraphicFormatWriters.Keys)}";

	/// <summary>
	/// Creates a <see cref="Command"/> instance for the <c>encode</c> command.
	/// </summary>
	/// <returns>The command instance.</returns>
	public static Command CreateCommand()
	{
		Argument<string> textArg = new("text", "The text to be encoded.");
		Option<FileInfo> outputFileOption = new(new[] { "--outputFile", "-o" }, $"The path to the file to be written with the encoded image. {SupportedFormatsHelpString}");
		Option<QRCodeGenerator.ECCLevel> eccLevelOption = new("--ecc", () => DefaultECCLevel, "The ECC correction level.");
		Option<int> widthOption = new("--width", () => DefaultWidth, "The width in pixels of the output.");
		Option<int> heightOption = new("--height", () => DefaultHeight, "The height in pixels of the output.");

		outputFileOption.AddValidator(result =>
		{
			string? extension = result.GetValueForOption(outputFileOption)?.Extension;
			if (extension is not null && !GraphicFormatWriters.ContainsKey(extension))
			{
				result.ErrorMessage = $"Unsupported output format: {extension}. {SupportedFormatsHelpString}";
			}
		});

		Command command = new("encode", "Creates a QR code.")
		{
			textArg,
			outputFileOption,
			eccLevelOption,
			widthOption,
			heightOption,
		};
		command.SetHandler(ctxt => new EncodeCommand
		{
			Console = ctxt.Console,
			Text = ctxt.ParseResult.GetValueForArgument(textArg),
			OutputFile = ctxt.ParseResult.GetValueForOption(outputFileOption),
			ECCLevel = ctxt.ParseResult.GetValueForOption(eccLevelOption),
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
		QRCodeGenerator generator = new();
		QRCodeData data = generator.CreateQrCode(this.Text, this.ECCLevel);

		AsciiQRCode asciiArt = new(data);
		this.Console?.WriteLine(asciiArt.GetGraphic(1, endOfLine: Environment.NewLine));

		if (this.OutputFile is not null)
		{
			this.Write(data, this.OutputFile);
			this.Console?.WriteLine($"Saved to: \"{this.OutputFile.FullName}\"");
		}
	}

	private void Write(QRCodeData data, FileInfo outputFile)
	{
		string extension = Path.GetExtension(outputFile.FullName);
		if (!GraphicFormatWriters.TryGetValue(extension, out Func<QRCodeData, byte[]>? writer))
		{
			throw new NotSupportedException($"Unsupported output file format: {extension}. {SupportedFormatsHelpString}");
		}

		using FileStream fileStream = outputFile.OpenWrite();
		fileStream.Write(writer(data));
	}
}
