// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class QRDecoderTests : TestBase
{
	[Fact]
	public void TryDecode_Span_NoQRCode() => AssertQRCode(null, "noQRcode.jpg");

	[Theory]
	[InlineData("bmp")]
	[InlineData("gif")]
	[InlineData("png")]
	[InlineData("jpg")]
	public void TryDecode_Span(string extension) => AssertQRCode("Hello, World!", $"Generated1.{extension}");

	private static void AssertQRCode(string? expectedText, string imageName)
	{
		ReadOnlyMemory<byte> photo = GetResourceMemory(imageName);
		Assert.Equal(expectedText is not null, QRDecoder.TryDecode(photo.Span, out string? actualText));
		Assert.Equal(expectedText, actualText);
	}
}
