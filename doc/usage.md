# Usage
[go back](../readme.md)

This article is divided into the following sections:

 1) [How to build the interpiler](#building-the-interpiler)
 2) [How to use the interpiler to compile `.au3`-files](#using-the-interpiler)
 3) [How to run the compiled applications](#running-compiled-applications)

------

# Building the interpiler

## Prequisites: The `.NET Core SDK`

The project requires the v2.1-installation of the `.NET Core SDK` (or higher).

### Installing the `.NET Core SDK` using Visual Studio [Windows and MacOS]

The `.NET Core SDK` comes with default installation of Visual Studio, when the following options are selected inside the installer during the installation or upgrade process:

1) Click `Modify` on your Visual Studio Installation: ![Modify](images/installer-01.png)
2) Select the component `.NET Core runtime` in the category `Individual Components` > `.NET`: ![Modify](images/installer-02.png)
3) Select the components `.NET Compiler Platform SDK` and `C# and Visual Basic Roslyn Compilers` in the category `Individual Components` > `Compilers, build tools and runtimes`: ![Modify](images/installer-03.png)
 
### Installing the `.NET Core SDK` _without_ Visual Studio [Windows and MacOS]

You can downlowad the SDK via the following links:
 - [Windows](https://www.microsoft.com/net/learn/get-started/windows)
 - [MacOS](https://www.microsoft.com/net/learn/get-started/macos)

### Installing the `.NET Core SDK` on Linux

You can follow the instructions provided [here](https://www.microsoft.com/net/learn/get-started/linux/) for the Linux installation.

Often, the Linux installation requires you to install the Microsoft signature keys.<br/>
You can either refer to [these instructions on github](https://github.com/dotnet/core/blob/master/release-notes/download-archives/2.0.0-download.md) or [this microsoft-link](https://www.microsoft.com/net/learn/get-started/linux/ubuntu17-10).<br/>
On APT-based installation systems (mostly Debian, Ubuntu, LinuxMint, etc.), the commands usually look as follows:
```bash
curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg
sudo sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-artful-prod artful main" > /etc/apt/sources.list.d/dotnetdev.list'
```
Of course, the commands above vary depending on your system and architecture.

------

After having the linked key registration instructions you can install the `.NET Core SDK` as follows:

#### APT

```bash
$ sudo apt-get update
$ sudo apt-get install dotnet-sdk-2.1.0
```

#### DFN

```bash
$ sudo dfn update
$ sudo dfn install libunwind libicu compat -openssl10
$ sudo dfn install dotnet-sdk-2.1.0
```

#### YUM

```bash
$ sudo yum update
$ sudo yum install libunwind libicu
$ sudo yum install dotnet-sdk-2.1.0
```

## Building the interpiler project:

On Windows and MacOS systems with a Visual Studio installations, the build step is rather straight-forward:

1a) Download the Repository and open the Project by opening the `.sln`-file in VisualStudio
1b) **Or:** Go to `FILE` > `Open` > `From Source Control` > `GitHub` > `Clone` > `https://github.com/Unknown6656/AutoIt-Interpreter`
2) Press `Build Solution` or <kbd>Crtl</kbd> + <kbd>Shift</kbd> + <kbd>B</kbd>


On all major systems (Windows, Linux and MacOS), you can also use the command-line to build the project:

Switch to your downloaded copy of this repository and execute:
```bash
$ git clone https://github.com/Unknown6656/AutoIt-Interpreter
$ cd AutoIt-Interpreter
$ dotnet build
```

On Windows systems, you can alternatively use the `MSBuild` build engine as follows:
```batch
> git clone https://github.com/Unknown6656/AutoIt-Interpreter
> cd AutoIt-Interpreter
> msbuild
```

Congratulations, you have downloaded and build the interpiler!

# Using the interpiler

After having built the interpiler from source (via command-line or Visual Studio), use the following command to execute it as follows:

## Unix-based (command line)
```bash
$ cd bin
$ chmod a+rwx autoit.sh
$ ./autoit.sh <arguments>
```

## Windows (command line)
```batch
> cd bin
> autoit <arguments> 
```

## Windows and MacOS (Visual Studio)

1) Goto `Solution Explorer` > The `AutoItInterpreter` C# Project > `Properties` or <kbd>Alt</kbd> + <kbd>Enter</kbd> > `Debug`
2) Enter your command line arguments in the `Arguments` text box
3) Press <kbd>F5</kbd> or `Run` to start the interpiler with the set arguments

# Interpiler command line reference


# Running compiled applications

TODO

