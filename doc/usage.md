# Usage
[go back](../readme.md)

This article is divided into the following sections:

 1) [How to build the interpiler](#building-the-interpiler)
 2) [How to use the interpiler to compile `.au3`-files](#using-the-interpiler)
 3) [How to run the compiled applications](#running-compiled-applications)

------

# Building the interpiler

TODO


# Using the interpiler

TODO


# Running compiled applications

TODO





















-----------------

# /todo/




## Build

The project requires the v2.1-installation of the `.NET SDK`.

### Requirements [Windows and MacOS]

Download the `.NET SDK` either using the VisualStudio-Installer or via the following links:
 - [Windows](https://www.microsoft.com/net/learn/get-started/windows)
 - [MacOS](https://www.microsoft.com/net/learn/get-started/macos)

### Requirements [Linux]

Sometimes, the Linux installation requires you to install the Microsoft signature keys. Refer to [to these instructions on github](https://github.com/dotnet/core/blob/master/release-notes/download-archives/2.0.0-download.md) or [this microsoft-link](https://www.microsoft.com/net/learn/get-started/linux/ubuntu17-10).
Then install v2.1 or higher as follows:

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

### Build/Run [VisualStudio and VisualStudio Code]

1) Open the Project by opening the `.sln`-file in VisualStudio
2) Press `Run` or <kbd>F5</kbd>
3) ???
4) Profit!

### Build/Run [Command line]

Then switch to your downloaded copy of this repository and execute:
```bash
$ cd <location of the downloaded repository folder>
$ dotnet run
```

## Usage

After having built the ~interpreter~ interpiler from source (via cmd line or VS), use the following command to execute it as follows:
```bash
### linux/unix/mac:
$ cd CSAutoItInterpreter/bin
$ chmod a+rwx autoit.sh
$ ./autoit.sh <arguments>

### windows
$ cd CSAutoItInterpreter/bin
$ autoit <arguments> 
```