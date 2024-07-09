// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class QRDecoderTests : TestBase
{
	public static readonly string[] SupportedFileTypes = new[] { "bmp", "gif", "png", "jpg" };
	public static readonly object[][] SupportedFileTypesData = SupportedFileTypes.Select(ft => new object[] { ft }).ToArray();

	[Fact]
	public void TryDecode_Span_NoQRCode() => AssertQRCode(null, "noQRcode.jpg");

	[Theory, MemberData(nameof(SupportedFileTypesData))]
	public void TryDecode_Span(string extension) => AssertQRCode("Hello, World!", $"Generated1.{extension}");

	[Fact]
	public void TryDecode_Path()
	{
		string filePath = GetResourceFilePath("Generated1.png");
		Assert.True(QRDecoder.TryDecode(filePath, out string? actualText));
		Assert.Equal("Hello, World!", actualText);
	}

	[Fact]
	public void TryDecode_Path_NoQRCode()
	{
		string filePath = GetResourceFilePath("noQRcode.jpg");
		Assert.False(QRDecoder.TryDecode(filePath, out string? actualText));
		Assert.Null(actualText);
	}

	private static void AssertQRCode(string? expectedText, string imageName)
	{
		ReadOnlyMemory<byte> photo = GetResourceMemory(imageName);
		Assert.Equal(expectedText is not null, QRDecoder.TryDecode(photo.Span, out string? actualText));
		Assert.Equal(expectedText, actualText);
	}
}
