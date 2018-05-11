'use strict';

import {debug, workspace, commands, window, ExtensionContext, QuickPickItem, QuickPickOptions, DebugConfiguration, DebugConfigurationProvider, WorkspaceFolder, CancellationToken, ProviderResult} from 'vscode';
import {DebugProtocol} from 'vscode-debugprotocol';
import * as nls from 'vscode-nls';
import {exec} from 'child_process';
import { Exceptions, ExceptionConfigurations } from './exceptions';

const localize = nls.config({locale: process.env.VSCODE_NLS_CONFIG})();
var exceptions;

const DEFAULT_EXCEPTIONS: ExceptionConfigurations = {
    "System.Exception": "never",
    "System.SystemException": "never",
    "System.ArithmeticException": "never",
    "System.ArrayTypeMismatchException": "never",
    "System.DivideByZeroException": "never",
    "System.IndexOutOfRangeException": "never",
    "System.InvalidCastException": "never",
    "System.NullReferenceException": "never",
    "System.OutOfMemoryException": "never",
    "System.OverflowException": "never",
    "System.StackOverflowException": "never",
    "System.TypeInitializationException": "never"
};

export function activate(context: ExtensionContext) {
    context.subscriptions.push(debug.registerDebugConfigurationProvider("unity", new UnityDebugConfigurationProvider()));

    exceptions = new Exceptions(DEFAULT_EXCEPTIONS);
    window.registerTreeDataProvider("exceptions", exceptions);
    context.subscriptions.push(commands.registerCommand('exceptions.always', exception => exceptions.always(exception)));
    context.subscriptions.push(commands.registerCommand('exceptions.never', exception => exceptions.never(exception)));
    context.subscriptions.push(commands.registerCommand('exceptions.addEntry', t => exceptions.addEntry(t)));
	context.subscriptions.push(commands.registerCommand('attach.attachToDebugger', config => startSession(context, config)));
}

export function deactivate() {
}

class UnityDebugConfigurationProvider implements DebugConfigurationProvider {
	provideDebugConfigurations(folder: WorkspaceFolder | undefined, token?: CancellationToken): ProviderResult<DebugConfiguration[]> {
		const config = [
			{
				name: "Unity Editor",
				type: "unity",
				path: folder.uri.path + "/Library/EditorInstance.json",
				request: "launch"
			},
			{
				name: "Windows Player",
				type: "unity",
				request: "launch"
			},
			{
				name: "OSX Player",
				type: "unity",
				request: "launch"
			},
			{
				name: "Linux Player",
				type: "unity",
				request: "launch"
			},
			{
				name: "iOS Player",
				type: "unity",
				request: "launch"
			},
			{
				name: "Android Player",
				type: "unity",
				request: "launch"
			}
		];
		return config;
	}

	resolveDebugConfiguration(folder: WorkspaceFolder | undefined, debugConfiguration: DebugConfiguration, token?: CancellationToken): ProviderResult<DebugConfiguration> {
        if (debugConfiguration && !debugConfiguration.__exceptionOptions) {
            debugConfiguration.__exceptionOptions = exceptions.convertToExceptionOptionsDefault();
        }
		return debugConfiguration;
	}
}

function startSession(context: ExtensionContext, config: any) {
    let execCommand = "";
    if (process.platform !== 'win32')
        execCommand = "mono ";
    exec(execCommand + context.extensionPath + "/bin/UnityDebug.exe list", function (error, stdout, stderr) {
        const processes = [];
        const lines = stdout.split("\n");
        for (let i = 0; i < lines.length; i++) {
            if (lines[i]) {
                processes.push(lines[i]);
            }
        }
        if (processes.length == 0) {
            window.showErrorMessage("No Unity Process Found.");
        } else {
            window.showQuickPick(processes).then(function (string) {
                if (!string) {
                    return;
                }
                config.name    = string;
                config.request = "launch";
                config.type    = "unity";
                config.__exceptionOptions = exceptions.convertToExceptionOptionsDefault();
                debug.startDebugging(undefined, config)
                    .then(function (response) {
                            console.log(response);
                        },
                        function (error) {
                            console.log(error);
                        });
            });
        }
    });
}