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

export class Exceptions implements TreeDataProvider<Exception> {
    private _onDidChangeTreeData: EventEmitter<Exception | undefined> = new EventEmitter<Exception | undefined>();
    readonly onDidChangeTreeData?: Event<Exception | undefined> = this._onDidChangeTreeData.event;

    constructor(private exceptions: ExceptionConfigurations) {
    }

    public always(element: Exception) {
        this.exceptions[element.name] = "always";
        element.mode = "always";
        this._onDidChangeTreeData.fire(element);
        this.setExceptionBreakpoints(this.exceptions);
    }

    public never(element: Exception) {
        this.exceptions[element.name] = "never";
        element.mode = "never";
        this._onDidChangeTreeData.fire(element);
        this.setExceptionBreakpoints(this.exceptions);
    }

    public unhandled(element: Exception) {
        this.exceptions[element.name] = "unhandled";
        element.mode = "unhandled";
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

    setExceptionBreakpoints(model: ExceptionConfigurations) {

        const args: DebugProtocol.SetExceptionBreakpointsArguments = {
            filters: [],
            exceptionOptions: this.convertToExceptionOptions(model)
        };

        vscode.debug.activeDebugSession.customRequest('setExceptionBreakpoints', args).then<DebugProtocol.SetExceptionBreakpointsResponse>();
    }

    public convertToExceptionOptionsDefault(): DebugProtocol.ExceptionOptions[] {
        return this.convertToExceptionOptions(this.exceptions);
    }

    public convertToExceptionOptions(model: ExceptionConfigurations): DebugProtocol.ExceptionOptions[] {
        const exceptionItems: DebugProtocol.ExceptionOptions[] = [];
        for (const exception in model) {
            exceptionItems.push({
                path: [{names: [exception]}],
                breakMode: model[exception]
            });
        }
        return exceptionItems;
    }

    getTreeItem(element: Exception): TreeItem | Thenable<TreeItem> {
        return element;
    }
    getChildren(element?: Exception): ProviderResult<Exception[]> {
        if (!this.exceptions) {
            window.showInformationMessage('No exception found');
            return Promise.resolve([]);
        }

        return new Promise(resolve => {
            if (element) {
                const exceptionItems: Exception[] = [];
                for (const exception in this.exceptions) {
                    exceptionItems.push(new Exception(exception, this.exceptions[exception]));
                }
                resolve(exceptionItems);
            } else {
                const exceptionItems: Exception[] = [];
                for (const exception in this.exceptions) {
                    exceptionItems.push(new Exception(exception, this.exceptions[exception]));
                }
                resolve(exceptionItems);
            }
        });
    }
    getParent?(element: Exception): ProviderResult<Exception> {
        return null;
    }
}

class Exception extends TreeItem {
    constructor(
        public name: string,
        public mode: string,
    ) {
        super(mode + " : " + name);
    }

    get label(): string {
        return `[${this.mode == "always" ? "✔" : "✖"}] ${this.name}`;
    }

    set label(newLabel: string) {
        this.name = this.name;
    }

    get tooltip(): string {
        return `[${this.mode == "always" ? "✔" : "✖"}] ${this.name}`;
    }

    contextValue = 'exception';
}