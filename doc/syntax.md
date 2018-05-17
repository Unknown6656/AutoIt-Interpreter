# A rough AutoIt3 custom-flavored syntax reference
[go back](../readme.md)

The AutoIt3-syntax is fully compatible with the AutoIt++ dialect, meaning that the [official syntax reference](https://www.autoitscript.com/autoit3/docs/) applies to AutoIt++.
<br/>
This article highlights the most important differences between AutoIt3's and AutoIt++'s syntaxes. It is therefore divided in the following sections:

1) [AutoIt++ operators](#autoit-operators)
2) [AutoIt++ string interpolation](#autoit-string-interpolation)
3) [P/Invoke functions](#pinvoke-functions)
4) [λ-Expressions](#λ-expressions)
5) [`new`-exprssions](#new-expressions)
6) [Inline C#-code](#inline-c-code)
7) TODO

<br/>
For a more detailed and formal syntax description of the AutoIt++ dialect, please refer to the [AutoIt++ syntax tree reference](syntax-tree.md).

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
| `!`, `-`, `+`, `~`                                                | (Right) Unary prefix |
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
<br/>
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

**NOTE: Due to internal runtime behaviour, all optional parameters must be passed when using λ-executions. Otherwise, errors might occur. This should be changed in the near future.**

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


**NOTE: Due to unresolved parser issues, it is recommended to wrap any** `new { ... }`**-epxression in parentheses.**

# Inline C#-Code

TODO
