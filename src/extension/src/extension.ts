// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { WorkspaceFolder, DebugConfiguration, ProviderResult, CancellationToken } from 'vscode';
import { join, basename, relative, resolve, extname } from 'path';
import * as fs from 'fs/promises';
import { createWriteStream, stat, unlink } from 'fs';
import * as os from 'os';
import * as cp from 'child_process';
import * as _glob from 'glob';
import * as https from 'https';
import { Octokit } from "@octokit/rest";
import { deepStrictEqual, rejects } from 'assert';
import { runInNewContext } from 'vm';
import { fileURLToPath } from 'url';
import { Stream } from 'stream';

// lifted from https://github.com/sindresorhus/slash
function slash(path: string) {
    const isExtendedLengthPath = /^\\\\\?\\/.test(path);
    const hasNonAscii = /[^\u0000-\u0080]+/.test(path); // eslint-disable-line no-control-regex

    if (isExtendedLengthPath || hasNonAscii) {
        return path;
    }

    return path.replace(/\\/g, '/');
}

function checkFileExists(filePath: string): Promise<boolean> {
    return new Promise((resolve, reject) => {
        stat(filePath, (err, stats) => {
            if (stats && stats.isFile()) {
                resolve(true);
            } else {
                resolve(false);
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

function glob(pattern: string, options: _glob.IOptions = {}): Promise<string[]> {
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

function downloadFile(url: string, path: string): Promise<void> {
    return new Promise((resolve, reject) => {

        const request = https.get(url, (res) => {
            const statusCode = res.statusCode;

            if (statusCode === 200) {

                const stream = createWriteStream(path, { flags: 'wx' });
                res.on('end', () => {
                    stream.end();
                    resolve();
                })
                    .on('error', e => {
                        stream.destroy();
                        unlink(path, () => reject(e));
                    })
                    .pipe(stream);
            } else if (statusCode === 301 || statusCode === 302) {
                downloadFile(res.headers.location!, path).then(() => resolve());
            } else {
                reject(new Error(`downloadFile failed ${res.statusCode}`));
            }
        });

        request.on('error', e => { unlink(path, () => reject(e)) });
    });
}

// this method is called when your extension is activated
// your extension is activated the very first time the command is executed
export async function activate(context: vscode.ExtensionContext) {

    let neoDebugChannel = vscode.window.createOutputChannel('Neo Debugger Log');

    const configProvider = new NeoContractDebugConfigurationProvider();
    context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider("neo-contract", configProvider));

    const factory = new NeoContractDebugAdapterDescriptorFactory(context, neoDebugChannel);
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
    const settings: DebugViewSettings = {
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
                    ? slash(join("${workspaceFolder}", relative(folder.uri.fsPath, programPath)))
                    : "${workspaceFolder}/<insert path to contract here>",
                invocation: {
                    operation: (programPath ? extname(programPath) : "") === ".nef"
                        ? "<insert operation here>"
                        : undefined,
                    args: [],
                },
                storage: []
            };
        }

        if (folder) {
            var neoVmFiles = await vscode.workspace.findFiles(new vscode.RelativePattern(folder, "**/*.{avm,nef}"));
            if (neoVmFiles.length > 0) {
                return neoVmFiles.map(f => createConfig(f.fsPath));
            }
        }

        return [createConfig()];
    }
}











class NeoContractDebugAdapterDescriptorFactory implements vscode.DebugAdapterDescriptorFactory {

    private readonly extension: vscode.Extension<any>;
    private readonly extensionMode: vscode.ExtensionMode;
    private get extensionPath() { return this.extension.extensionPath; }

    constructor(context: vscode.ExtensionContext, readonly channel: vscode.OutputChannel) {
        this.extension = context.extension;
        this.extensionMode = context.extensionMode;
    }

    async createDebugAdapterDescriptor(session: vscode.DebugSession, executable: vscode.DebugAdapterExecutable | undefined): Promise<vscode.DebugAdapterDescriptor> {

        const program: string = session.configuration["program"];
        this.validateDebugConfig(program, session.configuration);

        const config = vscode.workspace.getConfiguration("neo-debugger");
        let { cmd, args } = await this.getDebugAdapterCommand(program, config);

        if (config.get<Boolean>("debug", false)) {
            args.push("--debug");
        }

        if (config.get<Boolean>("log", false)) {
            args.push("--log");
        }

        const defaultDebugView = config.get<string>("default-debug-view");
        if (defaultDebugView) {
            args.push("--debug-view");
            args.push(defaultDebugView);
        }

        const options = session.workspaceFolder ? { cwd: session.workspaceFolder.uri.fsPath } : {};
        this.channel.appendLine(`launching ${cmd} ${args.join(' ')}`);
        this.channel.appendLine(`current directory ${options.cwd ?? 'missing'}`);
        return new vscode.DebugAdapterExecutable(cmd, args, options);
    }

    private async getDebugAdapterCommand(program: string, config: vscode.WorkspaceConfiguration): Promise<{ cmd: string; args: string[]; }> {

        // if the debug-adapter path is specified in the config file, use it 
        const debugAdapterConfig = config.get<string[]>("debug-adapter");
        if (debugAdapterConfig && debugAdapterConfig.length > 0) {
            return {
                cmd: debugAdapterConfig[0],
                args: debugAdapterConfig.slice(1)
            };
        }

        // get path to where debug adapter package gets installed 
        const packageId = getAdapterPackageId(program);
        const installedAdapterPath = this.getInstalledAdapterPath(packageId);
        // if the adapter is found at the expected installation location, use it
        if (await checkFileExists(installedAdapterPath)) {
            return { cmd: installedAdapterPath, args: [] };
        } else {
            this.channel.appendLine(`${packageId} not installed at ${installedAdapterPath}`)
        }

        // check the extension folder for the debug adapter package
        const adapterPackagePath = await this.getExtensionAdapterPackagePath(packageId);
        if (await checkFileExists(adapterPackagePath)) {
            const base = basename(adapterPackagePath, ".nupkg");
            this.channel.appendLine(`Installing ${base} package`);
            const version = base.startsWith(`${packageId}.`) ? base.substring(packageId.length + 1) : undefined;
            if (!version) { throw new Error(`could not determine package version of ${adapterPackagePath}`) }

            await this.installAdapter(packageId, version);
            if (await checkFileExists(installedAdapterPath) === false) {
                throw new Error(`Installing adapter package ${adapterPackagePath} failed`);
            }

            this.channel.appendLine(`  deleting \"${basename(adapterPackagePath)}\"`);
            await fs.rm(adapterPackagePath);
            return { cmd: installedAdapterPath, args: [] };
        } else {
            this.channel.appendLine(`${packageId} package not available at ${adapterPackagePath}`)
        }

        let version = this.extension.packageJSON.version;
        const octokit = new Octokit();
        const release = version === '0.0.0'
            ? await octokit.rest.repos.getLatestRelease({
                owner: 'neo-project',
                repo: 'neo-debugger'
            })
            : await octokit.rest.repos.getReleaseByTag({
                owner: 'neo-project',
                repo: 'neo-debugger',
                tag: version
            });
        if (release.status < 200 || release.status > 299) {
            throw new Error("fetching release info from github failed");
        }

        const downloadResponse = await vscode.window.showInformationMessage(
            `${packageId} package cannot be found locally. Download version ${release.data.tag_name} from GitHub?`,
            "Yes", "No");

        if (downloadResponse === 'Yes') {
            const asset = release.data.assets.find(a => {
                return a.name.startsWith(packageId) && a.name.endsWith(".nupkg");
            })

            if (asset) {

                const tempDir = await fs.mkdtemp(resolve(os.tmpdir(), `${packageId}-`));
                const tempPath = resolve(tempDir, asset.name);

                this.channel.appendLine(`downloading ${asset.browser_download_url} to ${tempDir}`)
                await downloadFile(asset.browser_download_url, tempPath);
                const stat = await fs.stat(tempPath);
                if (stat.size === 0) throw new Error(`download failed`);
                this.channel.appendLine(`installing ${packageId}.${release.data.tag_name} from ${tempDir}`)

                await this.installAdapter(packageId, release.data.tag_name, tempDir);
                if (await checkFileExists(installedAdapterPath)) {
                    await fs.rm(tempDir, { recursive: true, force: true });
                    return { cmd: installedAdapterPath, args: [] };
                }
            }
        }

        // if the extension is in development mode, check for the debug adapter in the relevant adapter source directory
        if (this.extensionMode === vscode.ExtensionMode.Development) {
            // const { folderName, framework } = getAdapterProjectInfo(program);
            // const adapterProjectFolderPath = resolve(this.extensionPath, "..", folderName);

            // // if there's a compiled version of the adapter, use it
            // var adapterProjectPath = resolve(adapterProjectFolderPath, "bin", "Debug", framework, basename(installedAdapterPath));
            // if (await checkFileExists(adapterProjectPath)) {
            //     return { cmd: adapterProjectPath, args: [] };
            // }

            // if there is not a compiled version of the adapter, launch the debug adapter via `dotnet run`
            return {
                cmd: "dotnet",
                args: ["run", "--project", this.getAdapterProjectPath(packageId), "--"]
            };
        }

        throw new Error(`cannot locate ${packageId} debug adapter`);

        function getAdapterPackageId(program: string) {
            switch (extname(program)) {
                case '.nef':
                    return 'Neo.Debug3.Adapter';
                case '.avm':
                    return 'Neo.Debug2.Adapter';
                default:
                    throw new Error(`Unexpected Neo contract extension ${extname(program)}`);
            }
        }


        // function getAdapterProjectInfo(program: string): { folderName: string; framework: string; } {
        //     switch (extname(program)) {
        //         case '.nef':
        //             return {
        //                 folderName: 'adapter3',
        //                 framework: "net6.0"
        //             };
        //         case '.avm':
        //             return {
        //                 folderName: 'adapter1',
        //                 framework: "netcoreapp3.1"
        //             };
        //         default:
        //             throw new Error(`Unexpected Neo contract extension {ext}`);
        //     }
        // }
    }


    private getAdapterProjectPath(packageId: string): string {
        const srcDirectoryPath = resolve(this.extensionPath, "..");
        switch (packageId) {
            case 'Neo.Debug3.Adapter':
                return resolve(srcDirectoryPath, 'adapter3');
            case 'Neo.Debug2.Adapter':
                return resolve(srcDirectoryPath, 'adapter2');
            default:
                throw new Error(`Unexpected adapter package ${packageId}`);
        }
    }


    private getAdapterProjectExePath(packageId: string): string {
        const adapterProjectPath = this.getAdapterProjectPath(packageId);
        const adapterFileName = basename(this.getInstalledAdapterPath(packageId));
        switch (packageId) {
            case 'Neo.Debug3.Adapter':
                return resolve(adapterProjectPath, "bin", "Debug", "net6.0", adapterFileName);
            case 'Neo.Debug2.Adapter':
                return resolve(adapterProjectPath, "bin", "Debug", "netcoreapp3.1", adapterFileName);
            default:
                throw new Error(`Unexpected adapter package ${packageId}`);
        }
    }


    private getInstalledAdapterPath(packageId: string): string {
        return resolve(this.extensionPath, packageId, this.getAssemblyName(packageId));
    }

    private getAssemblyName(packageId: string) {

        switch (packageId) {
            case 'Neo.Debug3.Adapter':
                return addExeOnWindows('neodebug-3-adapter');
            case 'Neo.Debug2.Adapter':
                return addExeOnWindows('neodebug-2-adapter');
            default:
                throw new Error(`Unexpected adapter package ${packageId}`);
        }

        function addExeOnWindows(path: string) {
            return os.platform() === "win32" ? `${path}.exe` : path;
        }
    }

    private async getExtensionAdapterPackagePath(packageId: string) {
        const extensionPath = this.extensionPath;
        const extensionVersion: string = this.extension.packageJSON.version;
        const currentVersionPackageName = resolve(extensionPath, `${packageId}.${extensionVersion}.nupkg`);

        // if (await checkFileExists(currentVersionPackageName)) {
        return currentVersionPackageName;
        // }

        // var matchGlob = join(extensionPath, `${packageId}.*.nupkg`);
        // var matches = await glob(matchGlob);
        // return matches.length === 1 ? matches[0] : undefined;
    }

    // private async installAdapterPackage(adapterPackagePath: string, packageId: string) {
    //     this.channel.appendLine(`Installing ${basename(adapterPackagePath)} package`);
    //     const base = basename(adapterPackagePath, ".nupkg");
    //     const version = base.startsWith(`${packageId}.`) ? base.substring(packageId.length + 1) : undefined;
    //     if (!version) { throw new Error(`could not determine package version of ${adapterPackagePath}`) }

    //     const commandLine = `dotnet tool install ${packageId} --version ${version} --tool-path ./${packageId} --add-source .`;
    //     this.channel.appendLine(`  executing \"${commandLine}\"`);
    //     await execChildProcess(commandLine, this.extensionPath);
    //     this.channel.appendLine(`  deleting \"${basename(adapterPackagePath)}\"`);
    //     await deleteFile(adapterPackagePath);
    // }

    private async installAdapter(packageId: string, version: string, sourceDir?: string) {
        sourceDir ??= '.';
        const toolPath = resolve(this.extensionPath, packageId);
        const commandLine = `dotnet tool install ${packageId} --version ${version} --tool-path ${toolPath} --add-source ${sourceDir}`;
        this.channel.appendLine(`  executing \"${commandLine}\"`);
        await execChildProcess(commandLine, this.extensionPath);
    }

    private validateDebugConfig(program: string, config: vscode.DebugConfiguration) {
        switch (extname(program)) {
            case '.nef': {
                if (config["utxo"]) {
                    throw new Error("utxo configuration not supported in Neo 3");
                }
                break;
            }
            case '.avm': {
                if (config["traceFile"]) {
                    throw new Error("traceFile configuration not supported in Neo 2");
                }
                if (config["operation"]) {
                    throw new Error("operation configuration not supported in Neo 2");
                }
                break;
            }
            default:
                throw new Error(`Unexpected Neo contract extension {ext}`);
        }
    }
}

// this method is called when your extension is deactivated
export function deactivate() { }
