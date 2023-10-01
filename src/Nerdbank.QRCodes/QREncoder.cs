// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using QRCoder;

namespace Nerdbank.QRCodes;

/// <summary>
/// QR Code creator.
/// </summary>
public class QREncoder
{
	/// <summary>
	/// The % of ECC data that exceeds the data obscured by the icon that is required to avoid a warning.
	/// </summary>
	private const int EccToIconObscuringWarningThreshold = 5;

	private static readonly Dictionary<string, Func<QREncoder, QRCodeData, TraceSource?, byte[]>> GraphicFormatWriters = new(StringComparer.OrdinalIgnoreCase)
	{
		[".txt"] = (@this, data, traceSource) => Encoding.UTF8.GetBytes(new AsciiQRCode(data).GetGraphic(1, drawQuietZones: !@this.NoPadding)),
#if WINDOWS
		[".bmp"] = (@this, data, traceSource) => @this.DrawArtsy(data, ImageFormat.Bmp, traceSource),
		[".png"] = (@this, data, traceSource) => @this.DrawArtsy(data, ImageFormat.Png, traceSource),
		[".tif"] = (@this, data, traceSource) => @this.DrawArtsy(data, ImageFormat.Tiff, traceSource),
		[".gif"] = (@this, data, traceSource) => @this.DrawArtsy(data, ImageFormat.Gif, traceSource),
		[".svg"] = (@this, data, traceSource) => Encoding.ASCII.GetBytes(new SvgQRCode(data).GetGraphic(@this.ModuleSize, @this.DarkColor, @this.LightColor, drawQuietZones: !@this.NoPadding, logo: @this.GetSvgLogo())),
		[".pdf"] = (@this, data, traceSource) => new PdfByteQRCode(data).GetGraphic(@this.ModuleSize, ToHtml(@this.DarkColor), ToHtml(@this.LightColor)),
#else
		[".png"] = (@this, data, traceSource) => new PngByteQRCode(data).GetGraphic(@this.ModuleSize, ToRgba(@this.DarkColor), ToRgba(@this.LightColor), drawQuietZones: !@this.NoPadding),
		[".bmp"] = (@this, data, traceSource) => new BitmapByteQRCode(data).GetGraphic(@this.ModuleSize, ToHtml(@this.DarkColor), ToHtml(@this.LightColor)),
#endif
	};

	/// <summary>
	/// Gets the file extensions associated with supported output image types.
	/// </summary>
	public static IReadOnlySet<string> SupportedExtensions { get; } = GraphicFormatWriters.Keys.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets or sets the light color to use.
	/// </summary>
	public Color LightColor { get; set; } = Color.White;

	/// <summary>
	/// Gets or sets the dark color to use.
	/// </summary>
	public Color DarkColor { get; set; } = Color.Black;

	/// <summary>
	/// Gets or sets the background color to use.
	/// </summary>
	public Color BackgroundColor { get; set; } = Color.White;

	/// <summary>
	/// Gets or sets a value indicating whether padding will be included in the graphic to provide a "quiet zone" to help cameras find the QR code.
	/// </summary>
	public bool NoPadding { get; set; }

	/// <summary>
	/// Gets or sets the pixel length for an edge along just one module (one of the small boxes that make up the QR code).
	/// </summary>
	public int ModuleSize { get; set; } = 20;

	/// <summary>
	/// Gets or sets the ECC level.
	/// </summary>
	/// <remarks>
	/// Higher correction level allows more of the QR code to be corrupted, but may require a larger graphic.
	/// </remarks>
	public QRCodeGenerator.ECCLevel ECCLevel { get; set; } = QRCodeGenerator.ECCLevel.Q;

	/// <summary>
	/// Gets or sets the % of the QR code that should be hidden behind the logo.
	/// </summary>
	public int IconSizePercent { get; set; } = 15;

	/// <summary>
	/// Gets or sets the width of the border (in pixels) around the icon.
	/// </summary>
	public int IconBorderWidth { get; set; } = 6;

	/// <summary>
	/// Gets or sets the background fill color for the icon.
	/// </summary>
	public Color? IconBackgroundColor { get; set; }

