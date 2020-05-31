<img alt="AutoIt++ icon" src="images/icon-1024.png" height="200"/>

# A rough AutoIt++ custom-flavored syntax reference
[go back](../readme.md)

The AutoIt3-syntax is fully compatible with the AutoIt++ dialect, meaning that the [official syntax reference](https://www.autoitscript.com/autoit3/docs/) applies to AutoIt++.
<br/>
This article highlights the most important **differences** between AutoIt3's and AutoIt++'s syntaxes. It is therefore divided in the following sections:

1) [AutoIt++ operators](#autoit-operators)
2) [AutoIt++ string interpolation](#autoit-string-interpolation)
3) [One-liner functions](#one-liner-functions)
4) [P/Invoke functions](#pinvoke-functions)
5) [λ-Expressions](#λ-expressions)
6) [`new`-exprssions](#new-expressions)
7) [Inline C#-code](#inline-c-code)
8) [`ifn't`](#ifnt)
9) [The `#OnAutoItExitRegister` directive](#onautoitexitregister-directive)
10) [The `#include` directive](#include-directive)
11) [The `#sign` directive](#sign-directive)
12) [The `#using` directive](#using-directive)
13) [The `#debugattach`, `#debugprint` and `#breakpoint` directives](#debugattach-debugprint-and-breakpoint-directives)
14) [The `#note`, `#warning` and `#error` directives](#note-warning-and-error-directives)
15) [`enum` declarations](#enum-declarations)

<br/>
For a more detailed and formal syntax description of the AutoIt++ dialect, please refer to the [AutoIt++ syntax tree reference](syntax-tree.md).
<br/>
You can also refer to the [AutoIt++ runtime behaviour description](runtime.md) and the [AutoIt++ language reference](language.md).

------

# AutoIt++ operators

As AutoIt3 has only a small amount of operators compared to languages like C#, C++ or F# (in which you can even define new operators!),
the AutoIt++ variant adds the following operators in order to improve code compactness and increase the developer's productivity:

## New logical operators: `Nor`, `Nand`, `Xor`, `Nxor` (a.k.a. `Xnor`), `Impl` and `!`

To extend the set of the logical (boolean) operators `And` and `Or`, the operators `Nor`, `Nand`, `Xor`, `Nxor`, `Impl`, `!` have been added.

The alias `Xnor` for the operator `Nxor` has been introduced in order to fullfill a personal request of [@Zedly](https://github.com/Zedly). In this article, however, the operator will be
addressed as `Nxor`.

The operator `!` is the short variant of the prfix (unary) operator `Not`. The other new operators are binary infix ones which can be used e.g. as follows:
```autoit
$foo = true
$bar = $test <> 7.5

If ($foo Xor $bar) Nand IsDeclared("test") Then
    ; ....
EndIf
```

The truth table for the logical operators is defined as follows:

| `A`   | `B`   | `A And B` | `A Nand B` | `A Or B` | `A Nor B` | `A Xor B` | `A Nxor B` | `A Impl B` |
|:-----:|:-----:|:---------:|:----------:|:--------:|:---------:|:---------:|:----------:|:----------:|
|`false`|`false`| `false`   | `true`     | `false`  |  `true`   | `false`   | `true`     | `true`     |
|`false`| `true`| `false`   | `true`     |  `true`  | `false`   |  `true`   | `false`    | `true`     |
| `true`|`false`| `false`   | `true`     |  `true`  | `false`   |  `true`   | `false`    | `false`    |
| `true`| `true`|  `true`   | `false`    |  `true`  | `false`   | `false`   | `true`     | `true`     |

## Arithmetic operator `\`

The operator `\` can be compared to VisualBasic.NET's `\`-operator, which represents an integer division, meaning that the following epxressions are translated thus:
```autoit
10 / 3      ; 3.33333333333333333333333...
10 \ 3      ; 3
```
Generally, any expression
```autoit
$a = $b \ $c
```
could be translated as
```autoit
$a = floor(floor($b) / floor($c))
```

## Bitwise arithmetical operators

A whole set of bitwise arithmetical binary infix operators have been introduced with AutoIt++:

 - `&&`: **Bitwise And**
 - `~&&`: **Bitwise Nand**
 - `||`: **Bitwise Or**
 - `~||`: **Bitwise Nor**
 - `^^`: **Bitwise Xor**
 - `~^^`: **Bitwise Nxor**
 - `~`: **Bitwise Not** (Unary)
 - `<<`: **Bitwise left-shift**
 - `>>`: **Bitwise right-shift**
 - `<<<`: **Bitwise rotate left**
 - `>>>`: **Bitwise rotate right**

They all conform with the usual standards known from other programming languages and their individual functions can be checked inside [this Wikipedia article](https://en.wikipedia.org/wiki/Bitwise_operation).

## String operators

The following string operators have been introduced with AutoIt++:

### String index and substring using `@`, `@|`, `@..` and `@|..`

The binary infix operators `@` and `@|` return the character at the _following_ position in the _leading_ string, in other words:
<br/>
The expression `a @ b` represents the **`b`-th character in the string `a`**. The difference between `@` and `@|` is that `@` uses zero-based indices and `@|` uses one-based ones.

The expression `a @ b .. c` represents the **substring of `a` starting at the `b`-th character** and having a **length of `c`**. The index `b` is zero-based.
<br/>
Similarly to the one-based indexing operator `@|`, the substring operator `@| ..` also uses one-based indices.


An example of the indexing and substring operators:
```autoit
$text = "Hello, World!"

$str1 = $text @ 0       ; "H"
$str2 = $text @| 1..4   ; "Hell"
$str3 = $text @ 2+5..5  ; "World"
```

To keep compatibility with AutoIt's indexing operator `[ ... ]`, strings can also be indexed using the `[ ... ]`-notation. The index is zero-based.

### String and array length using `#`

A length of a string or an array can be taken using the postfix operator `#` as follows:
```autoit
dim $arr[5] = [ 0, 1, 2, 3, 4 ]
$str = "my string"

$l_arr = $arr# ; has the value 5
$l_str = $str# ; has the value 9
```

## Unary Operators `#`, `!` and `~`

As partly mentioned in previous paragraphs, AutoIt++ introduces the following unary operators:

 - `#`: **String/Array Length** (left-associative, postfix)
 - `!`: **Logical (Boolean) Not** (right-associative, prefix)
 - `~`: **Bitwise Not** (right-associative, prefix)

The arithmetic unary operators `+` (Identity) and `-` (Negation) naturally do also exist and work as is being expected by the AutoIt3 specification and mathematical standards.

## Unary Byte Dereferencing Operator `°`

To dereference a pointer variable to its containing 1-byte value, use the unary prefix operator `°`:
```autoit
Func Malloc As "void* malloc(int)" From "glibc.so"
Func Free As "int free(void*)" From "glibc.so"


$region = Malloc(1024)          ; allocate 1024 bytes and get a pointer to the memory region
$first_byte = °$region          ; get the first byte in the region
$second_byte = °($region + 1)   ; get the second byte in the region

ConsoleWrite($"The first two bytes in the region '$region' are:\n")
ConsoleWrite($"\t$first_byte and $second_byte\n")

Free($region)                   ; Free the memory region
```

It is also possible to dereference constant pointers, e.g:
```autoit
$some_value = °0x140a835f
$null_deref = °0
$null_deref = °Null
```
It shall be noted, that a `null` or `0` dereferentiation results always in a fatal exception and programm termination.
So does any other invalid pointer-operation.

The dereferencing operator can also be used for pointer assignments:
```autoit
°$pointer = 0xff
```
The value will be truncated to its least significant byte if the value is greater than `0xff` (255).

AutoIt++ also permits the assignment of a value to a pointer returned from a function:
```autoit
°func() = 42
```

## Ternary 'inline-if' operator `... ? ... : ...`

TODO

## Assignment operators

TODO

## Operator Precedence

The following displays a list of operator precendence groups in AutoIt++. The top-most row represents operators or constructs with the highest precedence. Inside the row, the operators are parsed and matched from left to right.

| Operator(s) / Construct(s)                                        | Associativity        |
|-------------------------------------------------------------------|----------------------|
| `( ... )` parenthesizing                                          | Unary                |
| `func(...)`, `@macro`, `$variable`, literals, numbers and strings |                      |
| `[]` array access                                                 | (Left)               |
| `()` λ function call                                              | (Left)               |
| `.` "dot" member access                                           | (Left)               |
| `°`                                                               | (Right) Unary prefix |
| `~`                                                               | (Right) Unary prefix |
| `-`, `+`                                                          | (Right) Unary prefix |
| `!`                                                               | (Right) Unary prefix |
| `#`                                                               | (Left) Unary postfix |
| `^`                                                               | Right                |
| `%`, `/`, `\`, `*`                                                | Left                 |
| `-`, `+`                                                          | Left                 |
| `<<`, `>>`                                                        | Left                 |
| `<<<`, `>>>`                                                      | Left                 |
| `~&&`, `&&`                                                       | Left                 |
| `~^^`, `^^`                                                       | Left                 |
| <code>~&#124;&#124;</code>, <code>&#124;&#124;</code>             | Left                 |
| <code>@&#124;</code>, `@`                                         | Left                 |
| <code>@&#124; ..</code>, `@ ..`                                   | Left (Ternary)       |
| `&`                                                               | Left                 |
| `<>`, `==`, `=`                                                   | Left                 |
| `<=`, `<`, `>=`, `>`                                              | Left                 |
| `Nand`, `And`                                                     | Left                 |
| `Nxor`, `Xor`                                                     | Left                 |
| `Nor`, `Or`                                                       | Left                 |
| `Impl`                                                            | Left                 |

# AutoIt++ string interpolation

AutoIt++ allows the usage of so-called _interpolated strings_, which are known from other languages such as PHP, C#, JavaScript, Bash, Python and many others more.
<br/>
Interpolated strings have a leading dollar symbol (`$`) and can interpolate variables in their regular notation (`$`-prefixed) or macros (`@`-prefixed).
To escape any dollar- or at-characters inside an interpolated string, it must be prefixed with a backslash (`\`). To escape a backslash in an interpolated string, one must type to backslashes (`\\`).
A backslash can also be used as control-sequence prefix for the following entities:

| Expression | Translation                                                              |
|------------|--------------------------------------------------------------------------|
| `\"`       | The ASCII character `"` (`0x22`)                                         |
| `\r`       | The control-character `CR` (carrige return, `0x0D`)                      |
| `\n`       | The control-character `LF` (line feed, `0x0A`)                           |
| `\t`       | The control-character `HT` (horizontal tab, `0x09`)                      |
| `\v`       | The control-character `VT` (vertical tab, `0x0B`)                        |
| `\b`       | The control-character `BS` (backspace, `0x08`)                           |
| `\a`       | The control-character `BEL` (bell, `0x07`)                               |
| `\f`       | The control-character `FF` (form format, `0x0C`)                         |
| `\d`       | The control-character `DEL` (delete, `0x7F`)                             |
| `\0`       | The control-character `NUL` (null, `0x00`)                               |
| `\\`       | The ASCII character `\` (`0x5C`)                                         |
| `\$`       | The ASCII character `$` (`0x24`)                                         |
| `\@`       | The ASCII character `@` (`0x40`)                                         |
| `\x--`     | The ASCII character represented by the two hexadecimal digits `--`       |
| `\u----`   | The UNICODE character represented by the four hexadecimal digits `----`  |

Usage example:
```autoit
$foo = 42
$bar = "World"

$result = $"Hello, $bar!\nI have a variable named \"\$foo\", which has the value \"$foo\" <> @year."
; $result now has the value:
;   Hello, Bar!
;   I have a variable named "$foo", which has the value "42" <> 2018
```

More general information about interpolated strings can be found in [this Wikipedia article](https://en.wikipedia.org/wiki/String_interpolation).
<br/>
More general information about the ASCII control characters can be found in [this Wikipedia article](https://en.wikipedia.org/wiki/Control_character#In_ASCII).

# One-liner functions

"Simple" functions often have the following form:
```autoit
Func name( <parameters> )
    <single expression>
EndFunc
```
The single expresion can optionally be preceded by an `Return`-keyword.

To reduce the bloat created by such tiny functions, AutoIt++ introduces the one-line function syntax as follows:
```autoit
Func name( <parameters> ) -> <expression>
```

The following examples show how the syntax can be used:
```autoit
Func PrintAreaOfCircle($radius)
    ConsoleWriteLine($"A circle with the radius $radius has the area " & ($radius ^ 2 * @PI))
EndFunc

Func SomethingComplex($a, $b, $c = @E)
    Return $c ^ (Sin($a) * $b)
EndFunc

; are now equivalent to:

Func PrintAreaOfCircle($radius) -> ConsoleWriteLine($"A circle with the radius $radius has the area " & ($radius ^ 2 * @PI))

Func SomethingComplex($a, $b, $c = @E) -> $c ^ (Sin($a) * $b)
```

# P/Invoke functions

P/Invoke (also known as _"Platform Invocation Services"_) is a feature for .NET languages to call unmanaged code from managed languages, e.g. call C++ from C#.
<br/>
AutoIt++ also has the possiblity to perfom P/Invoke-calls using the [AutoIt3-conform function `DllCall`](https://www.autoitscript.com/autoit3/docs/functions/DllCall.htm), however, its usage can be sometimes a bit irksome.
To counter that, AutoIt++ introduces the ability to declare P/Invoke function signatures as follows:
```autoit
Func <name> As "<signature>" From "<library>"
```

**Example:**
<br/>
To call the function [`[kernel32.dll] BOOL WINAPI Beep(DWORD dwFreq, DWORD dwDuration)`](https://msdn.microsoft.com/en-us/library/windows/desktop/ms679277(v=vs.85).aspx), one can declare its signature in AutoIt++ as follows:
```autoit
Func MyBeep As "int Beep(int, int)" From "kernel32.dll"
```
It can be used just like any function:
```autoit
Beep(440, 2000)     ; The built-in 'Beep'-function
MyBeep(262, 1000)   ; The P/Invoke 'Beep'-function
; The two calls play the following beeps:
;   440Hz (A4) for 2 seconds
;   262Hz (C4) for 1 second
```
The call of `MyBeep` would be equivalent to the following code:
```autoit
DllCall("kernel32.dll", "BOOL", "Beep", "DWORD", 262, "DWORD", 1000)
```

A transalation of C++ to AutoIt3- or AutoIt++-types can be found in [this AutoIt3 documentation article](https://www.autoitscript.com/autoit3/docs/functions/DllCall.htm).
However, AutoIt++ also allows the following type aliases _(case-insensitive)_:

| AutoIt++ type string(s)                                                                                                       | C++ equivalent(s)                             | C# equivalent(s)                              | Size          |
|-------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------|-----------------------------------------------|---------------|
| `void`, `none`, `unit`                                                                                                        | `void`                                        | `void`                                        | 0 B           |
| `byte`, `boolean`                                                                                                             | (`unsigend`) `char`, `BOOLEAN`                | `byte`, `sbyte`                               | 1 B           |
| `word`, `short`, `ushort                                                                                                      | `wchar`, `WORD`, (`unsigend`) `short`         | `char`, `short`, `ushort`                     | 2 B           |
| `dword`, `int`, `uint`, `long`, `ulong`, `bool`,                                                                              | `DWORD`, (`unsigend`) `int`, `BOOL`           | `int`, `uint`                                 | 4 B           |
| `int64`, `uint64`, `hresult`                                                                                                  | (`unsigend`) `long long`, `HRESULT`           | `long`, `ulong`                               | 8 B           |
| `ptr`, `hwnd`, `void*`, `lparam`, `wparam`, `dword_ptr`, `lresult`, `handle`, `int_ptr`, `uint_ptr`, `long_ptr`, `ulong_ptr`  | `HWND`, `LPARAM`, `LRESULT`, `WPARAM`, `-*`   | `-*`, `IntPtr`, `UIntPtr`, `delegate`         | 4 or 8 B      |
| `float`, `single`, `float32`                                                                                                  | `float`                                       | `float`                                       | 4 B           |
| `double`, `float64`                                                                                                           | `double`                                      | `double`                                      | 8 B           |
| `decimal`, `float128`                                                                                                         | `__float128`, `float128` (Boost Library)      | `decimal`                                     | 16 B          |
| `str`, `astr`, `string`, `astring`                                                                                            | `char*`, `LPSTR`, `LPCSTR`                    | `string`, `StringBuilder`, `byte*`, `byte[]`  | 2x B (+4 B)   |
| `wstr`, `w_str`, `wstring`, `w_string`                                                                                        | `wchar*`, `LPWSTR`, `LPWCSTR`, `LPCTSTR`      | `string`, `StringBuilder`, `char*`, `char[]`  | 1x B (+4 B)   |
| `dynamic`, `var`, `struct`                                                                                                    | `struct`, `union`, `class`                    | `struct`, `enum`, `unmanaged`, `dynamic`      | ---           |

More general information about Platform Invocation Services can be found in [this Wikipedia article](https://en.wikipedia.org/wiki/Platform_Invocation_Services).

# λ-Expressions

AutoIt++ introduces a restriced set of syntax functionalities, which allows the creation of anonymous or λ-functions.

## Anonymous λ-functions

λ-functions can be created as follows:
```
<variable> '=' 'Func' '(' <opt_parameters> ')'
                    <statements>
               'EndFunc'
```
The current syntax requires λ-functions to be assigned directly to a variable or an array-element outside a declaration expression.
<br/>
This means, that the following expressions would be currently **invalid**:
```autoit
; λ-assignments must not be used during declaration-statements
Global Const $MY_FUNC = Func($a, $b)
                            Return $a * Sin($b)
                        EndFunc

; λ-expressions cannot be used inside 'nested' expressions
$result = (Func($a)
              Return $a ^ 2
           EndFunc)(5)
```

However, the following expressions **are valid**:
```autoit
$my_functions[3] = Func($a, $b)
                       Return $a * Sin($b)
                   EndFunc

$get_tau = Func()
               Return @PI * 2
           EndFunc
```

## λ-assignments

AutoIt also permits the assignment of existing functions (including built-in ones and P/Invoke-definitions) as λ-expressions. This, however, must also be only used as direct assignment:
```autoit
Func GetTau()
    Return @pi * 2
EndFunc

Func beep As "int Beep(int, int)" From "kernel32.dll"


$my_func = GetTau
$functions[7][2] = beep
```

## λ-execution

To execute any λ-assigned or -defined functions, one uses the same syntax notation as with 'regular' functions:
```autoit
$get_tau = Func()
               Return @PI * 2
           EndFunc


$tau = $get_tau()
$pi = ($get_tau() / 2)
```

It also works with arry-element-assigned λ-expressions or nested expressions:
```autoit
Func beep As "int Beep(int, int)" From "kernel32.dll"


$functions[7][2] = beep
$result = $functions[7][2](440, 1000)
```

# λ-currying

Given e.g. a function
<img src="http://latex.codecogs.com/svg.latex?F%20%3A%20A%20*%20B%20*%20C%20%5Crightarrow%20D"/>
it could be interpreted as it curryable form
<img src="http://latex.codecogs.com/svg.latex?F%20%3A%20A%20%5Crightarrow%20B%20%5Crightarrow%20C%20%5Crightarrow%20D"/>.
<br/>
If a parameter <img src="http://latex.codecogs.com/svg.latex?a%20%5Cin%20A"/> is passed to the function <img src="http://latex.codecogs.com/svg.latex?F"/>, the resulting "curried" function
has the following form: <img src="http://latex.codecogs.com/svg.latex?F%27%20%3A%20B%20%5Crightarrow%20C%20%5Crightarrow%20D"/>.

This concept has been transfered to λ-calls in AutoIt++: ommitting any mandataory parameter inside a λ-call leads to λ-currying of the associated function.

```autoit
$f = Func($a, $b, $c, $d)
         ConsoleWriteLine($"λ called with ($a, $b, $c, $d).")
     EndFunc

$g = $f(42)                 ; $g is now a function accepting the parameters $b, $c and $d
$h = $g("test", -0.5)       ; $h is now a function accepting the parameter $d

$x = $h(true)               ; prints 'λ called with (42, test, -0.5, true).'
$y = $h(-1.7)               ; prints 'λ called with (42, test, -0.5, -1.7).'
$z = $g(0, "foo", "bar")    ; prints 'λ called with (42, 0, foo, bar).'
```

If the function or its curried version contains any optional argument, the function is executed in the original sense, as soon as all mandatory arguments are passed:
```autoit
$f = Func($a, $b, $c = 9)
         ConsoleWriteLine($"λ called with ($a, $b, $c).")
     EndFunc

$g = $f(42)                 ; $g is now a function accepting the parameters $b and [$c]

$x = $g(-1)                 ; prints 'λ called with (42, -1, 9).'
$y = $g(0, 0.2)             ; prints 'λ called with (42, 0, 0.2).'
```

More information about currying can be found in [this Wikipedia article](https://en.wikipedia.org/wiki/Currying)

# `new`-Exprssions

Arrays can be initializized using the following AutoIt3-compatible syntax:
```autoit
Dim $array[5] = [ 1, 2, 3, 4, 5 ]
Dim $matrix[2][2] = [ [ 1, 0 ], [ 0, -1 ] ]
```

AutoIt++ introduces the following syntax to allow 'anonymous'- or 'inline'-initialization of arrays:
```autoit
$array = new{ 1, 2, 3, 4, 5 }
$matrix = new{ { 1, 0 }, { 0, -1 } }
```
The syntax can also be used inside any expression, e.g.:
```autoit
$func = Sin((new { 0, 42, @PI })[2])
;     = Sin(@PI)
;     = 0
```

Opposed to the AutoIt3-compatible array initialization syntax, `new`-expressions do not require that nested arrays' dimensions are distinct between the elements in themselves.
This means, that the following code is valid:
```autoit
$jagged = new { { 1, 2, 3 }, { 4, 5 }, { 6 } }
```
However, due to the internal storage method of arrays, invalid dimensions could be displayed. The array dimensions are determined by the first element, which means that the following
arrays have different dimensions from the runtime's point of view:
```autoit
$arr1 = new { { 1, 2, 3 }, { 4, 5 }, { 6 } }
$arr2 = new { { 1 }, { 2, 3 }, { 4, 5, 6 } }

; arr1 has the dimensions 3 x 3
; arr2 has the dimensions 3 x 1
```

This could result in some semantic data loss when using the `ReDim`-statement on arrays created with the `new`-expression.
<br/>
Please refer to [the runime behaviour reference](runtime.md) for mor information.

# Inline C#-Code

AutoIt++ allows the usage of C# code inside AutoIt++ code. The C# code must start and end with the following tags:
```autoit
; AutoIt++ code

#cs[csharp]

// C# code

#ce[csharp]

; AutoIt++ code
```
Do please note, that AutoIt comments (`; ...`) are invalid in C#. Comments start with `// ...` or are inclosed in `/* ... */`.<br/>
To access variables and macros from C#, use the following notation:
```csharp
__vars["name"]      // represents $name
__macros["name"]    // represents @name
```

To include a .NET namespace and use it inside an inline-C# code snippet, use the following directive:
```autoit
using[csharp] ...
```
where `...` is the name of the namespace, e.g. `System.Data` or `System.Security.Cryptography`.

Use the directive `#using <...>` to include a .NET library. For more information about the `#using`-directive [jump to this section](#using-directive).

**NOTE: The usage of inline-C#-code is considered unsafe, and therefore needs the compiler switch `-u` or `--unsafe` to compile.**

# `ifn't`

<img src="images/ifnt.jpg" height="200"/><br/>
[@Zedly](https://github.com/Zedly)

# `#OnAutoItExitRegister`-directive

Complementary to the directive `#OnAutoItStartRegister "..."`, AutoIt++ introduces the directive `#OnAutoItExitRegister "..."`.

TODO

# `#include`-directive

The `#include` directive now supports paths passed in single quotes, double quotes and angled brackets (`<` and `>`).
<br/>
The AutoIt++ include path resolver has multiple improvements compared to its AutoIt3-counterpart:

 - It autocompletes the file extension `.au3`, `.au2` or `.au`, if it has been omitted on local paths (not web-paths)
 - It prefers completely valid paths to those with an supplemented file extension
 - It does not differentiate between backslashes (`\`) and forward slashes (`/`) in a path.
 - It does support all major URI formats:<br/>
   `file://`, `http://`, `https://`, `ftp://`, `ftps://`, `sftp://` and `ssh://`
 - `smb://` or `smbd://` paths are currently only supported through mounting points or their UNC-notation
 - Relative web-paths are (partly) supported
 - It prefers same-named files in the source folder compared to the include directories

If any authentification has to be passed to an `ftp`, `ftps`, `sftp` or `ssh` url, the convention is the following:
```
protocol://username@host/path
protocol://username@host:port/path
protocol://username:password@host/path
protocol://username:password@host:port/path
```

The include resolver operates according to the following path resolvement preference table (The top-most line has the highest preference):

| Protocol(s)           | Description / Notes                                                                               |
|-----------------------|---------------------------------------------------------------------------------------------------|
| `file://`, (`smb://`) | Absolute path or relative path from the current working directory                                 |
| `file://`, (`smb://`) | Absolute path or relative path from the current working directory with the file extension `.au3`  |
| `file://`, (`smb://`) | Absolute path or relative path from the current working directory with the file extension `.au2`  |
| `file://`, (`smb://`) | Absolute path or relative path from the current working directory with the file extension `.au`   |
| `http://`, `https://` | Absolute HTTP or HTTPS web URI                                                                    |
| `ftp://`, `ftps://`   | Absolute FTP or FTPS web URI                                                                      |
| `sftp://`, `ssh://`   | Absolute SFTP or SSH web URI                                                                      |
| `file://`, (`smb://`) | Relative path from the _source_ directory                                                         |
| `file://`, (`smb://`) | Relative path from any include directory specified in the file `settings.json`                    |
| `file://`, (`smb://`) | Relative path from the compiler's root directory                                                  |


Examples:
```autoit
#include <../my_header.au3>
#include '\\192.168.178.22\Public\Documents\program1'   ; <-- '.au3' extension will be added automatically
#include "ssh://root:password@my-server.domain.com:22/home/script.au3"
```

Please refer to the [AutoIt++ runtime behaviour description](runtime.md) for more information about the `#include` file path resoltion.

# `#sign`-directive

TODO

# `#using`-directive

TODO

# `#debugattach`, `#debugprint` and `#breakpoint` directives

TODO

# `#note`, `#warning` and `#error` directives

TODO

# `enum` declarations

Enumeration declarations in AutoIt++ accept the operators `+`, `-`, `*` and `/` as optional "stepper operators".

_In an older iteration of AutoIt++, an assignment of the value `Default` to any enum element resulted in the assignment to be ignored. This has been resolved._
_Please refer to the [AutoIt++ runtime behaviour description](runtime.md) for more information about the behaviour of `Default`._
