// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { WorkspaceFolder, DebugConfiguration, ProviderResult, CancellationToken } from 'vscode';
import { join, basename, relative, resolve } from 'path';
import * as fs from 'fs';
import * as os from 'os';
import * as cp from 'child_process';

function checkFileExists(filePath: string): Promise<boolean> {
    return new Promise((resolve, reject) => {
        fs.stat(filePath, (err, stats) => {
			if (stats && stats.isFile()) {
                resolve(true);
            } else {
                resolve(false);
            }
        });
    });
}

function deleteFile(filePath: fs.PathLike): Promise<void> {
	return new Promise((resolve, reject) => {
		fs.unlink(filePath, (err) => {
			if (err) {
				reject(err);
			} else {
				resolve();
			}
		});
	});
}

function execChildProcess(command: string, workingDirectory: string): Promise<string> {
    return new Promise<string>((resolve, reject) => {
        cp.exec(command, { cwd: workingDirectory, maxBuffer: 500 * 1024 }, (error, stdout, stderr) => {
            if (error) {
                reject(error);
            }
            else if (stderr && stderr.length > 0) {
                reject(new Error(stderr));
            }
            else {
                resolve(stdout);
            }
        });
    });
}

function getDebugAdapterFileName(): string {
	return os.platform() === "win32" ? "neodebug-adapter.exe" : "neodebug-adapter";
}

function getDebugAdapterPath(extension:vscode.Extension<any>): string {
	return resolve(extension.extensionPath, "adapter", getDebugAdapterFileName());
}

function getDebugAdapterPackageFileName(extension:vscode.Extension<any>): string {
	return "neo.debug.adapter." + extension.packageJSON.version + ".nupkg";
}

function getDebugAdapterPackagePath(extension:vscode.Extension<any>): string {
	return resolve(extension.extensionPath, getDebugAdapterPackageFileName(extension));
}

function checkDebugAdapterExists(extension:vscode.Extension<any>): Promise<boolean> {
	return checkFileExists(getDebugAdapterPath(extension));
}

function checkDebugAdapterPackageExists(extension:vscode.Extension<any>): Promise<boolean> {
	return checkFileExists(getDebugAdapterPackagePath(extension));
}

function inDevelopmentMode() : boolean {
	// recommended workaround for https://github.com/Microsoft/vscode/issues/10272
	return vscode.env.sessionId === "someValue.sessionId";
}

async function processRuntimeDependencies(): Promise<void> {
	
	const extension = vscode.extensions.getExtension("ngd-seattle.neo-contract-debug") as vscode.Extension<any>;
	const debugAdapterExists = await checkDebugAdapterExists(extension);
	const debugAdapterPackageExists = await checkDebugAdapterPackageExists(extension);

	if (debugAdapterExists) {
		if (debugAdapterPackageExists) {
			// cleanup
			await deleteFile(getDebugAdapterPackagePath(extension));
		}
	} else {
		if (debugAdapterPackageExists) {
			let response = await execChildProcess(
				`dotnet tool install neodebug-adapter --version ${extension.packageJSON.version} --tool-path ./adapter --add-source .`,
				extension.extensionPath);
			await deleteFile(getDebugAdapterPackagePath(extension));
		} else {
			if (!inDevelopmentMode()) {
				vscode.window.showErrorMessage("Neo Debug adapter tool and package are both missing. Please reinstall the extension");
			}
		}
	}
}

// this method is called when your extension is activated
// your extension is activated the very first time the command is executed
export async function activate(context: vscode.ExtensionContext) {

	await processRuntimeDependencies();

	const configProvider = new NeoContractDebugConfigurationProvider();
	context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider("neo-contract", configProvider));

	const factory = new NeoContractDebugAdapterDescriptorFactory();
	context.subscriptions.push(vscode.debug.registerDebugAdapterDescriptorFactory("neo-contract", factory));
}

class NeoContractDebugConfigurationProvider implements vscode.DebugConfigurationProvider {
	public async provideDebugConfigurations(folder: WorkspaceFolder | undefined, token?: CancellationToken): Promise<DebugConfiguration[]> {
		
		function createConfig(programPath: string | undefined = undefined): DebugConfiguration {
			return {
				name: programPath ? basename(programPath) : "NEO Contract",
				type: "neo-contract",
				request: "launch",
				program: programPath && folder 
					? join("${workspaceFolder}", relative(folder.uri.fsPath, programPath)) 
					: "${workspaceFolder}",
				args: [],
				storage: [],
				runtime: {
					witnesses: {
						"check-result": true
					}
				}
			};
		}
		
		if (folder)
		{
			var neoVmFiles = await vscode.workspace.findFiles(new vscode.RelativePattern(folder, "**/*.{avm,nvm}"));
			if (neoVmFiles.length > 0) {
				return neoVmFiles.map(f => createConfig(f.fsPath));
			}
		}

		return [createConfig()];
	}
}

async function getDebugAdapterCommand(config:vscode.WorkspaceConfiguration) : Promise<[string, string[]]> {
	var debugAdapterConfig = config.get<string[]>("debug-adapter");
	if (debugAdapterConfig && debugAdapterConfig.length > 0) {
		return [debugAdapterConfig[0], debugAdapterConfig.slice(1)];
	}

	const extension = vscode.extensions.getExtension("ngd-seattle.neo-contract-debug") as vscode.Extension<any>;
	if (await checkDebugAdapterExists(extension)) {
		return [getDebugAdapterPath(extension), []];
	}

	if (inDevelopmentMode()) {
		const adapterPath = resolve(extension.extensionPath, "..", "adapter");
		return ["dotnet", ["run", "--project", adapterPath, "--"]];
	}

	throw new Error("cannot locate debug adapter");
}
class NeoContractDebugAdapterDescriptorFactory implements vscode.DebugAdapterDescriptorFactory {

	async createDebugAdapterDescriptor(session: vscode.DebugSession, executable: vscode.DebugAdapterExecutable | undefined): Promise<vscode.DebugAdapterDescriptor> {
		const config = vscode.workspace.getConfiguration("neo-debugger");
		let [cmd, args] = await getDebugAdapterCommand(config);
		
		if (config.get<Boolean>("debug", false))
		{
			args.push("--debug");
		}

		if (config.get<Boolean>("log", false))
		{
			args.push("--log");
		}

		return new vscode.DebugAdapterExecutable(cmd, args);
	}
}

// this method is called when your extension is deactivated
export function deactivate() {}
