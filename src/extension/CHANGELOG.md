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

## [0.9] - Unreleased

### Added

- Blockchain data provided by Neo-Express checkpoint
- VSCode Watch window support for Neo contract variables and storage items
- Basic type casting support for watch window expressions 
- Debug variable support for domain model stack items like Block and Transaction
- UTXO configuration parameters

### Changed

- Refactored NeoDebug Adapter to seperate common debug functionally from isolated debug
  adapter implementation.
- Updated debug adapter to netcore 3.0 and C# 8 (with nullable types enabled)
- Updated Neo branding as per https://neo.org/presskit

## [0.5] - 2019-09-06

Initial Release
