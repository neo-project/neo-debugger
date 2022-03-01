# Neo Smart Contract Debugger Storage Schema

Version 3.3 of the Neo Smart Contract Debugger adds support for decoding the key/item byte streams
in contract storage into higher-order types. This makes it easier for developers to understand what
is happening inside their contracts.

> Note, Storage Schema is currently in preview. If you'd like to try out this functionality today,
  install the [pre-release version](https://code.visualstudio.com/updates/v1_63#_pre-release-extensions)
  of the Neo Smart Contract Debugger extension. 

You can see an example of how schematized storage looks in the debugger via this screenshot:

![Storage Schema Screenshot](StorageSchemaScreenshot.png)

This screenshot comes from the [Neo Contributor NFT sample](https://github.com/ngdenterprise/neo-contrib-token)
which has been updated to enable Storage Schema. In particular, note the following:

* Single value storages are displayed in the debugger as a simple variable name + value pair. 
  For example, notice that the `TotalSupply` storage contains a single integer value 3.
* Storage map storages are displayed in the debugger as a collection. Storage map storages have one
  or more key segments with name and type information that the debugger can display to the user.
  * For single segment keys, the segment value is used as the name of the variable. For example, 
    notice how the `Token` storage uses the token ID - a hex-encoded 256 bit hash code - as the
    variable name under the top level `Token` item.
  * For multi segment keys, the key/item pairs are displayed as a collection, with key and item
    children. The key item has a child for each segment in the key, displaying that segment's name
    and value. For example, notice how the `AccountToken` storage has three key/item pairs. The
    first key/item pair has been expanded to show the key segments - `owner` and `tokenId`.
* Storage items can be primitive values such as integers and hash codes. They can also be composite
  types such as structures, arrays and maps. For example, notice how the `Token` storage values are
  `TokenState` instances, with fields such as `Owner` and `Name`.
* Storage Schema includes a primitive `Address` type. For example, notice that the `ContractOwner`
  storage item is the Neo address of the account that deployed the contract.

## Neo Contract Storage

In order to understand how Storage Schema is specified, it is useful to understand the underlying
model of Neo contract storage. If you are an experienced Neo contract developer, you may wish to
skim or skip this section.

Neo contract storage is a key/value byte array store. Each key and value is stored as a raw array
of bytes. Any additional structure of the key or value is provided by code and is unavailable via
the storage engine.

Neo contracts typically use hard coded key prefixes to group different types of data together.
Multi-byte prefixes are also supported, but are typically only needed for contacts with more than
255 prefixes. Otherwise multi-byte prefixes are just extra storage (and associated GAS cost) with
little additional value.

> Note, there are a variety of Neo N3 samples that use strings for storage prefixes. Such samples
  should not be considered best practice. Minimizing prefix length is considered the best practice
  for Neo contract storage.

As an example, the Neo Contributor NFT sample stores six different groups of data, each with its own
[unique single byte prefix](https://github.com/ngdenterprise/neo-contrib-token/blob/main/token-contract/NeoContributorToken.cs#L32). 

``` cs
const byte Prefix_TotalSupply = 0x00;
const byte Prefix_Balance = 0x01;
const byte Prefix_TokenId = 0x02;
const byte Prefix_Token = 0x03;
const byte Prefix_AccountToken = 0x04;
const byte Prefix_ContractOwner = 0xFF;
```

Some groups of data are only a single storage value. As an example, in the NFT sample there is a
single `TotalSupply` and `ContractOwner` value. In those cases the prefix is used directly as the
storage key. You can see this in the NFT Sample `UpdateTotalSupply` method where `Prefix_TotalSupply`
is used to construct the key used to read and write a single integer value. 

``` cs
StorageContext context = Storage.CurrentContext;
byte[] key = new byte[] { Prefix_TotalSupply };

BigInteger totalSupply = (BigInteger)Storage.Get(context, key);
Storage.Put(context, key, totalSupply + 1);
```

Of course, contracts often need to store multiple pieces of data in a single group. In these cases
each data item has its own unique key. The data item's key is pre-pended with the associated hard
coded prefix in order to group all related data together in storage. The pre-pended prefix also serves
to avoid key collisions, since the the key prefix is unique to the group. Neo contract languages
typically include helper classes to make it easy to manage multiple key/value pairs within a single
prefixed group. For example, the C# Smart Contract Framework provides the `StorageMap` to simplify the
management of related data under a common key prefix.

As an example, the NFT sample stores information about multiple minted NFTs. Each NFT has a unique
256 bit hash used as the token ID. Each token has additional information (such as owner and name)
stored in contract storage, keyed by the token ID. The hard coded `Prefix_Token` byte is used to 
group all token key/values together and a `StorageMap` is used to read and write NFT information.

``` cs
StorageMap tokenMap = new StorageMap(Storage.CurrentContext, Prefix_Token);
var tokenData = tokenMap.Get(tokenId); // tokenId provided as method parameter 
TokenState token = (TokenState)StdLib.Deserialize(tokenData);
token.Owner = newOwner; // newOwner provided as method parameter 
tokenMap.Put(tokenId, StdLib.Serialize(token));
```

`StorageMap`'s Get and Put methods automatically pre-pend the prefix provided to the constructor
when reading and writing to contract storage. This makes it easy for developers to be consistent
in their use of storage prefixes.

> Note, `StorageMap` also supports indexer syntax (i.e. `tokenMap[tokenId]`) for reading and
  writing to contract storage. Many sample contacts uses this syntax rather than Get/Put.

Storage keys may also be generated from multiple individual values. As an example, the `AccountToken`
prefix in the NFT sample is used to index all the tokens owned by a given address. For this
storage group, the item key is the owner address + the token ID. The StorageMap then pre-pends
the group prefix as it does for single key items as shown above.

``` cs
StorageMap accountMap = new(Storage.CurrentContext, Prefix_AccountToken);
// combine the owner and the token ID to generate the un-prefixed key
ByteString key = owner + tokenId;
accountMap.Put(key, 0);
```

By structuring the `AccountToken` key this way, the `StorageMap` can return a list of token IDs
owned by a single address using just a couple lines of code:

``` cs
StorageMap accountMap = new StorageMap(Storage.CurrentContext, Prefix_AccountToken);
return accountMap.Find(owner, FindOptions.KeysOnly | FindOptions.RemovePrefix);
```

The `StorageMap` here is constructed just as it was for the token owner update code above.
However, instead of using the `Get` method, this sample code uses the `Find` method, returning 
a list of all the key/value pairs starting with prefix (specified via constructor parameter) and
owner (specified via method parameter). The list returned by Find is further refined by specifying
the `KeysOnly` and `RemovePrefix` options. Note, Find removes the entire prefix it used for the 
search (i.e. `Prefix_AccountToken` and `owner` in this case)

## Neo Storage Schema 

With the basics of Neo Contract Storage described, let's cover the value added by Storage Schema.
As stated above, the underlying storage engine provides no information about the structure of
stored keys or values. In order to provide the view of contract storage in the screen shot above,
the debugger needs additional information about how the contract stores data. In many ways, Storage
Schema is similar to the [debug info](https://github.com/ngdenterprise/design-notes/blob/master/NDX-DN11%20-%20NEO%20Debug%20Info%20Specification.md)
generated by the compiler, but for storage items rather than NeoVM stack items.

> Note: the expectation is that eventually Storage Schemas will be automatically generated from source
  at compile time and included in the contract manifest or debug info. However, as of the initial
  Storage Schema preview no Neo compiler has been updated to generate Storage Schema. As such, Storage
  Schemas must be hand authored at this time. 

Like Neo [contract manifests](https://github.com/neo-project/proposals/blob/master/nep-15.mediawiki)
and debug info, Storage Schema is stored in a JSON format. This will make it easier to integrate
Storage Schema into existing Neo tooling. This section describes both the conceptual model of Storage
Schema as well as how it is encoded in JSON.

> Note: the Storage Schema format has been designed for hand authoring, due to the lack to Storage Schema
  tooling at this time. The Storage Schema format will likely evolve and may have breaking changes
  before it exits preview and tooling comes online.

Since Storage Schemas currently have to be hand authored today, it's not feasible to include them in
contract manifest or debug info files. As such, the debugger looks for Storage Schema information in
a file named `storage-schema.json`. The debugger look for the `storage-schema.json` file in the same
folder as the `.nef` file specified by the 
[`program` property](https://github.com/neo-project/neo-debugger/blob/master/docs/debug-config-reference.md#program)
of the launch configuration. However, since the `.nef` file location is typically not included in source
control, that's not a convenient place to store the `storage-schema.json` file. If the `storage-schema.json`
file is not found in the folder along side the `.nef` file, the debugger will search parent folders
until it reaches the root of the workspace or the root of the drive.

> Note, for projects (like NFT Sample) that have multiple contracts, it's important *NOT* to store
  `storage-schema.json` files in a folder that parents more than one contract. Currently, there's no
  information in the Storage Schema file to indicate what contract it goes with. Since the debugger
  walks up the file system looking for a `storage-schema.json` file, it could accidentally load the
  wrong Storage Schema file. This won't corrupt data in the blockchain or checkpoint, but it will lead
  to incorrect information being displayed in the debugger storage view.

### StorageDef

A contract's Storage Schema contains zero or more StorageDefs (aka Storage Definitions). Each
StorageDef describes the structure of the key and the type of the value associated with a given prefix.

A StorageDef consists of the following.

* Name: string
* Key Prefix: One or more bytes
* Key Segments: Zero or more (name + primitive type name) pairs
* Value type: contact type name

For StorageDefs with multiple key segments, Storage Schema requires that all key segments except the
last be fixed size types. If a variable length type such as `String` or `Integer` is used as a key
segment, the debugger can't determine where one key segment ends and the next begins.

> Note, the requirement for fixed size key segments is only a limitation of Storage Schema. Contract
  storage and helper classes like StorageMap support variable length key segments without issue. 
 
StorageDefs are encoded in JSON using this format:

> Note, Storage Schema definitions in this document are specified in TypeScript. However, there is no 
  requirement that TypeScript be used to generate or consume Storage Schemas. TypeScript is merely a 
  convenient syntax for specifying JSON document structure. 

``` ts
interface StorageSchema {
    storage: { [key: string]: StorageDef }
    // Note, FieldDef are described later in the document
    struct: { [key: string]: FieldDef[] }
}

interface KeySegment {
    key: string,
    // type information described later in document
    type: PrimitiveType
}

interface StorageDef {
    key: {
        // note, prefix number values must be <= 255 (i.e. bytes)
        //       string prefix values will be UTF8 encoded
        prefix: number | Array<number> | string,
        segments?: KeySegment | KeySegment[]
    },
    // type information described later in document
    value: ContractType
}
```

Example:

``` json
"storage": {
    // storage object property names are used as StorageDef names
    "TotalSupply": {
        // all storage defs must have key and value properties
        "key": {
            // storage def key object must have a prefix property
            // key object prefix can be a single integer value 
            "prefix": 0 
            // storage def key object segments property is optional 
        },
        // storage def value property is string encoded type information
        // type information + encoding described later in document
        "value": "Integer"
    },
    "Token": {
        "key": {
            // key object prefix can be an array of integer values
            "prefix": [ 3 ], 
            // key object segments property can be specified as a single KeySegment object 
            "segments": {
                // KeySegment objects must have name and type properties
                "name": "tokenId",
                // KeySegment types must be primitive types (described later in document)
                "type": "Hash256"
            }
        },
        "value": "TokenState"
    },
    "AccountToken": {
        "key": {
            // key object prefix can be a string, which is encoded as a UTF-8 byte array
            "prefix": "account_token", 
            // segments property can be specified as an array of KeySegment objects
            "segments": [
                {
                    "name": "owner",
                    "type": "Address"
                },
                {
                    "name": "tokenId",
                    "type": "Hash256"
                }
            ]
        },
        "value": "Integer"
    },
}
```

### StructDef

A contract's Storage Schema contains zero or more StructDefs (aka Structure Definitions). A struct
describes a named heterogeneous collection of fields, where each field has a name and a type. They
are often used in contracts to store information about a given construct, such as an NFT token. As
an example, the NFT stores a `TokenState` instance in contract storage, keyed by the token's 256
bit hash code ID. The NFT sample defines the `TokenState` struct like this:

``` cs
public class TokenState
{
    public UInt160 Owner = UInt160.Zero;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string Image = string.Empty;
}
```

Structs can only be used as StorageDef values. StorageDef key segments *must* be primitive
types. Structs cannot be stored in contract storage directly, they must be serialized (typically via
`StdLib.Serialize`) when being stored and deserialized (typically via `StdLib.Deserialize`) when
read. By including StructDefs in the StorageSchema, the debugger can automatically deserialize
structures stored in contract storage and display the individual fields in the variables tree view.

A StructDef consists of the following:

* Name: string
  * Note, struct names may not contain hash ('#') or angle bracket ('&lt;' and '&gt;') characters.
* Fields: One or more (name + type name) pairs

StructDefs are encoded in JSON using the following format:

``` ts
interface StorageSchema {
    // Note, StorageDef described earlier in the document
    storage: { [key: string]: StorageDef }
    struct: { [key: string]: FieldDef[] }
}

interface FieldDef {
    name: string,
    // type information described later in document
    type: ContractType
}
```

Example:

```json
"struct": {
    // struct object property names are used as StructDef names
    // structDef names cannot contain '#', '<' or '>' characters
    "TokenState": [
        {
            "name": "Owner",
            "type": "Address"
        },
        {
            "name": "Name",
            "type": "String"
        },
        {
            "name": "Description",
            "type": "String"
        },
        {
            "name": "Image",
            "type": "String"
        }
    ]
}
```

### Unified Neo Contract Type Model

As stated above, Storage Schema objects reference information about Contract Types. Contract Types
is a new richer model for describing type information than has been used in the debugger previously.
This section describes this model (along with providing a road map for upcoming improvements).

> Note, the Contract Type Model only describe information about NeoVM and storage items in order to 
  provide a better debugger experience. This model does not modify the behavior of NeoVM or how
  contract storage works in any way.

Neo compilers generate debug info that is used by the debugger. This primarily consists of information
that the NeoVM doesn't need when executing contracts. For example, the NeoVM does not need to know
the names of method parameters and variables. However, it's easier for the developer if the debugger
can automatically map NeoVM stack items back to the associated variables defined by the developer.
The debug information generated by the compiler enables this mapping.

Debug info also contains information about a variable's type. This can be used in cases to modify how
a given variable is displayed in the debugger. For example, a string variable is stored as an
immutable byte array by NeoVM. By including variable type information in the debug info, the debugger
can convert the underling NeoVM stack item into a display format more closely aligned to the code written
by the developer. For example, the immutable byte array stored by NeoVM can be converted to a string
value by the debugger.

Unfortunately, the original type model used by the debugger today was not rich enough to capture the
details needed to provide the schematized storage view shown in the screen shot above. As such, a new 
richer model for contract type information was developed. Eventually, this new contract type model will
also be used to provide richer type information for runtime items in addition to storage items.

> Note: C# declarations for the new Contract Type Model are available in the 
  [Blockchain Toolkit Library project](https://github.com/ngdenterprise/neo-blockchaintoolkit-library/blob/master/src/bctklib/models/ContractTypes.cs)

#### Primitive Type

NeoVM supports three primitive types: booleans, arbitrary sized integers and immutable byte arrays.
While booleans and integers instances have clear debugger representations, byte arrays often
represent some type of higher level type, such as a 256 bit hash, a Neo address or a string.

The Contract Type model defines the following primitive types. These types can be directly represented
as byte arrays and thus can be stored in contract storage directly without serialization

* Boolean
* Integer
* ByteArray
* String
* Hash160
* Hash256
* PublicKey
* Signature
* Address

Most of these types should be familiar to Neo contract developers. Many of these primitive types overlap
with [ContractParameterType](https://github.com/neo-project/neo/blob/master/src/neo/SmartContract/ContractParameterType.cs)
values.

One primitive type to note is the `Address` type. Under the hood, `Address` is a 160 bit hash code, just as 
`Hash160` is. However, `Address` is rendered in the debugger UI using the standard Neo address encoding, leading
to values such as `NaTtKdE8nt1E9FKKhH6hScXmDGPjgjpdhi` instead of hex encoded byte arrays. Given the prevalence
of Neo addresses in contract code, it made sense to include a specific primitive type to represent addresses and
for them to render in the most developer friendly manner possible.

One other thing to note about `Address`: Some contracts use an all-zero 160 bit hash code to represent "no
address". As an example, the NFT sample uses the all-zero address to represent an NFT without at owner. Since
the all-zero address value typically has special meaning, the debugger displays the all-zero address value in
a special way to make it easy to identify. You can see an example of this in the `TokenState.Owner` value in
the screen shot from the start of this document. The `Erik Zhang` token has `N000000000000000000000000000000000`
for the owner value. Neo addresses are base58 encoded and zero (i.e. `0`) is not a valid address character. So
by encoding what is typically an invalid address this way, it is easy to identify without risk of colliding with
a valid Neo address.

When string encoding a primitive type, the name of the primitive type is used directly. If there is a conflict
with a user defined struct name, a '#' character can be used as a prefix to disambiguate. For example, `Address`
could be a struct type defined by a contract, but `#Address` always indicates the primitive address type. If there
is no name collision, the hash character is optional. 

#### Struct Type

As described in the StructDef section above, a struct is a named heterogeneous collection of fields. Field types
may be of any type described in this type model. In order to avoid collisions with other types, struct names 
may not contain characters (to avoid collisions with primitive types) or angle brackets (to avoid collisions
with Array and Map types below).

Structs are string encoded simply by their name. As an example, see the StorageDef example above where the 
`Token` storage value type is the `TokenState` StructDef.

#### Array&lt;T> Type

> Note, while Array&lt;T> types can be specified in Storage Schema files, they are currently not handled by
  the debugger and will be displayed as if they have Unspecified type. Display of Array&lt;T>  types will
  be implemented in a future version of the debugger.

An Array&lt;T> is a homogeneous collection of items. While the underlying NeoVM Array stack type is heterogenous,
it is common for developers to consistently store a given type in a given array. The C# Smart Contract
Framework even provides a generic List&lt;T> type for homogeneous collections.

When string encoding an Array type, the type of the homogeneous collection is specified between the brackets.
The value between the brackets can be any type of Contract Type, including primitives, unspecified, structs or
even other generic array and map types.

#### Map<K,V> Type

> Note, while `Map<K,V>` types can be specified in Storage Schema files, they are currently not handled by
  the debugger and will be displayed as if they have Unspecified type. Display of `Map<K,V>` types will
  be implemented in a future version of the debugger

A Map<K, V> is a dictionary that maps a key of type K to a value of type V. This is very similar conceptually
to how contact storage works. However, please note that contract storage and Maps are fundamentally different.
A StorageDef cannot be described as a Map<K, V>. A Map<K, V> can be serialized into contract storage, but the
Map keys cannot be iterated or inspected while stored as contract storage keys can.

When string encoding a map type, the key and value types of the map are stored between the brackets and separated
by a comma. The key type of a Map<K,V> *MUST* be a primitive type. The value type can be any type of Contract type
including primitives, unspecified, structs or even other generic array and map types.

#### Unspecified Type

There are times where type information is unspecified or cannot be calculated. For those case, the `Unspecified`
type can be used. Like primitive types, the Unspecified type is simply the string `Unspecified` with an optional
hash character prefix to handle potential name collisions.

The debugger does a small amount of type validation when displaying storage and stack items. For example, if a
given item has an associated Struct type, the debugger will validate the underlying NeoVM item is an array object
(array and structs are stored the same in NeoVM) and that the struct field count matches the array count. If
there is a mismatch, the debugger will discard the type information and display the value as if the type 
were Unspecified. 