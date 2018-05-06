# A rough AutoIt3 custom-flavored syntax reference
[go back](../readme.md)

The AutoIt3-syntax is fully compatible with the AutoIt++ dialect, meaning that the [official syntax reference](https://www.autoitscript.com/autoit3/docs/) applies to AutoIt++.
<br/>
This article highlights the most important differences between AutoIt3's and AutoIt++'s syntaxes. It is therefore divided in the following sections:

1) [AutoIt++ operators](#autoit-operators)
2) [AutoIt++ string interpolation](#autoit-string-interpolation)
3) [P/Invoke functions](#pinvoke-functions)
4) [λ-Expressions](#λ-expressions)
5) [Inline C#-code](#inline-c-code)
6) TODO

------

# AutoIt++ operators

As AutoIt3 has only a small amount of operators compared to languages like C#, C++ or F# (in which you can even define new operators!),
the AutoIt++ variant adds the following operators in order to improve code compactness and increase the developer's productivity:

## New logical operators: `Nor`, `Nand`, `Xor`, `Nxor`, `Impl` and `!`

To extend the set of the logical (boolean) operators `And` and `Or`, the operators `Nor`, `Nand`, `Xor`, `Nxor`, `Impl`, `!` have been added.
<br/>
The operator `!` is the short variant of the prfix (unary) operator `Not`. The other new operators are binary infix ones which can be used e.g. as follows:
```autoit
$foo = true
$bar = $test <> 7.5

If ($foo Xor $bar) Nand IsDeclared("test") Then
    ; ....
EndIf
```

The truth table for the logical operators is defined as follows:

| `A`   | `B`   | `And` | `Nand` | `Or`  | `Nor` | `Xor` | `Nxor` | `Impl` |
|:-----:|:-----:|:-----:|:------:|:-----:|:-----:|:-----:|:------:|:------:|
|`false`|`false`|`false`| `true` |`false`| `true`|`false`| `true` | `true` |
|`false`| `true`|`false`| `true` | `true`|`false`| `true`| `false`| `true` |
| `true`|`false`|`false`| `true` | `true`|`false`| `true`| `false`| `false`|
| `true`| `true`| `true`| `false`| `true`|`false`|`false`| `true` | `true` |

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

### String and array length using `#`

A length of a string or an array can be taken using the prefix operator `#` as follows:
```autoit
dim $arr[5] = [ 0, 1, 2, 3, 4 ]
$str = "my string"

$l_arr = #$arr ; has the value 5
$l_str = #$str ; has the value 9
```

## Assignment operators

TODO

## Unary Operators `#`, `!` and `~`

As partly mentioned in previous paragraphs, AutoIt++ introduces the following right-associative unary prefix operators:

 - `#`: **String/Array Length**
 - `!`: **Logical (Boolean) Not**
 - `~`: **Bitwise Not**

The arithmetic unary operators `+` (Identity) and `-` (Negation) naturally do also exist and work as is being expected by the AutoIt3 specification and mathematical standards.

## Operator Precedence

The following displays a list of operator precendences in AutoIt++. The top-most row represents operators with the highest precedence. Inside the row, the operators are parsed and matched from left to right.

| Operator(s)                                                   | Associativity |
|---------------------------------------------------------------|---------------|
| `#`, `+`, `-`, `!`, `~`                                       | Right (Unary) |
| `^`                                                           | Left          |
| `*`, `/`, `%`                                                 | Left          |
| `+`, `-`                                                      | Left          |
| `<<`, `>>`                                                    | Left          |
| `<<<`, `>>>`                                                  | Left          |
| `~&&`, `&&`                                                   | Left          |
| `~^^`, `^^`                                                   | Left          |
| <code>~&#124;&#124;</code>, <code>&#124;&#124;</code>         | Left          |
| <code>@&#124; ..</code>, `@ ..`, <code>@&#124;</code>, `@`    | _Dunno, right I think_ |
| `&`                                                           | Left          |
| `<>`, `==`, `=`                                               | Left          |
| `Nand`, `And`                                                 | Left          |
| `Nxor`, `Xor`                                                 | Left          |
| `Nor`, `Or`                                                   | Left          |
| `Impl`                                                        | Left          |

# AutoIt++ string interpolation

AutoIt++ allows the usage of so-called _interpolated strings_, which are known from other languages such as PHP, C#, JavaScript, Bash, Python and many others more.
<br/>
Interpolated strings have a leading dollar symbol (`$`) and can interpolate variables in their regular notation (`$`-prefixed) or macros (`@`-prefixed).
To escape any dollar- or at-characters inside an interpolated string, it must be prefixed with a backslash (`\`). To escape a backslash in an interpolated string, one must type to backslashes (`\\`).
A backslash can also be used as control-sequence prefix for the following entities:

| Expression | Translation |
|------------|------------|
| `\"` | The ASCII character `"` (`0x22`) |
| `\r` | The control-character `CR` (carrige return, `0x0D`) |
| `\n` | The control-character `LF` (line feed, `0x0A`) |
| `\t` | The control-character `HT` (horizontal tab, `0x09`) |
| `\v` | The control-character `VT` (vertical tab, `0x0B`) |
| `\b` | The control-character `BS` (backspace, `0x08`) |
| `\a` | The control-character `BEL` (bell, `0x07`) |
| `\f` | The control-character `FF` (form format, `0x0C`) |
| `\d` | The control-character `DEL` (delete, `0x7F`) |
| `\0` | The control-character `NUL` (null, `0x00`) |
| `\\` | The ASCII character `\` (`0x5C`) |
| `\$` | The ASCII character `$` (`0x24`) |
| `\@` | The ASCII character `@` (`0x40`) |
| `\x--` | The ASCII character represented by the two hexadecimal digits `--` |
| `\u----` | The UNICODE character represented by the four hexadecimal digits `----` |

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

TODO

# Inline C#-Code

TODO
