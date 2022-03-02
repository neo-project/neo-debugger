# Neo Smart Contract Debugger

[![](https://github.com/neo-project/neo-debugger/actions/workflows/build-vscode.yml/badge.svg)](https://github.com/neo-project/neo-debugger/actions)
[![](https://vsmarketplacebadge.apphb.com/version-short/ngd-seattle.neo-contract-debug.svg)](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-contract-debug)

The Neo Smart Contract Debugger enables Neo developers to debug their smart contracts
in Visual Studio and Visual Studio Code. It is built on the same [virtual machine](https://github.com/neo-project/neo-vm)
as the [core Neo project](https://github.com/neo-project/neo) to ensure maximum compatibility
between the debugger and how contracts will execute in production.

Neo supports writing smart contracts in a variety of languages. However, the
debugger needs the smart contract complier to emit additional information the
debugger uses to map Neo Virtual Machine instructions back to source code.
The debug information format is [fully documented](https://github.com/ngdseattle/design-notes/blob/master/NDX-DN11%20-%20NEO%20Debug%20Info%20Specification.md#v10-format).
This format is supported by a variety of Neo smart contract compilers including 

* [NCCS (C#)](https://github.com/neo-project/neo-devpack-dotnet)
* [neow3j (Java/Kotlin/Android)](https://neow3j.io)
* [neo-boa (Python)](https://github.com/CityOfZion/neo-boa)
* [NeoGo (GoLang)](https://github.com/nspcc-dev/neo-go)
* [NEO•ONE (TypeScript)](https://neo-one.io)

## Versioning Strategy

As of March 2022, the Neo Smart Contract Debugger project has adopted 
[VS Code recommended guidance](https://code.visualstudio.com/api/working-with-extensions/publishing-extension#prerelease-extensions)
for version numbers. This will allow the VS Code Marketplace to offer production and pre-release
versions of this extension. Developers will be able to choose which version to install and VS Code
will automatically keep the extension up to date.

Going forward, the minor version of this extension will be even for production releases and odd
for preview releases. The first production release under this new versioning strategy will ve
v3.2. The first pre-release of this extension will be v3.3.

> Note, this project uses NerdBank Git Versioning to manage release version numbers.
> As such, patch versions of public releases will typically not be sequential. 

## Installation

The Neo Smart Contract Debugger requires a [.NET runtime](https://dotnet.microsoft.com/download/dotnet)
to be installed. The version of .NET Core needed depends on the version of the Neo
Smart Contract Debugger.

|Neo Smart Contract Debugger Version|.NET Core Version|
|-----------------------------------|-----------------|
| v3.1 | [v6.0](https://dotnet.microsoft.com/download/dotnet/6.0) (for Neo N3 contracts) <br /> [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) (for Neo Legacy Contracts) |
| v3.0 | [v5.0](https://dotnet.microsoft.com/download/dotnet/5.0) (for Neo N3 contracts) <br /> [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) (for Neo Legacy Contracts) |
| v2.0 (unsupported) | [v5.0](https://dotnet.microsoft.com/download/dotnet/5.0) (for Neo N3 contracts) <br /> [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) (for Neo Legacy Contracts) |
| v1.0 | [v3.1](https://dotnet.microsoft.com/download/dotnet/3.1) |
| v0.9 (unsupported) | [v3.0](https://dotnet.microsoft.com/download/dotnet/3.0) |
| v0.5 (unsupported) | [v2.2](https://dotnet.microsoft.com/download/dotnet/2.2) |

### Visual Studio

The Neo Smart Contract Debugger for Visual Studio is currently in preview.
To install it, download a recent release of neodebug-vs-{version}.vsix from
the [GitHub release](https://github.com/neo-project/neo-debugger/releases) page
to your local machine then double click on the file. 

The Neo Smart Contract Debugger for Visual Studio requires Visual Studio 2019
Community, Professional or Enterprise. It has not been tested with Visual
Studio 2022 preview releases. Additionally, The Neo Smart Contract Debugger 
for Visual Studio requires [.NET v5.0](https://dotnet.microsoft.com/download/dotnet/5.0)
in order to debug Neo N3 contracts as described above. Debugging Neo Legacy contracts
is not supported in the Neo Smart Contract Debugger for Visual Studio.

[Additional documentation](docs/visual-studio.md) on using The Neo Smart Contract Debugger 
for Visual Studio is available.

### Visual Studio Code 

The Neo Smart Contract Debugger for Visual Studio Code can be installed via the
[Visual Studio Code Marketplace](https://marketplace.visualstudio.com/vscode). It can be
installed [by itself](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-contract-debug)
or as part of the [Neo Blockchain Toolkit](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-blockchain-toolkit).

The Neo Smart Contract Debugger requires a [.NET runtime](https://dotnet.microsoft.com/download/dotnet-core)
to be installed. The version of .NET Core needed depends on the version of the Neo
Smart Contract Debugger.

As of version 2.0, the Neo Smart Contract Debugger for Visal Studio Code supports both 
[Neo N3 and Neo Legacy](https://medium.com/neo-smart-economy/introducing-neo-n3-the-next-evolution-of-the-neo-blockchain-b2960c4def6e).

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

Thanks for checking out the Neo Smart Contract Debugger!
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
