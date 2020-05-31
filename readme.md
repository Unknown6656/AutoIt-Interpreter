![Banner image](old/artwork/banner.png)

# The AutoIt Interpreter

This repository contains _two_ AutoIt-Interpreters:

 1. [The "old" **AutoIt++ Interpiler** written in 2018.](old/readme.md)
    <br/>
    This interpiler compiles AutoIt++-Code (a superset of AutoIt3) into CIL-code, which can be exceuted on any platform supporting .NET.
    <br/>
    Truth be told, the interpiler is not super stable on non-Windows platforms. That is why I am working on a ...
 2. [The "new" (.net5-based) **AutoIt Interpreter**.](new/readme.md)
    <br/>
    This will primarily only support the AutoIt3-specification (without all the fancy AutoIt++-sh█t).


#### Comparison:

Category | [AutoIt++ Interpiler](old/readme.md)| [AutoIt Interpreter](new/readme.md)
---------|---------------------|--------------------
Supported Language | AutoIt++ | Strict AutoIt 3
Works as an ... | Interpiler:<br/>60% compiler, 30% interpreter, 10% magic | Interpreter
Written in | F#, C#|C#
Works on Windows | Yup | Hopefully
Works on Linux | Kinda | Hopefully
Works on MacOs | Kinda | Hopefully
Status | Beta-ish | Not yet developed


## Links

 - [**Old** Usage page](old/doc/usage.md)
 - [AutoIt++ Language reference](old/doc/language.md)
 - [AutoIt++ Syntax reference](old/doc/syntax.md)
 - [AutoIt++ Runtime behaviour](old/doc/runtime.md)
 - [AutoIt++ Syntax tree reference](old/doc/syntax-tree.md)
 - [Official AutoIt3 documentation](https://www.autoitscript.com/autoit3/docs/)

## Maintainer(s)

 - [@Unknown6656](https://github.com/Unknown6656)
 - ([@Zedly](https://github.com/Zedly) in assistive and advisory function)
