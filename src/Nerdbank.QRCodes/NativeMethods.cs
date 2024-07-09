// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.QRCodes;

/// <summary>
/// The functions and data types imported from rust.
/// </summary>
internal static unsafe class NativeMethods
{
	private const string LibraryName = "nerdbank_zcash_rust";

	/// <summary>
	/// Decodes a QR code from an image saved to a file.
	/// </summary>
	/// <param name="filePath">The path to the image.</param>
	/// <param name="decoded">The buffer to receive the decoded characters.</param>
	/// <returns>
	/// The number of characters in the QR code. This may exceed the length of <paramref name="decoded"/>, in which case the caller should repeat the call with a larger buffer.
	/// </returns>
	internal static unsafe uint DecodeQrCodeFromFile(string filePath, Span<char> decoded)
	{
		fixed (char* filePathPtr = filePath)
		{
			fixed (char* decodedPtr = decoded)
			{
				int result = decode_qr_code_from_file(filePathPtr, (nuint)filePath.Length, decodedPtr, (nuint)decoded.Length);
				return result >= 0 ? checked((uint)result) : throw new InvalidOperationException("Failed to decode QR code.");
			}
		}
	}

	/// <summary>
	/// Decodes a QR code from an image saved to a file.
	/// </summary>
	/// <param name="image">The image to decode.</param>
	/// <param name="decoded">The buffer to receive the decoded characters.</param>
	/// <returns>
	/// The number of characters in the QR code. This may exceed the length of <paramref name="decoded"/>, in which case the caller should repeat the call with a larger buffer.
	/// </returns>
	internal static unsafe uint DecodeQrCodeFromImage(ReadOnlySpan<byte> image, Span<char> decoded)
	{
		fixed (byte* imagePtr = image)
		{
			fixed (char* decodedPtr = decoded)
			{
				int result = decode_qr_code_from_image(imagePtr, (nuint)image.Length, decodedPtr, (nuint)decoded.Length);
				return result >= 0 ? checked((uint)result) : throw new InvalidOperationException("Failed to decode QR code.");
			}
		}
	}

	[DllImport(LibraryName)]
	private static unsafe extern int decode_qr_code_from_file(char* file_path, nuint file_path_length, char* decoded, nuint decoded_length);

	[DllImport(LibraryName)]
	private static unsafe extern int decode_qr_code_from_image(byte* image, nuint image_length, char* decoded, nuint decoded_length);
}
