<img alt="AutoIt++ icon" src="images/icon-1024.png" height="200"/>

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
 - `< ... >` describes a regex expression
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
                                  [function_body]
                                  'EndFunc'
          
           function_parameters := function_parameters ',' variable_name
                                | variable_name

                 function_body := function_body %NL% local_line
                                | local_line %NL%
          
                    local_line := local_variable_declaration
                                | statement
                                | preprocessor_directive
         
   global_variable_declaration := global_modifiers variable_name
                                | global_modifiers variable_name '=' expression
                                | global_modifiers variable_name dynamic_indexers
                                | global_modifiers variable_name static_indexers '=' array_init_expression

              global_modifiers := 'Static' [global_scope]
                                | global_scope ['Const']

                  global_scope := 'Dim'
                                | 'Global'
    
    local_variable_declaration := local_modifiers variable_name
                                | local_modifiers variable_name '=' expression
                                | local_modifiers variable_name dynamic_indexers
                                | local_modifiers variable_name static_indexers '=' array_init_expression

               local_modifiers := 'Static' [local_scope]
                                | local_scope ['Const']

                   local_scope := 'Dim'
                                | 'Local'
                                
                 function_name := identifier
                 
                 variable_name := '$' identifier

                    macro_name := '@' identifier

                    identifier := < [_a-z][_a-z0-9]* >
                    

                                TODO


               static_indexers :=

              dynamic_indexers :=

         array_init_expression :=

                     statement := statement_if
                                | statement_while
                                | statement_for

                                TODO

                    expression := 

        preprocessor_directive :=



                                TODO

```
