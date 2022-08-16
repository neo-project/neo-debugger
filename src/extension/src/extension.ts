// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { WorkspaceFolder, DebugConfiguration, CancellationToken } from 'vscode';
import { join, basename, relative, resolve, extname } from 'path';
import * as fs from 'fs/promises';
import { createWriteStream, stat, unlink } from 'fs';
import * as os from 'os';
import * as cp from 'child_process';
import * as _glob from 'glob';
import * as https from 'https';
import { Octokit } from "@octokit/rest";

const OWNER = 'neo-project';
const REPO = 'neo-debugger';

interface GithubReleaseAsset {
    url: string;
    browser_download_url: string;
    id: number;
    node_id: string;
    name: string;
    label: string | null;
    state: "uploaded" | "open";
    content_type: string;
    size: number;
    download_count: number;
    created_at: string;
    updated_at: string;
    uploader: {
        name?: string | null | undefined;
        email?: string | null | undefined;
        login: string;
        id: number;
        node_id: string;
        avatar_url: string;
        gravatar_id: string | null;
        url: string;
        html_url: string;
        followers_url: string;
        following_url: string;
        gists_url: string;
        starred_url: string;
        subscriptions_url: string;
        organizations_url: string;
        repos_url: string;
        events_url: string;
        received_events_url: string;
        type: string;
        site_admin: boolean;
        starred_at?: string | undefined;
    } | null;
}

interface GithubRelease {
    url: string;
    html_url: string;
    assets_url: string;
    upload_url: string;
    tarball_url: string | null;
    zipball_url: string | null;
    id: number;
    node_id: string;
    tag_name: string;
    target_commitish: string;
    name: string | null;
    body?: string | null | undefined;
    draft: boolean;
    prerelease: boolean;
    created_at: string;
    published_at: string | null;
    author: {
        name?: string | null | undefined;
        email?: string | null | undefined;
        login: string;
        id: number;
        node_id: string;
        avatar_url: string;
        gravatar_id: string | null;
        url: string;
        html_url: string;
        followers_url: string;
        following_url: string;
        gists_url: string;
        starred_url: string;
        subscriptions_url: string;
        organizations_url: string;
        repos_url: string;
        events_url: string;
        received_events_url: string;
        type: string;
        site_admin: boolean;
        starred_at?: string | undefined;
    };
    assets: GithubReleaseAsset[];
    body_html?: string | undefined;
    body_text?: string | undefined;
    mentions_count?: number | undefined;
    discussion_url?: string | undefined;
    reactions?: {
        url: string;
        total_count: number;
        "+1": number;
        "-1": number;
        laugh: number;
        confused: number;
        heart: number;
        hooray: number;
        eyes: number;
        rocket: number;
    } | undefined;
}

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


function downloadFile(url: string, path: string, onData: (length: number) => boolean) {
    return new Promise<void>((resolve, reject) => {
        const request = https.get(url, (res) => {
            const statusCode = res.statusCode;

            if (statusCode === 200) {

                const stream = createWriteStream(path, { flags: 'wx' });
                res
                    .on('data', (c:Buffer) => {
                        const cancel = onData(c.length);
                        if (cancel) {
                            reject(new Error("user canceled"));
                        }
                    })
                    .on('end', () => {
                        stream.end();
                        resolve();
                    })
                    .on('error', e => {
                        stream.destroy();
                        unlink(path, () => reject(e));
                    })
                    .pipe(stream);
            } else if (statusCode === 301 || statusCode === 302) {
                downloadFile(res.headers.location!, path, onData ).then(() => resolve());
            } else {
                reject(new Error(`downloadFile failed ${res.statusCode}`));
            }
        });

        request.on('error', e => { unlink(path, () => reject(e)) });
    });

}

