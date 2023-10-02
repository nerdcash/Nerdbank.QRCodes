# Nerdbank.QRCodes

## Features

* Decode a QR code (using the excellent ZXing.Net library, Windows only) from a high quality render or a photo with a QR code somewhere in the image.
* Encode data in a QR code (using the excellent QRCoder library) supporting several graphic formats (some Windows only) and even as ASCII art.

## Usage

### Create a QR code

There are lots of options for customizing QR codes including style, image in the center, colors, and output image format.
Following is a basic example.

```cs
using QRCoder;

QREncoder encoder = new();
QRCodeGenerator generator = new();
QRCodeData data = generator.CreateQrCode("https://some.url", encoder.ECCLevel);
encoder.Encode(data, "some.png", traceSource: null);
```

### Decode a QR code

Decoding a QR code is very straightforward.

```cs
using Bitmap bitmap = (Bitmap)Image.FromFile("some.jpg");
if (QRDecoder.TryDecode(bitmap, out string? data))
{
	Console.WriteLine(data);
}
```

Today this requires running on Windows.
