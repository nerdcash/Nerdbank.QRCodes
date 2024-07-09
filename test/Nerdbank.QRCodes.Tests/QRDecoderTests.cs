// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class QRDecoderTests : TestBase
{
	[Fact]
	public void TryDecode_Span_NoQRCode()
	{
		ReadOnlyMemory<byte> photo = GetResourceMemory("noQRcode.jpg");
		Assert.False(QRDecoder.TryDecode(photo.Span, out string? data));
		Assert.Null(data);
	}
}
