// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
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
	/// The % of ECC data that exceeds the data obscured by the icon that is required to avoid a warning.
	/// </summary>
	private const int EccToIconObscuringWarningThreshold = 5;

	private const int DefaultIconSizePercent = 15;
	private const int DefaultIconBorderWidth = 6;
	private const int DefaultModuleSize = 20;
	private const bool DefaultNoPadding = false;
	private const QRCodeGenerator.ECCLevel DefaultECCLevel = QRCodeGenerator.ECCLevel.Q;

	private static readonly Color DefaultBackgroundColor = Color.White;
	private static readonly Color DefaultLightColor = Color.White;
	private static readonly Color DefaultDarkColor = Color.Black;
	private static readonly Dictionary<string, Func<EncodeCommand, QRCodeData, byte[]>> GraphicFormatWriters = new(StringComparer.OrdinalIgnoreCase)
	{
		[".txt"] = (@this, data) => Encoding.UTF8.GetBytes(new AsciiQRCode(data).GetGraphic(1, drawQuietZones: !@this.NoPadding)),
#if WINDOWS
		[".bmp"] = (@this, data) => @this.DrawArtsy(data, ImageFormat.Bmp),
		[".png"] = (@this, data) => @this.DrawArtsy(data, ImageFormat.Png),
		[".tif"] = (@this, data) => @this.DrawArtsy(data, ImageFormat.Tiff),
		[".gif"] = (@this, data) => @this.DrawArtsy(data, ImageFormat.Gif),
		[".svg"] = (@this, data) => Encoding.ASCII.GetBytes(new SvgQRCode(data).GetGraphic(@this.ModuleSize, @this.DarkColor, @this.LightColor, drawQuietZones: !@this.NoPadding, logo: @this.GetSvgLogo())),
		[".pdf"] = (@this, data) => new PdfByteQRCode(data).GetGraphic(@this.ModuleSize, ToHtml(@this.DarkColor), ToHtml(@this.LightColor)),
#else
		[".png"] = (@this, data) => new PngByteQRCode(data).GetGraphic(@this.ModuleSize, ToRgba(@this.DarkColor), ToRgba(@this.LightColor), drawQuietZones: !@this.NoPadding),
		[".bmp"] = (@this, data) => new BitmapByteQRCode(data).GetGraphic(@this.ModuleSize, ToHtml(@this.DarkColor), ToHtml(@this.LightColor)),
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
	public FileInfo? OutputFile { get; set; }

	/// <summary>
	/// Gets or sets the light color to use.
	/// </summary>
	public Color LightColor { get; set; }

	/// <summary>
	/// Gets or sets the dark color to use.
	/// </summary>
	public Color DarkColor { get; set; }

	/// <summary>
	/// Gets or sets the background color to use.
	/// </summary>
	public Color BackgroundColor { get; set; }

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

	/// <summary>
	/// Gets or sets the path to the icon that should overlay the center of the QR code.
	/// </summary>
	public FileInfo? IconPath { get; set; }

	/// <summary>
	/// Gets or sets the % of the QR code that should be hidden behind the logo.
	/// </summary>
	public int IconSizePercent { get; set; } = DefaultIconSizePercent;

	/// <summary>
	/// Gets or sets the width of the border (in pixels) around the icon.
	/// </summary>
	public int IconBorderWidth { get; set; } = DefaultIconBorderWidth;

	/// <summary>
	/// Gets or sets the background fill color for the icon.
	/// </summary>
	public Color? IconBackgroundColor { get; set; }

	private static string SupportedFormatsHelpString => $"Supported formats: {string.Join(", ", GraphicFormatWriters.Keys)}";

	private int EccRecoveryPercentage => this.ECCLevel switch
	{
		QRCodeGenerator.ECCLevel.L => 7,
		QRCodeGenerator.ECCLevel.M => 15,
		QRCodeGenerator.ECCLevel.Q => 25,
		QRCodeGenerator.ECCLevel.H => 30,
		_ => throw new NotSupportedException(),
	};

	/// <summary>
	/// Creates a <see cref="Command"/> instance for the <c>encode</c> command.
	/// </summary>
	/// <returns>The command instance.</returns>
	public static Command CreateCommand()
	{
		Argument<string> textArg = new("text", "The text to be encoded.");
		Option<FileInfo> outputFileOption = new(new[] { "--outputFile", "-o" }, $"The path to the file to be written with the encoded image. {SupportedFormatsHelpString}");

		Option<QRCodeGenerator.ECCLevel> eccLevelOption = new("--ecc", () => DefaultECCLevel, "The ECC correction level (0-3 or any of L, M, Q, H).");
		eccLevelOption.AddCompletions(Enum.GetValuesAsUnderlyingType(typeof(QRCodeGenerator.ECCLevel)).Cast<int>().Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray());

		Option<Color> lightColorOption = new("--light-color", ParseColor, description: "The light color to use. N/A for TXT format.");
		lightColorOption.SetDefaultValue(DefaultLightColor);

		Option<Color> darkColorOption = new("--dark-color", ParseColor, description: "The dark color to use. N/A for TXT format.");
		darkColorOption.SetDefaultValue(DefaultDarkColor);

		Option<Color> backgroundColorOption = new("--background-color", ParseColor, description: "The background color to use. Ignored by some formats.");
		backgroundColorOption.SetDefaultValue(DefaultBackgroundColor);

		Option<FileInfo> iconFileOption = new("--icon", "The path to the file containing the logo to superimpose on the QR code.");
		iconFileOption.ExistingOnly();

		Option<int> iconSizePercentOption = new("--icon-size", () => DefaultIconSizePercent, "The percent of the QR code that should be concealed behind the logo (1-99).");
		iconSizePercentOption.AddValidator(result =>
		{
			if (result.GetValueForOption(iconSizePercentOption) is < 1 or > 99)
			{
				result.ErrorMessage = "Value must fall within the range 1..99, inclusive.";
			}
		});

		Option<int> iconBorderWidthOption = new("--icon-border-width", () => DefaultIconBorderWidth, "The width (in pixels) of the border around the icon. Minimum 1.");
		iconBorderWidthOption.AddValidator(result =>
		{
			if (result.GetValueForOption(iconBorderWidthOption) is < 1)
			{
				result.ErrorMessage = "Value must be at least 1.";
			}
		});

		Option<Color> iconBackgroundColorOption = new("--icon-background-color", ParseColor, description: "The background color for the icon.");

		Option<bool> noPaddingOption = new("--no-padding", () => DefaultNoPadding, "Omits the 'quiet zone' around the code in the saved image. Ignored by some formats.");
		Option<int> moduleSizeOption = new("--size", () => DefaultModuleSize, "The length in pixels of an edge of a single module (one of the small boxes that make up the QR code). N/A for TXT format.");

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

		if (Math.Min(System.Console.WindowWidth / 2, System.Console.WindowHeight) < data.ModuleMatrix.Count + 2)
		{
			this.Console?.WriteLine("Window size too small to print an text copy of the QR code.");
		}
		else
		{
			AsciiQRCode asciiArt = new(data);
			this.Console?.WriteLine(asciiArt.GetGraphic(1, endOfLine: Environment.NewLine));
		}

		System.Console.WriteLine($"QR data size: {data.GetRawData(QRCodeData.Compression.Uncompressed).Length}");
		System.Console.WriteLine($"QR size: {data.ModuleMatrix.Count}²");

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

#if WINDOWS

	private SvgQRCode.SvgLogo? GetSvgLogo()
	{
		using Bitmap? logo = this.GetLogoBitmap();
		return logo is null ? null : new(logo, this.IconSizePercent, this.IconBackgroundColor is not null);
	}

	private Bitmap? GetLogoBitmap()
	{
		if (this.IconPath is not null)
		{
			Image iconImage = Image.FromFile(this.IconPath.FullName);
			if (iconImage is not Bitmap)
			{
				// TODO: Convert.
			}

			return (Bitmap)iconImage;
		}

		return null;
	}

	private byte[] DrawArtsy(QRCodeData data, ImageFormat imageFormat)
	{
		Bitmap? icon = null;
		try
		{
			if (this.IconPath is not null)
			{
				icon = this.GetLogoBitmap();

				if (this.IconSizePercent > this.EccRecoveryPercentage)
				{
					this.Console?.Error.WriteLine($"ERROR: Icon will obscure {this.IconSizePercent}% of the QR code but ECC setting only allows recovery of up to {this.EccRecoveryPercentage}% missing or corrupted data.");
				}
				else if (this.EccRecoveryPercentage - this.IconSizePercent < EccToIconObscuringWarningThreshold)
				{
					this.Console?.Error.WriteLine($"WARNING: Icon will obscure {this.IconSizePercent}% of the QR code but ECC setting only allows recovery of up to {this.EccRecoveryPercentage}% missing or corrupted data, leaving {this.EccRecoveryPercentage - this.IconSizePercent}% ECC data remaining.");
				}
			}

			bool circles = false;
			using Bitmap bitmap = icon is not null || !circles
				? new QRCode(data).GetGraphic(
					this.ModuleSize,
					this.DarkColor,
					this.LightColor,
					icon,
					this.IconSizePercent,
					this.IconBorderWidth,
					drawQuietZones: !this.NoPadding,
					this.IconBackgroundColor)
				: new ArtQRCode(data).GetGraphic(
					this.ModuleSize,
					this.DarkColor,
					this.LightColor,
					this.BackgroundColor,
					drawQuietZones: !this.NoPadding,
					quietZoneRenderingStyle: ArtQRCode.QuietZoneStyle.Flat,
					backgroundImageStyle: ArtQRCode.BackgroundImageStyle.Fill);
			using MemoryStream ms = new();
			bitmap.Save(ms, imageFormat);
			return ms.ToArray();
		}
		finally
		{
			icon?.Dispose();
		}
	}
#endif

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
