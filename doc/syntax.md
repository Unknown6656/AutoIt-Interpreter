# A rough AutoIt3 custom-flavored syntax reference
[go back](../readme.md)

The AutoIt3-syntax is fully compatible with the AutoIt++ dialect, meaning that the [official syntax reference](https://www.autoitscript.com/autoit3/docs/) applies to AutoIt++.
<br/>
This article highlights the most important differences between AutoIt3's and AutoIt++'s syntaxes. It is therefore divided in the following sections:

1) [AutoIt++ operators](#autoit++-operators)
2) [AutoIt++ string interpolation](#autoit++-string-interpolation)
3) [P/Invoke functions](#pinvoke-functions)
4) [λ-Expressions](#λ-expressions)
5) TODO

------

# AutoIt++ operators

As AutoIt3 has only a small amount of operators compared to languages like C#, C++ or F# (in which you can even define new operators!),
the AutoIt++ variant adds the following operators in order to improve code compactness and increase the developer's productivity:

## New logical operators: `Nor`, `Nand`, `Xor`, `Nxor` and `!`

To extend the set of the logical (boolean) operators `And` and `Or`, the operators `Nor`, `Nand`, `Xor`, `Nxor`, `!` have been added.
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

| `A`   | `B`   | `And` | `Nand` | `Or`  | `Nor` | `Xor` | `Nxor` |
|:-----:|:-----:|:-----:|:------:|:-----:|:-----:|:-----:|:------:|
|`false`|`false`|`false`| `true` |`false`| `true`|`false`| `true` |
|`false`| `true`|`false`| `true` | `true`|`false`| `true`| `false`|
| `true`|`false`|`false`| `true` | `true`|`false`| `true`| `false`|
| `true`| `true`| `true`| `false`| `true`|`false`|`false`| `true` |

## Bitwise arithmetical operators

A whole set of bitwise arithmetical binary infix operators have been introduced with AutoIt++:

 - `&&`: **Bitwise And**
 - `~&&`: **Bitwise Nand**
 - `||`: **Bitwise Or**
 - `~||`: **Bitwise Nor**
 - `^^`: **Bitwise Xor**
 - `~^^`: **Bitwise Nxor**
 - `~`: **Bitwise Not**
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

## Operator Precedence

TODO

# AutoIt++ string interpolation

AutoIt++ allows the usage of so-called _interpolated strings_, which are known from other languages such as PHP, C#, JavaScript, Bash, Python and many others more.
<br/>
Interpolated strings have a leading dollar symbol (`$`) and can interpolate variables in their regular notation (`$`-prefixed).
To escape the dollar-character inside an interpolated string, it must be prefixed with a backslash (`\`). To escape a backslash in an interpolated string, one must type to backslashes (`\\`).
A backslash can also be used as control-sequence prefix for the following entities:

| Expression | Translation |
|------------|------------|
| `\"` | The ASCII character `"` (`0x22`) |
| `\r` | The control-character `CR` (`0x0D`) |
| `\n` | The control-character `LF` (`0x0A`) |
| `\t` | The control-character `HT` (`0x09`) |
| `\v` | The control-character `VT` (`0x0B`) |
| `\b` | The control-character `BS` (`0x08`) |
| `\a` | The control-character `BEL` (`0x07`) |
| `\f` | The control-character `FF` (`0x0C`) |
| `\d` | The control-character `DEL` (`0x7F`) |
| `\0` | The control-character `NUL` (`0x00`) |
| `\\` | The ASCII character `\` (`0x5C`) |
| `\$` | The ASCII character `$` (`0x24`) |
| `\@` | The ASCII character `@` (`0x40`) |

TODO

More general information about interpolated strings can be found in [this Wikipedia article](https://en.wikipedia.org/wiki/String_interpolation).
<br/>
More general information about the ASCII control characters can be found in [this Wikipedia article](https://en.wikipedia.org/wiki/Control_character#In_ASCII).

# P/Invoke functions

TODO

# λ-Expressions

TODO