function downloadAsset(asset: GithubReleaseAsset, path: string, url?: string) {
    return vscode.window.withProgress({
        location: vscode.ProgressLocation.Notification,
        title: `Downloading ${asset.name}`,
        cancellable: true
    }, (progress, token) => {
        let total = 0;
        return downloadFile(asset.browser_download_url, path, length => {
            total += length;
            const increment = total / asset.size 
            const message = `${total} of ${asset.size} downloaded`;
            progress.report({ message });
            return token.isCancellationRequested;
        })
    })
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
        this.channel.appendLine(`Launching debug adapter "${cmd} ${args.join(' ')}"`);
        this.channel.appendLine(`  Current directory "${options.cwd ?? 'missing'}"`);
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
        const installedAdapterPath = resolve(this.extensionPath, packageId, this.getAssemblyName(packageId));

        // if the adapter is found at the expected installation location, use it
        if (await checkFileExists(installedAdapterPath)) {
            return { cmd: installedAdapterPath, args: [] };
        } else {
            this.channel.appendLine(`${packageId} not found at ${installedAdapterPath}`)
        }

        // if the debug adapter package is available locally, install it
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
            this.channel.appendLine(`${packageId} package not found in extension path ${this.extensionPath}`)
        }

        // if the extension is in development mode (i.e. the debugger extension is being debugged),
        // look for the debug adapter in the relevant adapter source directory
        if (this.extensionMode === vscode.ExtensionMode.Development
            && (config.get<boolean>("adapter-project") ?? true)
        ) {
            // if there's a compiled version of the adapter, use it
            const adapterProjectExePath = this.getAdapterProjectExePath(packageId);
            if (await checkFileExists(adapterProjectExePath)) {
                return { cmd: adapterProjectExePath, args: [] };
            }

            // if there is not a compiled version of the adapter, launch the debug adapter via `dotnet run`
            return {
                cmd: "dotnet",
                args: ["run", "--project", this.getAdapterProjectPath(packageId), "--"]
            };
        }

        // if the debug adapter package is not available locally, check to see if it can be
        // downloaded from github
        const version = this.extension.packageJSON.version;
        const asset = await getAdapterPackageReleaseAsset(version, packageId);
        if (asset) {

            const tempDir = await fs.mkdtemp(resolve(os.tmpdir(), `${packageId}-`));
            const tempPath = resolve(tempDir, asset.name);

            this.channel.appendLine(`downloading ${asset.browser_download_url} to ${tempDir}`)
            await downloadAsset(asset, tempPath);
            const stat = await fs.stat(tempPath);
            if (stat.size === 0) throw new Error(`download failed`);

            this.channel.appendLine(`installing ${packageId}.${version} from ${tempDir}`)
            await this.installAdapter(packageId, version, tempDir);
            if (await checkFileExists(installedAdapterPath)) {
                this.channel.appendLine(`removing ${tempDir}`)
                await fs.rm(tempDir, { recursive: true, force: true });
                return { cmd: installedAdapterPath, args: [] };
            }
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

        async function getAdapterPackageReleaseAsset(version: string, packageId: string): Promise<GithubReleaseAsset | undefined> {
            const release = await getAdapterPackageRelease(version);
            return release?.assets.find(asset => {
                return asset.name.startsWith(packageId)
                    && asset.name.endsWith('.nupkg')
            });
        }

        async function getAdapterPackageRelease(version: string): Promise<GithubRelease | undefined> {
            try {
                const octokit = new Octokit();
                const release = await octokit.rest.repos.getReleaseByTag({
                    owner: OWNER,
                    repo: REPO,
                    tag: version
                });
                if (release.status < 200 || release.status > 299) {
                    return undefined;
                }
                return release.data;
            } catch {
                return undefined;
            }
        }
    }

    private getAdapterProjectPath(packageId: string): string {
        const segments = [this.extensionPath, ".."];
        switch (packageId) {
            case 'Neo.Debug3.Adapter':
                segments.push('adapter3'); break;
            case 'Neo.Debug2.Adapter':
                segments.push('adapter2'); break;
            default:
                throw new Error(`Unexpected adapter package ${packageId}`);
        }
        return resolve(...segments);
    }

    private getAdapterProjectExePath(packageId: string): string {
        const adapterProjectPath = this.getAdapterProjectPath(packageId);
        const segments = [adapterProjectPath, "bin", "Debug"];

        switch (packageId) {
            case 'Neo.Debug3.Adapter':
                segments.push("net6.0");
                break;
            case 'Neo.Debug2.Adapter':
                segments.push("netcoreapp3.1");
                break;
            default:
                throw new Error(`Unexpected adapter package ${packageId}`);
        }

        segments.push(this.getAssemblyName(packageId));
        return resolve(...segments);
    }

    private getAssemblyName(packageId: string) {

        let path: string;
        switch (packageId) {
            case 'Neo.Debug3.Adapter':
                path = 'neodebug-3-adapter';
                break;
            case 'Neo.Debug2.Adapter':
                path = 'neodebug-2-adapter';
                break;
            default:
                throw new Error(`Unexpected adapter package ${packageId}`);
        }

        return os.platform() === "win32" ? `${path}.exe` : path;
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

    private async installAdapter(packageId: string, version: string, sourceDir?: string) {
        sourceDir ??= '.';
        const toolPath = resolve(this.extensionPath, packageId);
        const commandLine = `dotnet tool install ${packageId} --version ${version} --tool-path ${toolPath} --add-source ${sourceDir}`;
        this.channel.appendLine(`Installing ${packageId} via \"${commandLine}\"`);
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
