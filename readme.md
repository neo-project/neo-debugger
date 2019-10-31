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
a fork of the Neo Compiler for .NET. This fork - known as NeoN-DE (DE stands for
Debugger Enhancements) is currently only available via a [fork of the Neo DevPack
for .NET repo](https://github.com/devhawk/neo-devpack-dotnet/tree/dehvawk/master2/Neo.Compiler.MSIL).
We have every intention to move this fork into the official
[Neo DevPack for .NET repo](https://github.com/neo-project/neo-devpack-dotnet),
and to merge the debugger enhancements to the official NeoN tool release.

Additionally, we intend to standardize and document the debug information generated
by NeoN-DE so that other Neo smart contract compilers such as
[neo-boa](https://github.com/CityOfZion/neo-boa) can generate it. It is an explicit
goal for this debugger to work with any language that can compile Neo smart contracts.

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
