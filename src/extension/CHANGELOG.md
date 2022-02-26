# Neo Smart Contract Debugger Change Log

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This project uses [NerdBank.GitVersioning](https://github.com/AArnott/Nerdbank.GitVersioning)
to manage version numbers. This tool automatically sets the Semantic Versioning Patch
value based on the [Git height](https://github.com/AArnott/Nerdbank.GitVersioning#what-is-git-height)
of the commit that generated the build. As such, released versions of this extension
will not have contiguous patch numbers. Initial major and minor releases will be documented
in this file without a patch number. Patch version will be included for bug fix releases, but
may not exactly match a publicly released version.

## [Unreleased]

## Changed

* Only override CheckWitness when specified via `runtime.witnesses` launch config property.

## Fixed

* Respect DebugInfo.SlotVariable.Index

## Engineering

* Adopting [VS Code recommendations](https://code.visualstudio.com/api/working-with-extensions/publishing-extension#prerelease-extensions)
  for version numbers to enable shipping pre-release versions of the debugger. 

## [3.1.23] - 2021-12-14

### Changed
* Update dependencies for Neo 3.1.0 release (#149)

## [3.0.6] - 2021-10-12

### Changed

* Update dependencies 
  * Microsoft.VisualStudio.Shared.VsCodeDebugProtocol 17.0.50801.1
  * Neo 3.0.3 
  * Neo.BlockchainToolkit.Library 3.0.13
  * Nerdbank.GitVersioning 3.4.240
  * Nito.Disposables 2.2.1

## [3.0.3] - 2021-08-06

### Changed

* Update dependencies for Neo 3.0.2 release

## [3.0] - 2021-08-02

### Changed

* Neo N3 release support
* Bumped major version to 3 for consistency with Neo N3 release
* Update dependencies

## [2.0.18] - 2021-07-21

> Note, this is a preview release even though it does not carry a "-preview" version. VSCode Marketplace does not support [semantic versioning](https://semver.org/) pre-release identifiers.

### Changed

* Neo N3 RC4 support
* Update Debug Adapter Protocol initialization code  (#140)
* update dependencies (#144)
* Convert map key to hex string if GetString fails in MapToJson (#134, fixes #132)
* improve error messages during launch config parsing (#143)
* better sequence point handling in disassembly manager (#143)

### Added

* Show Gas Consumed during debugging (#136, fixes #135)
* Load debug info for stored-contracts array members (#138, fixes #131)

## [2.0.18] - 2021-05-04

> Note, this is a preview release even though it does not carry a "-preview" version. VSCode Marketplace does not support [semantic versioning](https://semver.org/) pre-release identifiers.

### Added

* Neo N3 RC3 support

## [2.0.15] - 2021-05-17

> Note, this is a preview release even though it does not carry a "-preview" version. VSCode Marketplace does not support [semantic versioning](https://semver.org/) pre-release identifiers.

### Changed

* rework disassembly generation (#112)
* workaround devpack #610 (#113)
* Rework variables/evaulation (#115)
* read address version from trace file (#118)

## [2.0.7] - 2021-05-04

> Note, this is a preview release even though it does not carry a "-preview" version. VSCode Marketplace does not support [semantic versioning](https://semver.org/) pre-release identifiers.

### Added

* Neo N3 RC2 support

### Changed

* Update Disassembly view

## [2.0.5] - 2021-05-03

> Note, this is a preview release even though it does not carry a "-preview" version. VSCode Marketplace does not support [semantic versioning](https://semver.org/) pre-release identifiers.

### Changed

* re-enable ParseStorage on adapter 3
* ensure storage key hash code is 8 characters long

## [2.0.3] - 2021-03-23

> Note, this is a preview release even though it does not carry a "-preview" version. VSCode Marketplace does not support [semantic versioning](https://semver.org/) pre-release identifiers.

### Added

* Neo N3 RC1 support

## [1.2.58-preview] - 2021-02-08

### Added

* Neo 3 Preview 5 support

## [1.2.58-preview] - 2020-12-28

### Added

* Neo 3 Preview 4 support

## Changes

* Improve Nep17 debug experience (#84)
* Adapt to Script Hash Identification Change (#82)
* Oracle Response Debugging (#80)
* Use extensionMode to detect Development Mode (#79)
* update contract parameter parser
* include event name in notify output
* allow string signers
* forward slash createConfig
* update debugger CheckWitness support
* update config snippets
* Add invoke-file and signers support to neo 3 launch configuration (#76)
* TryGetNativeContract during trace debugging (#75)

## [1.2.28-preview] - 2020-08-18

### Added

* Trace Debug Support


## [1.2.25-preview] - 2020-08-03

### Added

* Neo 3 Preview 3 support

## [1.2.10-preview] - 2020-06-22

### Added

- Neo 3 Preview 2 support

## [1.1] - 2020-05-28

### Added

- Disassembly view
- Cross Contract Support
- Specify folder mappings to support debugging contracts compiled on machines
  with different folder layouts

### Changed

- Folded library project into adapter project.
- Passing --debug to the debug adapter now pauses until debugger attached instead
  of launching the JIT Debugger. JIT Debugger approach only works on Windows.

### Fixed

- Adding item with the non-unique key to emulated storage [#20](https://github.com/neo-project/neo-debugger/issues/20)
- Improve document path resolution [#24](https://github.com/neo-project/neo-debugger/issues/24)

## [1.0] - 2020-02-06

### Added

- Added support for [.avmdbgnfo format](https://github.com/ngdseattle/design-notes/blob/master/NDX-DN11%20-%20NEO%20Debug%20Info%20Specification.md#v010-format)

### Changed

- Updated debug adapter to .NET Core 3.1
- Bumped version number for official release of Neo Blockchain Toolkit 1.0

## [0.9] - 2019-11-06

### Added

- Blockchain data provided by Neo-Express checkpoint
- VSCode Watch window support for Neo contract variables and storage items
- Basic type casting support for watch window expressions 
- Debug variable support for domain model stack items like Block and Transaction
- UTXO configuration parameters

### Changed

- Refactored NeoDebug Adapter to seperate common debug functionally from isolated debug
  adapter implementation.
- Updated debug adapter to .NET Core 3.0 and C# 8 (with nullable types enabled)
- Updated Neo branding as per https://neo.org/presskit

## [0.5] - 2019-09-06

Initial Release
