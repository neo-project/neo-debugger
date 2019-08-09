// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { WorkspaceFolder, DebugConfiguration, ProviderResult, CancellationToken } from 'vscode';
import { join, basename } from 'path';
import { unwatchFile } from 'fs';

// this method is called when your extension is activated
// your extension is activated the very first time the command is executed
export function activate(context: vscode.ExtensionContext) {
	const configProvider = new NeoContractDebugConfigurationProvider();
	context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider("neo-contract", configProvider));

	const factory = new NeoContractDebugAdapterDescriptorFactory();
	context.subscriptions.push(vscode.debug.registerDebugAdapterDescriptorFactory("neo-contract", factory));
}

async function* findNeoVmFiles(uri: vscode.Uri) : AsyncIterableIterator<string> {
	for (var [fileName,fileType] of await vscode.workspace.fs.readDirectory(uri)) {
		if (fileType === vscode.FileType.File && fileName.endsWith(".avm"))
		{
			yield join(uri.fsPath, fileName);
		}

		if (fileType === vscode.FileType.Directory)
		{
			for await (var file of findNeoVmFiles(vscode.Uri.file(join(uri.fsPath, fileName))))
			{
				yield file;
			}
		}
	}
}

class NeoContractDebugConfigurationProvider implements vscode.DebugConfigurationProvider {
	public async provideDebugConfigurations(folder: WorkspaceFolder | undefined, token?: CancellationToken): Promise<DebugConfiguration[]> {
		
		function createConfig(programPath: string | undefined = undefined): DebugConfiguration {
			return {
				name: programPath ? basename(programPath) : "NEO Contract",
				type: "neo-contract",
				request: "launch",
				program: "${workspaceFolder}" + (programPath ? programPath : ""),
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
			var configs : DebugConfiguration[] = [];
			for await (var neoVmFile of findNeoVmFiles(folder.uri))
			{
				var programPath = neoVmFile.slice(folder.uri.fsPath.length);
				configs.push(createConfig(programPath));
			}

			if (configs.length > 0) {
				return configs;
			}
		}

		return [createConfig()];
	}
}

class NeoContractDebugAdapterDescriptorFactory implements vscode.DebugAdapterDescriptorFactory {

	createDebugAdapterDescriptor(session: vscode.DebugSession, executable: vscode.DebugAdapterExecutable | undefined): vscode.ProviderResult<vscode.DebugAdapterDescriptor> {
		const config = vscode.workspace.getConfiguration("neo-debugger");
		
		var debugAdapterConfig = config.get<string[]>("debug-adapter");
		var cmd = debugAdapterConfig && debugAdapterConfig.length > 0 ? debugAdapterConfig[0] : "neo-debug-adapter";
		var args : string[] = debugAdapterConfig && debugAdapterConfig.length > 1 ? debugAdapterConfig.slice(1) : [];
		
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
