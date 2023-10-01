// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.Versioning;
#if WINDOWS
using ZXing;
using ZXing.Windows.Compatibility;
#endif

namespace Nerdbank.QRCodes;

/// <summary>
/// Contains functions for decoding QR codes.
/// </summary>
public static class QRDecoder
{
	/// <summary>
	/// Finds and decodes a QR code from an image or photo.
	/// </summary>
	/// <param name="qrcodePhoto">The bitmap that may contain a QR code.</param>
	/// <param name="data">Receives the data encoded in the QR code, if found.</param>
	/// <returns>A value indicating whether a QR code was found and decoded.</returns>
	[SupportedOSPlatform("windows")]
	public static bool TryDecode(Bitmap qrcodePhoto, [NotNullWhen(true)] out string? data)
	{
#if WINDOWS
		BarcodeReader reader = new BarcodeReader();
		Result? result = reader.Decode(qrcodePhoto);
		if (result?.BarcodeFormat == BarcodeFormat.QR_CODE)
		{
			data = result.Text;
			return true;
		}

		data = null;
		return false;
#else
		data = null;
		return false;
#endif
	}
}
