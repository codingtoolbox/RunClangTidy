# RunClangTidy
**RunClangTidy** is a Visual Studio 2019 extension that allows to run clang-tidy from the solution explorer project or file context menu.  

![main](https://raw.githubusercontent.com/codingtoolbox/screenshots/master/runclangtidy-main.png)

Plugin can be configured via _Tools|Options|clang-tools_

![options](https://raw.githubusercontent.com/codingtoolbox/screenshots/master/runclangtidy-options.png)

  * _clang-tidy analysis options_: clang-tidy command line options
  
  * _clang-tidy location_: path to clang-tidy
  
  * _compile command__: command used in clang compilation database command string
  
Plugin adds following menu commands:

![menu](https://raw.githubusercontent.com/codingtoolbox/screenshots/master/runclangtidy-menu.png)

  * _Generate Compilation Database..._
  Generates clang compilation database (compile_commands.json) for the current project. 
  Compilation database is used for clang-tidy analysis and needs to be updated when file is added or removed from a project. 
  * _Run Clang-Tidy_
  Runs clang-tidy for the current project or file selection and prints output to Clang Output window.
  Clang-tidy warnings and errors are added to Error List window and highlighted in the editor pane:
  
![editor](https://raw.githubusercontent.com/codingtoolbox/screenshots/master/runclangitdy-editor.png)
