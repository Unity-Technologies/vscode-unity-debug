var vscode = require('vscode');
var exec = require('child_process').exec;

function activate(context) {

    var disposable = vscode.commands.registerCommand('attach.attachToDebugger', function () {

        var execCommand = "";

        if(process.platform !== 'win32')
            execCommand = "mono ";

        exec(execCommand + context.extensionPath + "/bin/UnityDebug.exe list", function(error, stdout, stderr) {
            var processes = [];

            var lines = stdout.split("\n");
            for(var i = 0; i < lines.length; i++)
            {
                if(lines[i])
                {
                    processes.push(lines[i]);
                }
            }

            if(processes.length == 0)
            {
                vscode.window.showErrorMessage("No Unity Process Found.");
            }else{
                vscode.window.showQuickPick(processes)
                .then(function(string){

                    if(!string)
                    {
                        return;
                    }

                    var config = {
                        name:string,
                        type:"unity",
                        request:"launch"
                    };

                    var call = vscode.commands.executeCommand("vscode.startDebug", config);
                    call.then(function(response){
                        console.log(response);
                    },function(error)
                    {
                        console.log(error);
                    });

                });
            }
        });
    });

    context.subscriptions.push(disposable);
}
exports.activate = activate;

function deactivate() {
}
exports.deactivate = deactivate;