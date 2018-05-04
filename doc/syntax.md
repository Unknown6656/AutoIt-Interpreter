# A rough AutoIt3 custom-flavored syntax reference
[go back](../readme.md)

The AutoIt3-syntax is fully compatible with the AutoIt++ dialect, meaning that the [official syntax reference](https://www.autoitscript.com/autoit3/docs/) applies to AutoIt++.
<br/>
This article highlights the most important differences between AutoIt3's and AutoIt++'s syntaxes. It is therefore divided in the following sections:

1) [AutoIt++ operators](#autoit++-operators)
2) TODO
3) TODO

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
The expression `a @ b` represents the `b`-th character in the string `a`. The difference between `@` and `@|` is that `@` uses 0-based indices and `@|` uses one-based ones.

The substring expression


TODO

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
