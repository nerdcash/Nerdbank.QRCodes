// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
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
	/// <summary>
	/// Gets or sets the console.
	/// </summary>
	public IConsole? Console { get; set; }

	/// <summary>
	/// Gets tho QR encoder to use.
	/// </summary>
	public QREncoder Encoder { get; } = new();

	/// <summary>
	/// Gets or sets the text to encode.
	/// </summary>
	public required string Text { get; set; }

	/// <summary>
	/// Gets or sets the file to write the barcode to.
	/// </summary>
	public FileInfo? OutputFile { get; set; }

	private static string SupportedFormatsHelpString => $"Supported formats: {string.Join(", ", QREncoder.SupportedExtensions)}";

	/// <summary>
	/// Creates a <see cref="Command"/> instance for the <c>encode</c> command.
	/// </summary>
	/// <returns>The command instance.</returns>
	public static Command CreateCommand()
	{
		QREncoder defaultEncoder = new();

		Argument<string> textArg = new("text", "The text to be encoded.");
		Option<FileInfo> outputFileOption = new(new[] { "--outputFile", "-o" }, $"The path to the file to be written with the encoded image. {SupportedFormatsHelpString}");

		Option<QRCodeGenerator.ECCLevel> eccLevelOption = new("--ecc", () => defaultEncoder.ECCLevel, "The ECC correction level (0-3 or any of L, M, Q, H).");
		eccLevelOption.AddCompletions(Enum.GetValuesAsUnderlyingType(typeof(QRCodeGenerator.ECCLevel)).Cast<int>().Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray());

		Option<Color> lightColorOption = new("--light-color", ParseColor, description: "The light color to use. N/A for TXT format.");
		lightColorOption.SetDefaultValue(defaultEncoder.LightColor);

		Option<Color> darkColorOption = new("--dark-color", ParseColor, description: "The dark color to use. N/A for TXT format.");
		darkColorOption.SetDefaultValue(defaultEncoder.DarkColor);

		Option<Color> backgroundColorOption = new("--background-color", ParseColor, description: "The background color to use. Ignored by some formats.");
		backgroundColorOption.SetDefaultValue(defaultEncoder.BackgroundColor);

		Option<FileInfo> iconFileOption = new("--icon", "The path to the file containing the logo to superimpose on the QR code.");
		iconFileOption.ExistingOnly();

		Option<int> iconSizePercentOption = new("--icon-size", () => defaultEncoder.IconSizePercent, "The percent of the QR code that should be concealed behind the logo (1-99).");
		iconSizePercentOption.AddValidator(result =>
		{
			if (result.GetValueForOption(iconSizePercentOption) is < 1 or > 99)
			{
				result.ErrorMessage = "Value must fall within the range 1..99, inclusive.";
			}
		});

		Option<int> iconBorderWidthOption = new("--icon-border-width", () => defaultEncoder.IconBorderWidth, "The width (in pixels) of the border around the icon. Minimum 1.");
		iconBorderWidthOption.AddValidator(result =>
		{
			if (result.GetValueForOption(iconBorderWidthOption) is < 1)
			{
				result.ErrorMessage = "Value must be at least 1.";
			}
		});

		Option<Color> iconBackgroundColorOption = new("--icon-background-color", ParseColor, description: "The background color for the icon.");

		Option<bool> noPaddingOption = new("--no-padding", () => defaultEncoder.NoPadding, "Omits the 'quiet zone' around the code in the saved image. Ignored by some formats.");
		Option<int> moduleSizeOption = new("--size", () => defaultEncoder.ModuleSize, "The length in pixels of an edge of a single module (one of the small boxes that make up the QR code). N/A for TXT format.");

		outputFileOption.AddValidator(result =>
		{
			string? extension = result.GetValueForOption(outputFileOption)?.Extension;
			if (extension is not null && !QREncoder.SupportedExtensions.Contains(extension))
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
			backgroundColorOption,
			iconFileOption,
			iconSizePercentOption,
			iconBorderWidthOption,
			iconBackgroundColorOption,
			noPaddingOption,
			moduleSizeOption,
		};
		command.SetHandler(ctxt => new EncodeCommand
		{
			Console = ctxt.Console,
			Text = ctxt.ParseResult.GetValueForArgument(textArg),
			OutputFile = ctxt.ParseResult.GetValueForOption(outputFileOption),
			Encoder =
			{
				ECCLevel = ctxt.ParseResult.GetValueForOption(eccLevelOption),
				LightColor = ctxt.ParseResult.GetValueForOption(lightColorOption),
				DarkColor = ctxt.ParseResult.GetValueForOption(darkColorOption),
				BackgroundColor = ctxt.ParseResult.GetValueForOption(backgroundColorOption),
				IconPath = ctxt.ParseResult.GetValueForOption(iconFileOption),
				IconSizePercent = ctxt.ParseResult.GetValueForOption(iconSizePercentOption),
				IconBorderWidth = ctxt.ParseResult.GetValueForOption(iconBorderWidthOption),
				IconBackgroundColor = ctxt.ParseResult.HasOption(iconBackgroundColorOption) ? ctxt.ParseResult.GetValueForOption(iconBackgroundColorOption) : null,
				NoPadding = ctxt.ParseResult.GetValueForOption(noPaddingOption),
				ModuleSize = ctxt.ParseResult.GetValueForOption(moduleSizeOption),
			},
		}.Execute());
		return command;
	}

	/// <summary>
	/// Executes the command.
	/// </summary>
	public void Execute()
	{
		QRCodeGenerator generator = new();
		QRCodeData data = generator.CreateQrCode(this.Text, this.Encoder.ECCLevel);

		if (Math.Min(System.Console.WindowWidth / 2, System.Console.WindowHeight) < data.ModuleMatrix.Count + 2)
		{
			this.Console?.WriteLine("Window size too small to print an text copy of the QR code.");
		}
		else
		{
			AsciiQRCode asciiArt = new(data);
			this.Console?.WriteLine(asciiArt.GetGraphic(1, endOfLine: Environment.NewLine));
		}

		this.Console?.WriteLine($"QR data size: {data.GetRawData(QRCodeData.Compression.Uncompressed).Length}");
		this.Console?.WriteLine($"QR size: {data.ModuleMatrix.Count}²");

		if (this.OutputFile is not null)
		{
			TraceSource? traceSource = this.Console is null ? null : new("QR writer")
			{
				Listeners = { new TraceSourceIConsoleListener(this.Console) },
			};
			this.Encoder.Encode(data, this.OutputFile, traceSource);
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
}
