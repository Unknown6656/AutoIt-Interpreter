![issues](https://img.shields.io/github/issues/Unknown6656/AutoIt-Interpreter)
![forks](https://img.shields.io/github/forks/Unknown6656/AutoIt-Interpreter)
![stars](https://img.shields.io/github/stars/Unknown6656/AutoIt-Interpreter)
![license](https://img.shields.io/github/license/Unknown6656/AutoIt-Interpreter)

![Banner image](new/artwork/banner.png)

# Platform-Independent AutoIt3 Intepreter

AutoIt is a traditionally Windows-only scripting language based on the Visual Basic syntax.
The aim of this repository is to provide an AutoIt3 interpteter for other platforms, such as Linux/Unix and MacOS.

This repository contains _two_ AutoIt-Interpreters:

 1. [The newer (.NET5-based) **extensible AutoIt3 Interpreter**.](new/readme.md)
    <br/>
    This Interpreter is based purly on .NET5-conform C#/F# code and primarily supports the AutoIt3-spcification.
    The Interpreter does support a plugin-based extension system in order to provide a custom syntax or framework functionality to the interpreter.

 1. [The "old" **AutoIt++ Interpiler** written in 2018 (discontinued).](old/readme.md)
    <br/>
    This interpiler compiles AutoIt++-Code (a superset of AutoIt3) into CIL-code, which can be exceuted on any platform supporting .NET.
    <br/>
    Truth be told, the interpiler is not super stable on non-Windows platforms, and has therefore been discontinued.


#### Comparison:

Category | [new AutoIt3 Interpreter](new/readme.md) | [old AutoIt++ Interpiler](old/readme.md)
---------|---------------------|--------------------
Supported Language| Strict AutoIt 3<br/>with extension support | AutoIt 3<br/>AutoIt++ 
Works as an ... | Interpreter | Interpiler/Transpiler:<br/>60% compiler, 30% interpreter, 10% magic 
Written in | C#, F# | C#, F#
Target Framework | .NET 5 and newer | .NET Core 2.1
Works on Windows | Hell yeah | Yup
Works on Linux | Not yet tested | Kinda
Works on MacOs | Not yet tested | Kinda
Status | In development | Beta-ish


## Links

 - [TODO]
 - [Official AutoIt3 documentation](https://www.autoitscript.com/autoit3/docs/)

### Old Links

 - [**Old** Usage page](old/doc/usage.md)
 - [AutoIt++ Language reference](old/doc/language.md)
 - [AutoIt++ Syntax reference](old/doc/syntax.md)
 - [AutoIt++ Runtime behaviour](old/doc/runtime.md)
 - [AutoIt++ Syntax tree reference](old/doc/syntax-tree.md)

## Maintainer(s)

 - [@Unknown6656](https://github.com/Unknown6656)
 - ([@Zedly](https://github.com/Zedly) in assistive and advisory function)
