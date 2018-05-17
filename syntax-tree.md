# A rough AutoIt3 custom-flavored syntax reference
[go back](../readme.md)

The AutoIt3-syntax is fully compatible with the AutoIt++ dialect, meaning that the [official syntax reference](https://www.autoitscript.com/autoit3/docs/) applies to AutoIt++.
<br/>
This article, however, attempts to display a complete syntax tree of the AutoIt++ dialect.
<br/>
If a 'more human readable' and more visually descriptive list of AutoIt++ syntax feautures is requested, please refer to the [AutoIt++ syntax reference](syntax.md)


## AutoIt++ syntax tree

Reference:

 - `xxx` describes manadatory non-terminal symbols
 - `'xxx'` describes manadatory terminal symbols
 - `[ ... ]` describes optional groups
 - `xxx yyy` describes the concatenation of `xxx` and `yyy`
 - `xxx | yyy` describes the unification of `xxx` and `yyy`
 - `xxx := ...` describes the definition of `...`
 - `// xxx` represents a comment with the message `xxx`
 - `%EOF%` represents the end of file
 - `%NL%` represents a new line

It has to be noted, that indentation and cases are to be ignored during the parsing (in the sense, that AutoIt3 and AutoIt++ are _case-insensitive_).
<br/>
The following syntax tree describes, how a valid AutoIt++ code file (hereby called `program`) will be parsed:

```
             program := [global_lines] %EOF%

        global_lines := global_lines %NL% global_line 
                      | global_line

         global_line := function_declaration
                      | global_variable_declaration
                      | statement
                      | preprocessor_directive

function_declaration := 'Func' function_name '(' [function_parameters] ')' %NL%
                        [function_body %NL%]
                        'EndFunc'

 function_parameters :=

                        TODO


       function_body := local_lines

         local_lines := local_lines %NL% local_line
                      | local_line

          local_line := local_variable_declaration
                      | statement
                      | preprocessor_directive

TODO

```
