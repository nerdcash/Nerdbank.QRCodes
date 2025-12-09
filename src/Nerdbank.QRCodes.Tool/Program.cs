// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Nerdbank.QRCodes.Tool;

RootCommand rootCommand = new("Offers QR code creation or reading functions.")
{
	EncodeCommand.CreateCommand(),
	DecodeCommand.CreateCommand(),
};

return rootCommand.Parse(args).Invoke();
