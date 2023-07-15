// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using QRCoder;

namespace Nerdbank.QRCodes.Tool;

/// <summary>
/// Creates bar codes.
/// </summary>
public class EncodeCommand
{
	private const int DefaultModuleSize = 20;
	private const bool DefaultNoPadding = false;
	private const QRCodeGenerator.ECCLevel DefaultECCLevel = QRCodeGenerator.ECCLevel.Q;

	private static readonly Color DefaultLightColor = Color.White;
	private static readonly Color DefaultDarkColor = Color.Black;
	private static readonly Dictionary<string, Func<EncodeCommand, QRCodeData, byte[]>> GraphicFormatWriters = new(StringComparer.OrdinalIgnoreCase)
	{
		[".txt"] = (@this, data) => Encoding.UTF8.GetBytes(new AsciiQRCode(data).GetGraphic(1, drawQuietZones: !@this.NoPadding)),
		[".bmp"] = (@this, data) => new BitmapByteQRCode(data).GetGraphic(@this.ModuleSize, ToHtml(@this.DarkColor), ToHtml(@this.LightColor)),
		[".png"] = (@this, data) => new PngByteQRCode(data).GetGraphic(@this.ModuleSize, ToRgba(@this.DarkColor), ToRgba(@this.LightColor), drawQuietZones: !@this.NoPadding),
#if WINDOWS
		[".svg"] = (@this, data) => Encoding.ASCII.GetBytes(new SvgQRCode(data).GetGraphic(@this.ModuleSize, @this.DarkColor, @this.LightColor, drawQuietZones: !@this.NoPadding)),
		[".pdf"] = (@this, data) => new PdfByteQRCode(data).GetGraphic(@this.ModuleSize, ToHtml(@this.DarkColor), ToHtml(@this.LightColor)),
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
	/// Gets or sets the light color to use.
	/// </summary>
	public Color LightColor { get; set; }

	/// <summary>
	/// Gets or sets the dark color to use.
	/// </summary>
	public Color DarkColor { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether padding will be included in the graphic to provide a "quiet zone" to help cameras find the QR code.
	/// </summary>
	public bool NoPadding { get; set; } = DefaultNoPadding;

	/// <summary>
	/// Gets or sets the pixel length for an edge along just one module (one of the small boxes that make up the QR code).
	/// </summary>
	public int ModuleSize { get; set; } = DefaultModuleSize;

	/// <summary>
	/// Gets or sets the ECC level.
	/// </summary>
	/// <remarks>
	/// Higher correction level allows more of the QR code to be corrupted, but may require a larger graphic.
	/// </remarks>
	public QRCodeGenerator.ECCLevel ECCLevel { get; set; } = DefaultECCLevel;

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
		Option<Color> lightColorOption = new("--light-color", ParseColor, description: "The light color to use. N/A for TXT format.");
		Option<Color> darkColorOption = new("--dark-color", ParseColor, description: "The dark color to use. N/A for TXT format.");
		Option<bool> noPaddingOption = new("--no-padding", () => DefaultNoPadding, "Omits the 'quiet zone' around the code in the saved image. Ignored by some formats.");
		Option<int> moduleSizeOption = new("--size", () => DefaultModuleSize, "The length in pixels of an edge of a single module (one of the small boxes that make up the QR code). N/A for TXT format.");

		lightColorOption.SetDefaultValue(DefaultLightColor);
		darkColorOption.SetDefaultValue(DefaultDarkColor);

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
			lightColorOption,
			darkColorOption,
			noPaddingOption,
			moduleSizeOption,
		};
		command.SetHandler(ctxt => new EncodeCommand
		{
			Console = ctxt.Console,
			Text = ctxt.ParseResult.GetValueForArgument(textArg),
			OutputFile = ctxt.ParseResult.GetValueForOption(outputFileOption),
			ECCLevel = ctxt.ParseResult.GetValueForOption(eccLevelOption),
			LightColor = ctxt.ParseResult.GetValueForOption(lightColorOption),
			DarkColor = ctxt.ParseResult.GetValueForOption(darkColorOption),
			NoPadding = ctxt.ParseResult.GetValueForOption(noPaddingOption),
			ModuleSize = ctxt.ParseResult.GetValueForOption(moduleSizeOption),
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

	private static Color ParseColor(ArgumentResult argumentResult)
	{
		string value = argumentResult.Tokens[0].Value;

		try
		{
			Regex hexColor = new(@"^\#[a-f0-9]{6}$");
			if (hexColor.IsMatch(value))
			{
				return ColorTranslator.FromHtml(value);
			}

			if (Color.FromName(value) is { IsKnownColor: true } color)
			{
				return color;
			}

			argumentResult.ErrorMessage = "Unable to parse the specified color. It must be a hex '#rrggbb' value or a well-known color name.";
			return Color.Empty;
		}
		catch (Exception ex)
		{
			argumentResult.ErrorMessage = $"Unable to parse the specified color. It must be a hex '#rrggbb' value or a well-known color name. {ex.Message}";
			return Color.Empty;
		}
	}

	private static byte[] ToRgba(Color color) => new byte[4] { color.R, color.G, color.B, color.A };

	private static string ToHtml(Color color) => $"{color.R:x2}{color.G:x2}{color.B:x2}";

	private void Write(QRCodeData data, FileInfo outputFile)
	{
		string extension = Path.GetExtension(outputFile.FullName);
		if (!GraphicFormatWriters.TryGetValue(extension, out Func<EncodeCommand, QRCodeData, byte[]>? writer))
		{
			throw new NotSupportedException($"Unsupported output file format: {extension}. {SupportedFormatsHelpString}");
		}

		using FileStream fileStream = outputFile.OpenWrite();
		fileStream.Write(writer(this, data));
	}
}
