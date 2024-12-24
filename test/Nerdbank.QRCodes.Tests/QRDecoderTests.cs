// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class QRDecoderTests(ITestOutputHelper logger) : TestBase
{
	public static readonly string[] SupportedFileTypes = ["bmp", "gif", "png", "jpg"];
	public static readonly object[][] SupportedFileTypesData = SupportedFileTypes.Select(ft => new object[] { ft }).ToArray();

	[Fact]
	public void TryDecode_Span_NoQRCode() => this.AssertQRCode(null, "noQRcode.jpg");

	[Theory, MemberData(nameof(SupportedFileTypesData))]
	public void TryDecode_Span(string extension) => this.AssertQRCode("Hello, World!", $"Generated1.{extension}");

	[Fact]
	public void RealPhoto()
	{
		this.AssertQRCode_Path(
			"zcash:u1sp8wrjmxpxrjex2ytgzr2utm9j30lk5dage2xglyu6way6p94vjurpayn6n26cw5lcjwq3vwjw3azuxhd425wx4vqtvnam7vpvwfs5n783yjlxff7m7c5qc62l4unacw9qw6lrzulqt9pzt5p7qk676z02p9al7alj43duhkvjurt50vzaq0y66ehkxtqfslznvmn9rfasqh25rqvmz?amount=0.1&memo=WmFzaGkgU3RpY2tlcg",
			"realphoto_zcash_payment_request.jpg");
	}

	[Fact]
	public void TryDecode_Path() => this.AssertQRCode_Path("Hello, World!", "Generated1.png");

	[Fact]
	public void TryDecode_Path_NoQRCode() => this.AssertQRCode_Path(null, "noQRcode.jpg");

	private void AssertQRCode_Path(string? expectedText, string imageName)
	{
		Assert.Equal(expectedText is not null, QRDecoder.TryDecode(GetResourceFilePath(imageName), out string? actualText));
		if (actualText is not null)
		{
			logger.WriteLine(actualText);
		}

		Assert.Equal(expectedText, actualText);
	}

	private void AssertQRCode(string? expectedText, string imageName)
	{
		ReadOnlyMemory<byte> photo = GetResourceMemory(imageName);
		Assert.Equal(expectedText is not null, QRDecoder.TryDecode(photo.Span, out string? actualText));
		if (actualText is not null)
		{
			logger.WriteLine(actualText);
		}

		Assert.Equal(expectedText, actualText);
	}
}
