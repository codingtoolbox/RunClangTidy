# RunClangTidy
RunClangTidy is a Visual Studio 2019 extension that allows to run clang-tidy from the solution explorer project or file context menu.  Microsoft provides (better) clang-tidy integration functionality in Visual Studio 2019 16.4+, though they do not allow to specify custom clang-tidy tool. 
![main](https://raw.githubusercontent.com/codingtoolbox/screenshots/master/runclangtidy-main.png)
Plugin can be configured via Tools|Options|clang-tools
![options](https://raw.githubusercontent.com/codingtoolbox/screenshots/master/runclangtidy-options.png)
*clang-tidy analysis options: clang-tidy command line options
*clang-tidy location: path to clang-tidy
*compile command: command used for clang compilation database 
Plugin provides following menu commands:
![menu](https://raw.githubusercontent.com/codingtoolbox/screenshots/master/runclangtidy-menu.png)
*Generate Compilation Database...
Generates clang compilation database (compile_commands.json) for the current project. 
Compilation database is used for clang-tidy analysis and needs to be updated when file is added or removed from a project. 
*Run Clang-Tidy
Runs clang-tidy for the current project or file selection and prints output to Clang Output window.
Clang-tidy warnings and errors are added to Error List window and highlighted in the editor pane:
![editor](https://raw.githubusercontent.com/codingtoolbox/screenshots/master/runclangitdy-editor.png)

