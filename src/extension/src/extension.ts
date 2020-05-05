// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { WorkspaceFolder, DebugConfiguration, ProviderResult, CancellationToken } from 'vscode';
import { join, basename, relative, resolve } from 'path';
import * as fs from 'fs';
import * as os from 'os';
import * as cp from 'child_process';
import * as _glob from 'glob';

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

function glob(pattern:string, options: _glob.IOptions = {}) : Promise<string[]> {
	return new Promise((resolve, reject) => {
		_glob(pattern, options, (err, matches) => {
			if (err) {
				reject(err);
			} else {
				resolve(matches);
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

function checkDebugAdapterExists(extension:vscode.Extension<any>): Promise<boolean> {
	return checkFileExists(getDebugAdapterPath(extension));
}

async function getDebugAdapterPackagePath(extension:vscode.Extension<any>): Promise<string | undefined> {
	const currentVersionPackageName = resolve(extension.extensionPath, "Neo.Debug.Adapter." + extension.packageJSON.version + ".nupkg");
	if (await checkFileExists(currentVersionPackageName)) {
		return currentVersionPackageName;
	}

	var matchGlob = join(extension.extensionPath, "Neo.Debug.Adapter.*.nupkg");
	var matches = await glob(matchGlob);

	if (matches.length === 1) {
		return matches[0];
	}

	return undefined;
}

function getDebugAdapterVersion(path:string): string|undefined {

	const base = basename(path, ".nupkg");
	if (base.startsWith("Neo.Debug.Adapter.")) {
		return base.substring(18);
	}

	return undefined;
}

function inDevelopmentMode() : boolean {
	// recommended workaround for https://github.com/Microsoft/vscode/issues/10272
	return vscode.env.sessionId === "someValue.sessionId";
}

async function processRuntimeDependencies(channel : vscode.OutputChannel): Promise<void> {
	
	const extension = vscode.extensions.getExtension("ngd-seattle.neo-contract-debug") as vscode.Extension<any>;
	
	channel.appendLine(`extension folder ${extension.extensionPath}`);

	const debugAdapterPath = getDebugAdapterPath(extension);
	if (await checkFileExists(debugAdapterPath)) {
		channel.appendLine(`located adapter ${debugAdapterPath}`);
		return;
	}

	const packagePath = await getDebugAdapterPackagePath(extension);
	if (packagePath && await checkFileExists(packagePath)) {
		channel.appendLine(`located packagePath ${packagePath}`);
		const version = getDebugAdapterVersion(packagePath);
		if (version) {
			const commandLine = `dotnet tool install Neo.Debug.Adapter --version ${version} --tool-path ./adapter --add-source .`;
			channel.appendLine(`executing \"${commandLine}\"`);
			await execChildProcess(commandLine, extension.extensionPath);
			channel.appendLine(`deleting \"${packagePath}\"`);
			await deleteFile(packagePath);
			return;
		}
	} else {
		channel.appendLine(`no packagePath found`);
	}

	if (!inDevelopmentMode()) {
		vscode.window.showErrorMessage("Neo Debug adapter tool and package are both missing. Please reinstall the extension");
	} else {
		channel.appendLine(`development mode enabled`);
	}

}

// this method is called when your extension is activated
// your extension is activated the very first time the command is executed
export async function activate(context: vscode.ExtensionContext) {

	let neoDebugChannel = vscode.window.createOutputChannel('Neo Debugger Log');

	await processRuntimeDependencies(neoDebugChannel);

	const configProvider = new NeoContractDebugConfigurationProvider();
	context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider("neo-contract", configProvider));

	const factory = new NeoContractDebugAdapterDescriptorFactory(neoDebugChannel);
	context.subscriptions.push(vscode.debug.registerDebugAdapterDescriptorFactory("neo-contract", factory));

	context.subscriptions.push(vscode.commands.registerCommand("neo-debugger.displaySourceView",
		async () => await changeDebugView("source")));
	context.subscriptions.push(vscode.commands.registerCommand("neo-debugger.displayDisassemblyView",
		async () => await changeDebugView("disassembly")));
	context.subscriptions.push(vscode.commands.registerCommand("neo-debugger.toggleDebugView", 
		async () => await changeDebugView("toggle")));
}

class DebugViewSettings {
	debugView: 'source' | 'disassembly' | 'toggle' = 'source';
}

async function changeDebugView(debugView: 'source' | 'disassembly' | 'toggle') {
	const settings: DebugViewSettings =  {
		debugView: debugView
	};

	if (vscode.debug.activeDebugSession && vscode.debug.activeDebugSession.type === 'neo-contract') {
		await vscode.debug.activeDebugSession.customRequest("debugview", settings);
	}
}

class NeoContractDebugConfigurationProvider implements vscode.DebugConfigurationProvider {
	public async provideDebugConfigurations(folder: WorkspaceFolder | undefined, token?: CancellationToken): Promise<DebugConfiguration[]> {
		
		function createConfig(programPath: string | undefined = undefined): DebugConfiguration {
			return {
				name: programPath ? basename(programPath) : "Neo Contract",
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
		return ["dotnet", ["run", "--project", adapterPath, "--no-build", "--"]];
	}

	throw new Error("cannot locate debug adapter");
}
class NeoContractDebugAdapterDescriptorFactory implements vscode.DebugAdapterDescriptorFactory {

	channel: vscode.OutputChannel;
	constructor (channel: vscode.OutputChannel) {
		this.channel = channel;
	}

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

		const defaultDebugView = config.get<string>("default-debug-view");
		if (defaultDebugView)
		{
			args.push("--debug-view");
			args.push(defaultDebugView);
		}

		const options = session.workspaceFolder ? { cwd: session.workspaceFolder.uri.fsPath } : {};
		this.channel.appendLine(`launching ${cmd} ${args.join(' ')}`);
		this.channel.appendLine(`current directory ${options.cwd ?? 'missing'}`);
		return new vscode.DebugAdapterExecutable(cmd, args, options);	
	}
}

// this method is called when your extension is deactivated
export function deactivate() {}
