<!-- markdownlint-enable -->
# Neo Legacy Smart Contract Debugger launch.config Reference

> Note, this document is for Neo Legacy smart contract launch configurations (i.e. .AVM files).
> For information about Neo N3 smart contract launch configurations, please see
> [this document](debug-config-reference.md).

The Neo Smart Contract Debugger enables fine grained execution control
via custom configuration settings in [launch.json](https://code.visualstudio.com/Docs/editor/debugging#_launch-configurations).
This document provides information on these settings.

## program

Absolute path to AVM file being debugged.

Examples:

```json
"program": "${workspaceFolder}\\bin\\Debug\\netstandard2.0\\publish\\domain.avm",
```

JSON Schema:

```json
"program": {
    "type": "string"
},
```

## args

Command line arguments passed to the contract entrypoint. JSON strings that are
prefixed with `'0x'` are treated as a hex-encoded byte array. JSON strings that
are prefixed with `'@'` are treated a base-58 encoded address.

Examples:

```json
"args": ["register", ["neo.org", "Harry Pierson"]],

// 0x prefixed strings are treated as hex-encoded byte arrays
"args": ["getTxInfo", ["0xd2cbfbe9bec47318113e4d41c95174023851df74d7cb2a9e4049d5c84d2b2a6d"]],

// '@' prefixed strings are treated as base-58 encoded Neo addresses
"args": ["balanceOf", ["@AXwFY3qdGm6sYn8p59E7ckKWsZwdJyrHdn"]],
```

JSON Schema:

```json
"args": {
    "type": "array",
    "default": []
},
```

## return-types

Specifies the expected return type of the contract entry-point. Particularly useful
when the contract entry-point returns a C# `object`, but the specific operation
being invoked returns a strongly-typed value.

Note, it is possible for Neo smart contracts to have multiple return values.
Smart contracts compiled from C# always have a single return value, but the
configuration property name is plural and the value must be an array.

Examples:

```json
"return-types": ["bool"],

"return-types": ["string"],
```

JSON Schema:

```json
"return-types": {
    "type": "array",
    "items": {
        "type": "string",
        "enum": [
            "int",
            "bool",
            "string",
            "hex",
            "byte[]"
        ]
    }
},
```

## checkpoint

Path to a [Neo-Express](https://github.com/neo-project/neo-express)
[checkpoint](https://github.com/neo-project/neo-express/blob/master/docs/command-reference.md#neo-express-checkpoint)
to use for blockchain data.

Examples:

```json
"checkpoint": "${workspaceFolder}\\checkpoints\\3-mint-tokens-invoked.neo-express-checkpoint",
```

JSON Schema:

```json
"checkpoint": {
    "type": "string"
},
```

## storage

Key/value pairs used to populate debugger's emulated storage. Similar to other
launch configuration settings, strings prefixed with `'0x'` are treated as hex-encoded
byte arrays.

If a specified key already exists in the checkpoint file, the value specified in
the launch configuration takes precedence.

JSON for the `storage` configuration setting can be generated via the Neo-Express
[`contract storage` command](https://github.com/neo-project/neo-express/blob/master/docs/command-reference.md#neo-express-contract-storage)
by using the `--json` argument.

Examples:

```json
"storage": [
    {
        "key": "neo.org",
        "value": "Neo Foundation"
    }
],

"storage": [
    {
        "key": "0x8a6f1e4f13022b26e56e957cb8251b082f0748b1007465737361",
        "value": "0x174876e800"
    },
    {
        "key": "0x796c707075536c61746f740074636172746e6f63",
        "value": "0x174876e800"
    },
    {
        "key": "0xd2cbfbe9bec47318113e4d41c95174023851df74d7cb2a9e4049d5c84d2b2a6d006f666e497874",
        "value": "0x174876e80005028a6f1e4f13022b26e56e957cb8251b082f0748b1140000000380"
    },
],
```

JSON Schema:

```json
"storage": {
    "type": "array",
    "items": {
        "type": "object",
        "required": [
            "key",
            "value"
        ],
        "properties": {
            "key": {
                "type": "string"
            },
            "value": {
                "type": "string"
            },
            "constant": {
                "type": "boolean"
            }
        }
    },
    "default": []
},
```

## sourceFileMap

Optional source file mappings passed to the debug engine

Example:

``` json
"sourceFileMap": {
    "C:\foo": "/home/user/foo"
}
```

JSON Schema:

``` json
"sourceFileMap": {
    "type": "object",
    "additionalProperties": {
        "type": "string"
    }
},
```

## stored-contracts

Optional additional contracts to load for dynamic invoke scenarios. Stored contracts
can include optional emulated storage key/value pairs as described above.

Example:

``` json
"stored-contracts": [
    "${workspaceFolder}/Second/bin/Debug/netstandard2.0/Second.avm",
    {
        "program": "${workspaceFolder}/Third/bin/Debug/netstandard2.0/Third.avm",
        "storage": [
            {
                "key": "neo.org",
                "value": "Neo Foundation"
            }
        ],
    }
],
```

JSON Schema:

``` json
"stored-contracts": {
    "type": "array",
    "description": "",
    "items": {
        "oneOf": [
            {
                "type": "string",
                "description": "Absolute path to AVM file"
            },
            {
                "type": "object",
                "required": [
                    "program"
                ],
                "properties": {
                    "program": {
                        "type": "string",
                    },
                    "storage": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "required": [
                                "key",
                                "value"
                            ],
                            "properties": {
                                "key": {
                                    "type": "string"
                                },
                                "value": {
                                    "type": "string"
                                },
                                "constant": {
                                    "type": "boolean"
                                }
                            }
                        }
                    }
                }
            }
        ]
    }
}

```

## runtime

Specifies behavior of `Runtime.Trigger` and `Runtime.CheckWitness` members.

`Runtime.Trigger` returns either `TriggerType.Verification` or `TriggerType.Application`.
By default, the debugger return `TriggerType.Application` for `Runtime.Trigger`, but
this can be overridden by the `runtime.trigger` configuration property.

`Runtime.CheckWitness` takes a byte array and returns a boolean. The
`runtime.witnesses` configuration property can either accept an array of
hex-encoded byte arrays to compare the method parameter to or an object
with a `check-result` property containing a hard-coded boolean value to
return, regardless of the parameter passed to `Runtime.CheckWitness`.

Examples:

```json
"runtime": {
    "witnesses": {
        "check-result": true
    }
}
```

JSON Schema:

```json
"runtime": {
"type": "object",
"properties": {
"trigger": {
    "type": "string",
    "enum": [
        "verification",
        "application"
    ]
},
"witnesses": {
    "oneOf": [
        {
            "type": "array",
            "items": "string"
        },
        {
            "type": "object",
            "required": [
                "check-result"
            ],
            "properties": {
                "check-result": {
                    "type": "boolean"
                }
            }
        }
    ]
}
```

## utxo

UTXO assets (aka NEO and GAS) to attach to the transaction being debugged. Objects
in the `input` array are compatible with objects in the result.balance.unspent array
of [`getunspents`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getunspents.html)
RPC endpoint. You can retrieve unspents information from Neo-Express via the
[`show unspents` command](https://github.com/neo-project/neo-express/blob/master/docs/command-reference.md#neo-express-show).
The input.value property is permitted for compatibility with `getunspents` JSON
response format but is not used by the debugger.

Examples:

```json
"utxo": {
    "inputs": [
        {
            "txid": "0xf5e388f6a98a133755190f06e4dd3c9d9afd916a1e27b52250f1d54d405086cf",
            "n": 0
        }
    ],
    "outputs": [
        {
            "asset": "neo",
            "value": 1000,
            "address": "0x30f41a14ca6019038b055b585d002b287b5fdd47"
        }
    ]
},
```

JSON Schema:

```json
"utxo": {
    "type": "object",
    "properties": {
        "inputs": {
            "type": "array",
            "required": [
                "txid",
                "n"
            ],
            "properties": {
                "txid": { "type": "string" },
                "n": { "type": "number" },
                "value": { "type": "number" }
            }
        },
        "outputs": {
            "type": "array",
            "required": [
                "asset",
                "value",
                "address"
            ],
            "properties": {
                "asset": { "type": "string" },
                "value": { "type": "number" },
                "address": { "type": "string" }
            }
        }
    }
},
```
