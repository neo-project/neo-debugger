const fs = require('fs');
const path = require('path');
const process = require('process');

/*

The Neo Smart Contract Debugger works with VS Code v1.47.0 and later. However, the new VSCode pre-release
extension support requires v1.63.0 or later. In order to continue to support developers running older
versions of VS code, the VSCode engine version in package.json is only set to v1.63.0 in pre-release branches.

This script updates "engines.vscode" and "devDependencies.@types/vscode" properties in package.json
from the folder above this script's location to v1.63.0. Note, the user can override the vscode engine
version to use via a command line parameter, however this input is not validated in any way.

*/

async function main(path, engine) {
    engine = engine[0] == '^' ? engine : "^" + engine;

    const packageJsonText = await fs.promises.readFile(path, "utf8");
    const packageJson = JSON.parse(packageJsonText);

    packageJson.engines.vscode = engine;
    packageJson.devDependencies["@types/vscode"] = engine;

    await fs.promises.writeFile(path, JSON.stringify(packageJson, null, 4));
}

const packageJsonPath = path.join(__dirname, '../package.json');
const engineVersion = process.argv.length > 2 ? process.argv[2] : "1.63.0";
console.log("Updating package.json vscode engine to " + engineVersion);
main(packageJsonPath, engineVersion);