	/// <summary>
	/// Gets or sets the path to the icon that should overlay the center of the QR code.
	/// </summary>
	public FileInfo? IconPath { get; set; }

	private static string SupportedFormatsHelpString => $"Supported formats: {string.Join(", ", QREncoder.SupportedExtensions)}";

#if WINDOWS

	private int EccRecoveryPercentage => this.ECCLevel switch
	{
		QRCodeGenerator.ECCLevel.L => 7,
		QRCodeGenerator.ECCLevel.M => 15,
		QRCodeGenerator.ECCLevel.Q => 25,
		QRCodeGenerator.ECCLevel.H => 30,
		_ => throw new NotSupportedException(),
	};

#endif

	/// <summary>
	/// Encodes data in a QR code.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="fileFormatExtension">The file extension (e.g. <c>.png</c>) of the image format to encode to. Check <see cref="SupportedExtensions"/> for supported extensions, which vary by platform.</param>
	/// <param name="traceSource">An optional logger to get warning or error messages.</param>
	/// <returns>The buffer containing the encoded image.</returns>
	/// <exception cref="NotSupportedException">Thrown if the file extension is not supported.</exception>
	public byte[] Encode(QRCodeData data, string fileFormatExtension, TraceSource? traceSource)
	{
		if (data is null)
		{
			throw new ArgumentNullException(nameof(data));
		}

		if (fileFormatExtension is null)
		{
			throw new ArgumentNullException(nameof(fileFormatExtension));
		}

		if (!GraphicFormatWriters.TryGetValue(fileFormatExtension, out Func<QREncoder, QRCodeData, TraceSource?, byte[]>? writer))
		{
			throw new NotSupportedException($"Unsupported output file format: {fileFormatExtension}. {SupportedFormatsHelpString}");
		}

		return writer(this, data, traceSource);
	}

	/// <summary>
	/// Encodes data in a QR code.
	/// </summary>
	/// <param name="data">The data to encode.</param>
	/// <param name="outputFile">The path to the file to save the image to. Check <see cref="SupportedExtensions"/> for supported extensions, which vary by platform.</param>
	/// <param name="traceSource">An optional logger to get warning or error messages.</param>
	/// <exception cref="NotSupportedException">Thrown if the file extension is not supported.</exception>
	public void Encode(QRCodeData data, FileInfo outputFile, TraceSource? traceSource)
	{
		if (data is null)
		{
			throw new ArgumentNullException(nameof(data));
		}

		if (outputFile is null)
		{
			throw new ArgumentNullException(nameof(outputFile));
		}

		string extension = Path.GetExtension(outputFile.FullName);
		if (!GraphicFormatWriters.TryGetValue(extension, out Func<QREncoder, QRCodeData, TraceSource?, byte[]>? writer))
		{
			throw new NotSupportedException($"Unsupported output file format: {extension}. {SupportedFormatsHelpString}");
		}

		using FileStream fileStream = outputFile.OpenWrite();
		fileStream.Write(writer(this, data, traceSource));

		// Truncate if rewriting.
		fileStream.SetLength(fileStream.Position);
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

	private byte[] DrawArtsy(QRCodeData data, ImageFormat imageFormat, TraceSource? logger)
	{
		Bitmap? icon = null;
		try
		{
			if (this.IconPath is not null)
			{
				icon = this.GetLogoBitmap();

				if (this.IconSizePercent > this.EccRecoveryPercentage)
				{
					logger?.TraceEvent(TraceEventType.Error, 0, $"ERROR: Icon will obscure {this.IconSizePercent}% of the QR code but ECC setting only allows recovery of up to {this.EccRecoveryPercentage}% missing or corrupted data.");
				}
				else if (this.EccRecoveryPercentage - this.IconSizePercent < EccToIconObscuringWarningThreshold)
				{
					logger?.TraceEvent(TraceEventType.Warning, 0, $"WARNING: Icon will obscure {this.IconSizePercent}% of the QR code but ECC setting only allows recovery of up to {this.EccRecoveryPercentage}% missing or corrupted data, leaving {this.EccRecoveryPercentage - this.IconSizePercent}% ECC data remaining.");
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
}
