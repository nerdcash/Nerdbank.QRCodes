// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
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
	/// Gets or sets the output writer.
	/// </summary>
	public TextWriter? Output { get; set; }

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

		Argument<string> textArg = new("text");
		textArg.Description = "The text to be encoded.";

		Option<FileInfo> outputFileOption = new("--outputFile", new[] { "-o" });
		outputFileOption.Description = $"The path to the file to be written with the encoded image. {SupportedFormatsHelpString}";

		Option<QRCodeGenerator.ECCLevel> eccLevelOption = new("--ecc");
		eccLevelOption.Description = "The ECC correction level (0-3 or any of L, M, Q, H).";
		eccLevelOption.DefaultValueFactory = _ => defaultEncoder.ECCLevel;
		eccLevelOption.CompletionSources.Add(context =>
		{
			return Enum.GetValuesAsUnderlyingType(typeof(QRCodeGenerator.ECCLevel))
				.Cast<int>()
				.Select(v => new System.CommandLine.Completions.CompletionItem(v.ToString(CultureInfo.InvariantCulture)));
		});

		Option<Color> lightColorOption = new("--light-color");
		lightColorOption.Description = "The light color to use. N/A for TXT format.";
		lightColorOption.CustomParser = ParseColor;
		lightColorOption.DefaultValueFactory = _ => defaultEncoder.LightColor;

		Option<Color> darkColorOption = new("--dark-color");
		darkColorOption.Description = "The dark color to use. N/A for TXT format.";
		darkColorOption.CustomParser = ParseColor;
		darkColorOption.DefaultValueFactory = _ => defaultEncoder.DarkColor;

		Option<Color> backgroundColorOption = new("--background-color");
		backgroundColorOption.Description = "The background color to use. Ignored by some formats.";
		backgroundColorOption.CustomParser = ParseColor;
		backgroundColorOption.DefaultValueFactory = _ => defaultEncoder.BackgroundColor;

		Option<FileInfo> iconFileOption = new("--icon");
		iconFileOption.Description = "The path to the file containing the logo to superimpose on the QR code.";
		iconFileOption.Validators.Add(result =>
		{
			FileInfo? file = result.GetValue(iconFileOption);
			if (file != null && !file.Exists)
			{
				result.AddError($"File does not exist: {file.FullName}");
			}
		});

		Option<int> iconSizePercentOption = new("--icon-size");
		iconSizePercentOption.Description = "The percent of the QR code that should be concealed behind the logo (1-99).";
		iconSizePercentOption.DefaultValueFactory = _ => defaultEncoder.IconSizePercent;
		iconSizePercentOption.Validators.Add(result =>
		{
			if (result.GetValue(iconSizePercentOption) is < 1 or > 99)
			{
				result.AddError("Value must fall within the range 1..99, inclusive.");
			}
		});

		Option<int> iconBorderWidthOption = new("--icon-border-width");
		iconBorderWidthOption.Description = "The width (in pixels) of the border around the icon. Minimum 1.";
		iconBorderWidthOption.DefaultValueFactory = _ => defaultEncoder.IconBorderWidth;
		iconBorderWidthOption.Validators.Add(result =>
		{
			if (result.GetValue(iconBorderWidthOption) is < 1)
			{
				result.AddError("Value must be at least 1.");
			}
		});

		Option<Color> iconBackgroundColorOption = new("--icon-background-color");
		iconBackgroundColorOption.Description = "The background color for the icon.";
		iconBackgroundColorOption.CustomParser = ParseColor;

		Option<bool> noPaddingOption = new("--no-padding");
		noPaddingOption.Description = "Omits the 'quiet zone' around the code in the saved image. Ignored by some formats.";
		noPaddingOption.DefaultValueFactory = _ => defaultEncoder.NoPadding;

		Option<int> moduleSizeOption = new("--size");
		moduleSizeOption.Description = "The length in pixels of an edge of a single module (one of the small boxes that make up the QR code). N/A for TXT format.";
		moduleSizeOption.DefaultValueFactory = _ => defaultEncoder.ModuleSize;

		outputFileOption.Validators.Add(result =>
		{
			string? extension = result.GetValue(outputFileOption)?.Extension;
			if (extension is not null && !QREncoder.SupportedExtensions.Contains(extension))
			{
				result.AddError($"Unsupported output format: {extension}. {SupportedFormatsHelpString}");
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
		command.SetAction(ctxt =>
		{
			new EncodeCommand
			{
				Output = ctxt.InvocationConfiguration.Output,
				Text = ctxt.GetValue(textArg)!,
				OutputFile = ctxt.GetValue(outputFileOption),
				Encoder =
				{
					ECCLevel = ctxt.GetValue(eccLevelOption),
					LightColor = ctxt.GetValue(lightColorOption),
					DarkColor = ctxt.GetValue(darkColorOption),
					BackgroundColor = ctxt.GetValue(backgroundColorOption),
					IconPath = ctxt.GetValue(iconFileOption),
					IconSizePercent = ctxt.GetValue(iconSizePercentOption),
					IconBorderWidth = ctxt.GetValue(iconBorderWidthOption),
					IconBackgroundColor = ctxt.GetResult(iconBackgroundColorOption) != null ? ctxt.GetValue(iconBackgroundColorOption) : null,
					NoPadding = ctxt.GetValue(noPaddingOption),
					ModuleSize = ctxt.GetValue(moduleSizeOption),
				},
			}.Execute();
			return Task.CompletedTask;
		});
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
			this.Output?.WriteLine("Window size too small to print an text copy of the QR code.");
		}
		else
		{
			AsciiQRCode asciiArt = new(data);
			this.Output?.WriteLine(asciiArt.GetGraphic(1, endOfLine: Environment.NewLine));
		}

		this.Output?.WriteLine($"QR data size: {data.GetRawData(QRCodeData.Compression.Uncompressed).Length}");
		this.Output?.WriteLine($"QR size: {data.ModuleMatrix.Count}²");

		if (this.OutputFile is not null)
		{
			TraceSource? traceSource = this.Output is null ? null : new("QR writer")
			{
				Listeners = { new TraceSourceTextWriterListener(this.Output) },
			};
			this.Encoder.Encode(data, this.OutputFile, traceSource);
			this.Output?.WriteLine($"Saved to: \"{this.OutputFile.FullName}\"");
		}
	}

	private static Color ParseColor(ArgumentResult argumentResult)
	{
		if (argumentResult.Tokens.Count == 0)
		{
			return Color.Empty;
		}

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

			argumentResult.AddError("Unable to parse the specified color. It must be a hex '#rrggbb' value or a well-known color name.");
			return Color.Empty;
		}
		catch (Exception ex)
		{
			argumentResult.AddError($"Unable to parse the specified color. It must be a hex '#rrggbb' value or a well-known color name. {ex.Message}");
			return Color.Empty;
		}
	}
}
