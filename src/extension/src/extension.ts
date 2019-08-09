// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { WorkspaceFolder, DebugConfiguration, ProviderResult, CancellationToken } from 'vscode';
import { join, basename, relative } from 'path';
import { unwatchFile } from 'fs';
import { create } from 'domain';

// this method is called when your extension is activated
// your extension is activated the very first time the command is executed
export function activate(context: vscode.ExtensionContext) {
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
