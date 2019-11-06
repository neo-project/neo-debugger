# Neo Smart Contract Debugger for Visual Studio Code

This preview release enables Neo developers to debug their smart contracts
in Visual Studio Code. As of the initial v0.5 release, the following features
are supported.

- Automatically generating initial launch.json file from compiled Neo
  smart contract files (.avm) discovered in the workspace
- Launching and stepping thru a smart contract
- Specifying smart contract entry point parameters in launch.json file
- Visualizing local variables and contract storage while debugging
- Specifying emulated contract storage values in launch.json
- Specifying emulated Runtime.CheckWitness behavior in launch.json

Please note, the Neo Smart Contract Debugger for Visual Studio Code is in early
access preview. There is more work to be done and there are assuredly bugs in the
product. Please let us know of any issues you find via our
[GitHub repo](https://github.com/neo-project/neo-debugger/).

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
