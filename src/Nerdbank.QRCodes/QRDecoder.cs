// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;

namespace Nerdbank.QRCodes;

/// <summary>
/// Contains functions for decoding QR codes.
/// </summary>
public static class QRDecoder
{
#if WINDOWS
	/// <summary>
	/// Searches for and decodes a QR code from an image or photo.
	/// </summary>
	/// <param name="qrcodePhoto">The bitmap that may contain a QR code.</param>
	/// <param name="data">Receives the data encoded in the QR code, if found.</param>
	/// <returns>A value indicating whether a QR code was found and decoded.</returns>
	public static bool TryDecode(Bitmap qrcodePhoto, [NotNullWhen(true)] out string? data)
	{
		if (qrcodePhoto is null)
		{
			throw new ArgumentNullException(nameof(qrcodePhoto));
		}

		using MemoryStream ms = new();
		qrcodePhoto.Save(ms, ImageFormat.Png);
		return TryDecode(ms.GetBuffer().AsSpan(0, (int)ms.Length), out data);
	}
#endif

	/// <summary>
	/// Searches for and decodes a QR code from a buffer containing an image or photo.
	/// </summary>
	/// <param name="image">The buffer containing an image (e.g. png, bmp, jpg, gif) that may contain a QR code.</param>
	/// <param name="data">The text encoded by the QR code, if one is found; otherwise <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if a QR code was found and decoded; otherwise <see langword="false"/>.</returns>
	public static bool TryDecode(ReadOnlySpan<byte> image, [NotNullWhen(true)] out string? data)
	{
		Span<char> decoded = stackalloc char[1024];
		uint length = NativeMethods.DecodeQrCodeFromImage(image, decoded);
		if (length == 0)
		{
			data = null;
			return false;
		}

		if (length > decoded.Length)
		{
			char[] buffer = ArrayPool<char>.Shared.Rent((int)length);
			length = NativeMethods.DecodeQrCodeFromImage(image, buffer);
			data = buffer.AsSpan(0, (int)length).ToString();
			buffer.AsSpan().Clear();
			ArrayPool<char>.Shared.Return(buffer);
		}
		else
		{
			data = decoded[..(int)length].ToString();
		}

		return true;
	}

	/// <summary>
	/// Searches for and decodes a QR code from an image or photo on disk.
	/// </summary>
	/// <param name="imagePath">The path to the saved image (e.g. png, bmp, jpg, gif) that may contain a QR code.</param>
	/// <param name="data">The text encoded by the QR code, if one is found; otherwise <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if a QR code was found and decoded; otherwise <see langword="false"/>.</returns>
	public static bool TryDecode(ReadOnlySpan<char> imagePath, [NotNullWhen(true)] out string? data)
	{
		Span<char> decoded = stackalloc char[1024];
		uint length = NativeMethods.DecodeQrCodeFromFile(imagePath, decoded);
		if (length == 0)
		{
			data = null;
			return false;
		}

		if (length > decoded.Length)
		{
			char[] buffer = ArrayPool<char>.Shared.Rent((int)length);
			length = NativeMethods.DecodeQrCodeFromFile(imagePath, buffer);
			data = buffer.AsSpan(0, (int)length).ToString();
			buffer.AsSpan().Clear();
			ArrayPool<char>.Shared.Return(buffer);
		}
		else
		{
			data = decoded[..(int)length].ToString();
		}

		return true;
	}
}
