## Unity Debugger Extension for Visual Studio Code

Use Visual Studio Code to debug your Unity C# projects.

## Setup

- Open your Unity project folder in the Visual Studio Code.

- Select the debug view on the left and click the cogwheel.

![Debug View](Screenshots/vscode-debug-view.png)

- In the drop down list select “Unity Debugger”. If you do not have Unity Debugger in the list, then you already have a .vscode/Launch.json file in your project that you must delete first.

![Debugger List](Screenshots/vscode-debugger-list.png)

- You will now have a .vscode/Launch.json file in your Unity project folder and can select which Unity target you wish to debug.

![Debugger List](Screenshots/vscode-debugger-unity.png)

- All done. You can now debug your C# scripts in VS Code by setting a breakpoint in a C# script from your project, switching to the debug view and clicking the green triangle button to attach to Unity. Enter play mode in Unity and the breakpoint should hit in VS code.
