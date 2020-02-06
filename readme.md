# Neo Smart Contract Debugger for Visual Studio Code

[![Build Status](https://dev.azure.com/NGDSeattle/Public/_apis/build/status/neo-project.neo-debugger?branchName=master)](https://dev.azure.com/NGDSeattle/Public/_build/latest?definitionId=27&branchName=master)
[![](https://vsmarketplacebadge.apphb.com/version-short/ngd-seattle.neo-contract-debug.svg)](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-contract-debug)

The Neo Smart Contract Debugger enables Neo developers to debug their smart contracts
in Visual Studio Code. It is built on the same [virtual machine](https://github.com/neo-project/neo-vm)
as the [core Neo project](https://github.com/neo-project/neo) to ensure maximum compatibility
between the debugger and how contracts will execute in production.

Docs are somewhat limited at this point. Please see the
[Neo Blockchain Toolkit Quickstart](https://github.com/neo-project/neo-blockchain-toolkit/blob/master/quickstart.md)
for an overview of Neo Smart Contract Debugger along with the other tools in the Neo Blockchain
Toolkit. Please review the [Debugger Launch Configuration Reference](docs/debug-config-reference.md)
for information on how to control the execution of contracts within the debugger.

Neo supports writing smart contracts in a variety of languages. However, the
debugger needs the smart contract complier to emit additional information the
debugger uses to map Neo Virtual Machine instructions back to source code.
Currently, the only Neo smart contract compiler that supports this debug information
is v2.6 of NEON - the Neo Compiler for .NET and part of the
[Neo Development Pack for .NET](https://github.com/neo-project/neo-devpack-dotnet).
Please see the Quickstart for information on installing the latest version of NEON.

> Note, previous versions of the Neo Smart Contract Debugger depended on a fork of
> NEON known as NEON-DE (DE stands for Debugger Enhancements). As of NEON v2.6,
> all of the features unique to the NEON-DE fork have been merged into the official
> tool. While the Neo Smart Contract Debugger still supports NEON-DE, we recommend
> that you switch over to using the official NEON releases.

Additionally, It is an explicit goal for this debugger to work with any
language that can compile Neo smart contracts. The debug information format has been
[fully documented](https://github.com/ngdseattle/design-notes/blob/master/NDX-DN11%20-%20NEO%20Debug%20Info%20Specification.md#v10-format).
and we are working with other smart contract compilers communities such as
[neo-boa](https://github.com/CityOfZion/neo-boa) to support this format.

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

## Installation

The latest released version of the Neo Smart Contract Debugger can be installed via the
[Visual Studio Code Marketplace](https://marketplace.visualstudio.com/vscode). It can be
installed [by itself](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-contract-debug)
or as part of the [Neo Blockchain Toolkit](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-blockchain-toolkit).

The Neo Smart Contract Debugger requires a [.NET Core runtime](https://dotnet.microsoft.com/download/dotnet-core)
to be installed. The version of .NET Core needed depends on the version of the Neo
Smart Contract Debugger.

|Neo Smart Contract Debugger Version|.NET Core Version|
|-----------------------------------|-----------------|
| v1.0 | [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) |
| v0.9 | [v3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) |
| v0.5 | [v2.2](https://dotnet.microsoft.com/download/dotnet-core/2.2) |

> As of v1.0, the Neo Smart Contract Debugger has snapped to a Long Term Support
> (LTS) release of .NET Core. .NET Core LTS releases are
> [supported for three years](https://github.com/dotnet/core/blob/master/microsoft-support.md#long-term-support-lts-releases).
> The next LTS release of .NET Core isn't projected be released until
> [November 2021](https://github.com/dotnet/core/blob/master/roadmap.md#upcoming-ship-dates),
> so we expect to stay on this version of .NET core for at least two years.

### Ubuntu Installation

Using the checkpoint functionality on Ubuntu 18.04 requires installing libsnappy-dev and libc6-dev via apt-get.

``` shell
> sudo apt install libsnappy-dev libc6-dev -y
```

### MacOS Installation

Using the checkpoint functionality on MacOS requires installing rocksdb via [Homebrew](https://brew.sh/)

``` shell
> brew install rocksdb
```

### Install Preview Releases

The Neo Smart Contract Debugger has a public [build server](https://dev.azure.com/NGDSeattle/Public/_build?definitionId=27).
You can install preview builds of the debugger by navigating to the build you wish to install,
pressing the "Artifacts" button in the upper right hand corner and downloading the VSIX-package
artifact. The artifact is a zip file containing the debugger VSIX file, which can be installed
manually. For more information on installing VSIX extensions in VSCode, please see the 
[official VSCode docs](https://code.visualstudio.com/docs/editor/extension-gallery#_install-from-a-vsix).
