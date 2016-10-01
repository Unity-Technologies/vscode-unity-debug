var vscode = require('vscode');
var exec = require('child_process').exec;

function activate(context) {

    var disposable = vscode.commands.registerCommand('extension.sayHello', function () {

        exec(context.extensionPath + "/bin/UnityDebug.exe list", function(error, stdout, stderr) {
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
                var pick = vscode.window.showQuickPick(processes);
                pick.then(function(string){

                    var config = {
                        name:string,
                        type:"unity",
                        request:"launch"
                    };

                    var call = vscode.commands.executeCommand("vscode.startDebug", config);
                    call.then(function(response){
                        console.log(response);
                    })
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