# Neo Smart Contract Debugger for Visual Studio Code

[![Build Status](https://dev.azure.com/NGDSeattle/Public/_apis/build/status/neo-project.neo-debugger?branchName=master)](https://dev.azure.com/NGDSeattle/Public/_build/latest?definitionId=27&branchName=master)
[![](https://vsmarketplacebadge.apphb.com/version-short/ngd-seattle.neo-contract-debug.svg)](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-contract-debug)

The Neo Smart Contract Debugger enables Neo developers to debug their smart contracts
in Visual Studio Code. It is built on the same [virtual machine](https://github.com/neo-project/neo-vm)
as the [core Neo project](https://github.com/neo-project/neo) to ensure maximum compatibility
between the debugger and how contracts will execute in production.

Please note, the Neo Smart Contract Debugger for Visual Studio Code is in early
access preview. There is more work to be done and there are assuredly bugs in the
product. Please let us know of any issues you find via our
[GitHub repo](https://github.com/neo-project/neo-debugger/).

Docs are somewhat limited at this point. Please see the
[Neo Blockchain Toolkit Quickstart](https://github.com/neo-project/neo-blockchain-toolkit/blob/master/quickstart.md)
for an overview of Neo-Express along with the other tools in the Neo Blockchain
Toolkit. Please review the [Debugger Launch Configuration Reference](docs/debug-config-reference.md)
for information on how to control the execution of contracts within the debugger.

Neo supports writing smart contracts in a variety of languages. However, the
debugger needs the smart contract complier to emit additional information the
debugger uses to map Neo Virtual Machine instructions back to source code.
Currently, there is only one tool that can generate this debugger information -
a fork of the Neo Compiler for .NET. This fork - known as NEON-DE (DE stands for
Debugger Enhancements) is currently only available via a [fork of the Neo DevPack
for .NET repo](https://github.com/ngdseattle/neo-devpack-dotnet/tree/master-de).
We have every intention to move this fork into the official
[Neo DevPack for .NET repo](https://github.com/neo-project/neo-devpack-dotnet),
and to merge the debugger enhancements to the official NEON tool release.

Additionally, we intend to standardize and document the debug information generated
by NEON-DE so that other Neo smart contract compilers such as
[neo-boa](https://github.com/CityOfZion/neo-boa) can generate it. It is an explicit
goal for this debugger to work with any language that can compile Neo smart contracts.

## Installation

The latest released version of the Neo Smart Contract Debugger can be installed via the
[Visual Studio Code Marketplace](https://marketplace.visualstudio.com/vscode). It can be
installed [by itself](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-contract-debug)
or as part of the [Neo Blockchain Toolkit](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-blockchain-toolkit).

The Neo Smart Contract Debugger requires the [.NET Core 3.1 runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1)
to be installed.

> As of v0.10, Neo-Express has snapped to a Long Term Support (LTS) release of
> .NET Core. .NET Core LTS releases are
> [supported for three years](https://github.com/dotnet/core/blob/master/microsoft-support.md#long-term-support-lts-releases).
> The next LTS release of .NET Core isn't projected be released until
> [November 2021](https://github.com/dotnet/core/blob/master/roadmap.md#upcoming-ship-dates),
> so we expect to stay on this version of .NET core for at least two years.

### Install Preview Releases

The Neo Smart Contract Debugger has a public [build server](https://dev.azure.com/NGDSeattle/Public/_build?definitionId=27).
You can install preview builds of the debugger by navigating to the build you wish to install,
pressing the "Artifacts" button in the upper right hand corner and downloading the VSIX-package
artifact. The artifact is a zip file containing the debugger VSIX file, which can be installed
manually. For more information on installing VSIX extensions in VSCode, please see the 
[official VSCode docs](https://code.visualstudio.com/docs/editor/extension-gallery#_install-from-a-vsix).

## A Message from the Engineer

Thanks for checking out the Neo Smart Contract Debugger for VSCode!
I am eager to hear your opinion of the product.

If you like the debugger, please let me know on [Twitter](https://twitter.com/devhawk),
[email](mailto:harrypierson@ngd.neo.org) or the [Neo Discord server](https://discord.gg/G5WEPwC).

If there are things about the debugger you don't like, please file issues in our
[GitHub repo](https://github.com/neo-project/neo-debugger/issues). You can hit me up on
Twitter, Discord or email as well, but GitHub issues are how we track bugs and new
features. Don't be shy - file an issue if there is anything you'd like to see changed
in the product.

Before you get started, I'd just like to point out again that the Neo Smart Contract
Debugger is currently in early preview. Only a handful of features have been implemented
so far. And those that are implemented are  more likely to have bugs in them. So please,
have some patience and provide as much feedback as you can. We also accept pull requests
if you want to get involved!

Most software is built by teams of people. However, the Neo Smart Contract Debugger
so far has been a solo effort. I'm looking forward to having other folks contribute
in the future, but so far it's just been me. That means that the debugger has been
designed around my experiences and my perspective. I can't help it, my perspective
is the only one I have! :) So while I find the debugger intuitive, I realize that
you may not feel the same. Please let me know if this is the case! I didn't build
the Neo Smart Contract Debugger for me, I built it for the Neo developer community
at large. So if there are changes we can make to make it more accessible, intuitive,
easier to use or just flat-out better - I want to hear about them.

Thanks again for checking out the Neo Smart Contract Debugger. I look forward to
hearing from you.

\- Harry Pierson (aka [DevHawk](http://devhawk.net)), Chief Architect NGD Seattle
