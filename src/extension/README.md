# Neo Smart Contract Debugger for Visual Studio Code

The Neo Smart Contract Debugger enables Neo developers to debug their smart contracts
in Visual Studio Code. It is built on the same [virtual machine](https://github.com/neo-project/neo-vm)
as the [core Neo project](https://github.com/neo-project/neo) to ensure maximum compatibility
between the debugger and how contracts will execute in production.

Please see the
[Neo Blockchain Toolkit Quickstart](https://github.com/neo-project/neo-blockchain-toolkit/blob/master/quickstart.md)
for an overview of Neo Smart Contract Debugger along with the other tools in the Neo Blockchain
Toolkit. Please review the
[Debugger Launch Configuration Reference](https://github.com/neo-project/neo-debugger/blob/master/docs/debug-config-reference.md)
for information on how to control the execution of contracts within the debugger.

The Neo blockchain supports writing smart contracts in a variety of languages.
However, the debugger needs the smart contract complier to emit additional information
the debugger uses to map Neo Virtual Machine instructions back to source code.
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

## Installation

The Neo Smart Contract Debugger requires the [.NET Core 3.1 runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1)
to be installed.

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