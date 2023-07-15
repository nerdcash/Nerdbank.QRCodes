// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Nerdbank.QRCodes.Tool;

RootCommand rootCommand = new("Offers QR code creation or reading functions.")
{
	EncodeCommand.CreateCommand(),
	DecodeCommand.CreateCommand(),
};

Parser parser = new CommandLineBuilder(rootCommand)
	.UseDefaults()
	.Build();

await parser.InvokeAsync(args);
