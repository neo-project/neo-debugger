# Neo Smart Contract Debugger for Visual Studio Code

[![Build Status](https://dev.azure.com/ngdenterprise/Build/_apis/build/status/neo-project.neo-debugger?branchName=master)](https://dev.azure.com/ngdenterprise/Build/_build/latest?definitionId=4&branchName=master)
[![](https://vsmarketplacebadge.apphb.com/version-short/ngd-seattle.neo-contract-debug.svg)](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-contract-debug)


The Neo Smart Contract Debugger enables Neo developers to debug their smart contracts
in Visual Studio Code. It is built on the same [virtual machine](https://github.com/neo-project/neo-vm)
as the [core Neo project](https://github.com/neo-project/neo) to ensure maximum compatibility
between the debugger and how contracts will execute in production.

Neo supports writing smart contracts in a variety of languages. However, the
debugger needs the smart contract complier to emit additional information the
debugger uses to map Neo Virtual Machine instructions back to source code.
The debug information format is [fully documented](https://github.com/ngdseattle/design-notes/blob/master/NDX-DN11%20-%20NEO%20Debug%20Info%20Specification.md#v10-format).
This format is supported by a variety of Neo smart contract compilers including 

* [NEON (C#)](https://github.com/neo-project/neo-devpack-dotnet)
* [neo-boa (Python)](https://github.com/CityOfZion/neo-boa)
* [NeoGo (GoLang)](https://github.com/nspcc-dev/neo-go)
* [NEO•ONE (TypeScript)](https://neo-one.io/)

As of version 2.0, the Neo Smart Contract Debugger supports both 
[Neo N3 and Neo Legacy](https://medium.com/neo-smart-economy/introducing-neo-n3-the-next-evolution-of-the-neo-blockchain-b2960c4def6e).

## Installation

The Neo Smart Contract Debugger can be installed via the
[Visual Studio Code Marketplace](https://marketplace.visualstudio.com/vscode). It can be
installed [by itself](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-contract-debug)
or as part of the [Neo Blockchain Toolkit](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-blockchain-toolkit).

The Neo Smart Contract Debugger requires a [.NET runtime](https://dotnet.microsoft.com/download/dotnet-core)
to be installed. The version of .NET Core needed depends on the version of the Neo
Smart Contract Debugger.

|Neo Smart Contract Debugger Version|.NET Core Version|
|-----------------------------------|-----------------|
| v2.0 | [v5.0](https://dotnet.microsoft.com/download/dotnet/5.0) (for Neo N3 contracts) <br /> [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) (for Neo Legacy Contracts) |
| v1.0 | [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) |
| v0.9 | [v3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) |
| v0.5 | [v2.2](https://dotnet.microsoft.com/download/dotnet-core/2.2) |

### Ubuntu Installation

Using the checkpoint functionality on Ubuntu requires installing libsnappy-dev and libc6-dev via apt-get.

``` shell
> sudo apt install libsnappy-dev libc6-dev -y
```

### MacOS Installation

Using the checkpoint functionality on MacOS requires installing rocksdb via [Homebrew](https://brew.sh/)

``` shell
> brew install rocksdb
```

### Install Preview Releases

The Neo Smart Contract Debugger has a public [build server](https://dev.azure.com/ngdenterprise/Build/_build?definitionId=4&_a=summary).
You can install preview builds of the debugger by navigating to the build you wish to install,
pressing the "Artifacts" button in the upper right hand corner and downloading the VSIX-package
artifact. The artifact is a zip file containing the debugger VSIX file, which can be installed
manually. For more information on installing VSIX extensions in VSCode, please see the 
[official VSCode docs](https://code.visualstudio.com/docs/editor/extension-gallery#_install-from-a-vsix).

## A Message from the Engineer

Thanks for checking out the Neo Smart Contract Debugger for VSCode!
I am eager to hear your opinion of the product.

If you like the debugger, please let me know on [Twitter](https://twitter.com/devhawk),
[email](mailto:harry@ngdenterprise.com) or the [Neo Discord server](https://discord.gg/G5WEPwC).

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

\- Harry Pierson (aka [DevHawk](http://devhawk.net)), Chief Architect ngd enterprise
