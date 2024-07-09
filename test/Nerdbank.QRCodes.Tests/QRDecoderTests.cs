// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class QRDecoderTests : TestBase
{
	[Fact]
	public async Task TryDecode_Span_NoQRCode()
	{
		using Stream photo = GetResource("noQRcode.jpg");
		using MemoryStream ms = new();
		await photo.CopyToAsync(ms);
		Assert.False(QRDecoder.TryDecode(ms.GetBuffer().AsSpan(0, (int)ms.Length), out string? data));
		Assert.Null(data);
	}
}
