import {QuickPickItem, TreeDataProvider, Event, EventEmitter, TreeItem, ProviderResult, window, InputBoxOptions} from 'vscode';
import * as vscode from 'vscode';
import * as nls from 'vscode-nls';
import {DebugProtocol} from 'vscode-debugprotocol';


//----- configureExceptions ---------------------------------------------------------------------------------------------------
// we store the exception configuration in the workspace or user settings as
export type ExceptionConfigurations = { [exception: string]: DebugProtocol.ExceptionBreakMode; };

class ExceptionItem implements QuickPickItem {
    label: string;
    description: string;
    model: DebugProtocol.ExceptionOptions
}

enum ExceptionMode {
    Always,
    Never,
    Unhandled
}

export class Exceptions implements TreeDataProvider<ExceptionBreakpoints> {
    private _onDidChangeTreeData: EventEmitter<ExceptionBreakpoints | undefined> = new EventEmitter<ExceptionBreakpoints | undefined>();
    readonly onDidChangeTreeData?: Event<ExceptionBreakpoints | undefined> = this._onDidChangeTreeData.event;

    constructor(private exceptions: ExceptionConfigurations) {
    }

    public always(element: ExceptionBreakpoints) {
        this.exceptions[element.name] = "always";
        element.setMode(ExceptionMode.Always);
        this._onDidChangeTreeData.fire(element);
        this.setExceptionBreakpoints(this.exceptions);
    }

    public never(element: ExceptionBreakpoints) {
        this.exceptions[element.name] = "never";
        element.setMode(ExceptionMode.Never);
        this._onDidChangeTreeData.fire(element);
        this.setExceptionBreakpoints(this.exceptions);
    }

    public unhandled(element: ExceptionBreakpoints) {
        this.exceptions[element.name] = "unhandled";
        element.setMode(ExceptionMode.Unhandled);
        this._onDidChangeTreeData.fire(element);
        this.setExceptionBreakpoints(this.exceptions);
    }

    public addEntry(t: any) {
        let options: InputBoxOptions = {
            placeHolder: "(Namespace.ExceptionName)"
        }

        window.showInputBox(options).then(value => {
            if (!value) {
                return;
            }
            this.exceptions[value] = "never";
            this._onDidChangeTreeData.fire(null);
        });
    }

    setExceptionBreakpoints(exceptionConfigs: ExceptionConfigurations) {

        const args: DebugProtocol.SetExceptionBreakpointsArguments = {
            filters: [],
            exceptionOptions: this.exceptionConfigurationToExceptionOptions(exceptionConfigs)
        };

        vscode.debug.activeDebugSession.customRequest('setExceptionBreakpoints', args).then<DebugProtocol.SetExceptionBreakpointsResponse>();
    }

    public convertToExceptionOptionsDefault(): DebugProtocol.ExceptionOptions[] {
        return this.exceptionConfigurationToExceptionOptions(this.exceptions);
    }

    public exceptionConfigurationToExceptionOptions(exceptionConfigs: ExceptionConfigurations): DebugProtocol.ExceptionOptions[] {
        const exceptionItems: DebugProtocol.ExceptionOptions[] = [];
        for (const exception in exceptionConfigs) {
            exceptionItems.push({
                path: [{names: [exception]}],
                breakMode: exceptionConfigs[exception]
            });
        }
        return exceptionItems;
    }

    public exceptionConfigurationToExceptionMode(exceptionConfig: string): ExceptionMode {
        switch (exceptionConfig)
        {
            case "always": return ExceptionMode.Always;
            case "never": return ExceptionMode.Never;
            case "unhandled": return ExceptionMode.Unhandled;
            default: throw new Error(exceptionConfig + ": is not a known exceptionConfig");
        }
    }

    getTreeItem(element: ExceptionBreakpoints): TreeItem | Thenable<TreeItem> {
        return element;
    }

    getChildren(element?: ExceptionBreakpoints): ProviderResult<ExceptionBreakpoints[]> {
        if (!this.exceptions) {
            window.showInformationMessage('No exception found');
            return Promise.resolve([]);
        }

        return new Promise(resolve => {
            if (element) {
                const exceptionItems: ExceptionBreakpoints[] = [];
                for (const exception in this.exceptions) {
                    exceptionItems.push(new ExceptionBreakpoints(exception, this.exceptionConfigurationToExceptionMode(this.exceptions[exception])));
                }
                resolve(exceptionItems);
            } else {
                const exceptionItems: ExceptionBreakpoints[] = [];
                for (const exception in this.exceptions) {
                    exceptionItems.push(new ExceptionBreakpoints(exception, this.exceptionConfigurationToExceptionMode(this.exceptions[exception])));
                }
                resolve(exceptionItems);
            }
        });
    }
    getParent?(element: ExceptionBreakpoints): ProviderResult<ExceptionBreakpoints> {
        return null;
    }
}

class ExceptionBreakpoints extends TreeItem {
    constructor(
        public name: string,
        public mode: ExceptionMode,
    ) {
        super(mode + " : " + name);
        this.setMode(mode);
    }

    setMode(mode: ExceptionMode) {
        this.mode = mode;
        this.label = this.tooltip = `[${this.mode == ExceptionMode.Always ? "✔" : "✖"}] ${this.name}`;
    }

    contextValue = 'exception';
}