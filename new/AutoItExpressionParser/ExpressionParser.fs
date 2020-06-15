namespace Unknown6656.AutoIt3.ExpressionParser

open System.Globalization
open System

open Piglet.Parser.Configuration.Generic
open Piglet.Parser.Configuration.FSharp
open Piglet.Parser.Construction

open AST
open System.Linq.Expressions
open System.Runtime.CompilerServices


type ParserMode =
    | MultiDeclaration = 0
    | ArbitraryExpression = 1
    | FunctionParameters = 2

type ExpressionParser(mode : ParserMode) =
    inherit ParserConstructor<PARSABLE_EXPRESSION>()
    member _.ParserMode = mode
    member private x.CreateTerminalF s (f : string -> 'a) = x.CreateTerminal<'a>(s, fun s -> f s)
    override x.Construct nt_result =
        let parse_num prefix (parser : string -> Int64) (input : string) =
            let s = input.TrimStart('+').ToLower().Replace(prefix, "")
            let n, s = if s.[0] = '-' then (true, s.Substring(1)) else (false, s)
            let l = parser s
            if n then -l else l
            |> Decimal
            |> Number

        x.Configurator.LexerSettings.EscapeLiterals <- false
        x.Configurator.LexerSettings.IgnoreCase <- true
        
        (* AutoIt Expression Grammar (v4):

                [start] := assg-target assg-op any-expr
                         | multi-decl-expr
                         | any-expr

                multi-decl-expr := multi-decl-expr "," decl-expr
                                 | decl-expr

                decl-expr := variable "=" any-expr
                           | variable

                assg-target := variable
                             | indexer-expr
                             | member-expr

                indexer-expr := object-expr "[" any-expr "]"

                member-expr := object-expr "." identifier
                             | "." identifier

                object-expr := assg-target
                             | macro
                             | literal
                             | funccall
                             | "(" any-expr ")"

                any-expr := object-expr
                          | any-expr "?" any-expr ":" any-expr
                          | any-expr bin-op any-expr
                          | un-op any-expr

                funccall := identifier "(" args ")"
                          | member-expr "(" args ")"

                args := arg-list
                      | ε

                arg-list := arg-list "," any-expr
                          | any-expr


        Binary operator precedence (ascending):
                Or
                And
                <= < > >=
                <> = ==
                &
                + -
                * /
                ^

        Unary operator precedence (ascending):
                Not
                + -
        *)
        
        let nt_params_decl_expr     = x.CreateNonTerminal<PARAMETER_DECLARATION list>   "params-decl-expr"
        let nt_param_decl_expr      = x.CreateNonTerminal<PARAMETER_DECLARATION>        "param-decl-expr"
        let nt_const_modifier       = x.CreateNonTerminal<bool>                         "const-modf"
        let nt_byref_modifier       = x.CreateNonTerminal<bool>                         "byref-modf"
        let nt_assg_target          = x.CreateNonTerminal<ASSIGNMENT_TARGET>            "assg-targ"
        let nt_assg_op              = x.CreateNonTerminal<OPERATOR_ASSIGNMENT>          "assg-op"
        let nt_multi_decl_expr      = x.CreateNonTerminal<VARIABLE_DECLARATION list>    "multi-decl-expr"
        let nt_decl_expr            = x.CreateNonTerminal<VARIABLE_DECLARATION>         "decl-expr"
        let nt_index_expr           = x.CreateNonTerminal<INDEXER_EXPRESSION>           "index-expr"
        let nt_member_expr          = x.CreateNonTerminal<MEMBER_EXPRESSION>            "member-expr"
        let nt_object_expr          = x.CreateNonTerminal<EXPRESSION>                   "object-expr"
        let nt_any_expr             = x.CreateNonTerminal<EXPRESSION>                   "any-expr"
        let nt_conditional_expr     = x.CreateNonTerminal<EXPRESSION>                   "cond-expr"
        let nt_func_call            = x.CreateNonTerminal<FUNCCALL_EXPRESSION>          "funccall"
        let nt_literal              = x.CreateNonTerminal<LITERAL>                      "literal"
        let nt_args                 = x.CreateNonTerminal<FUNCCALL_ARGUMENTS>           "args"
        let nt_arglist              = x.CreateNonTerminal<FUNCCALL_ARGUMENTS>           "arglist"
        let t_operator_assign_add   = x.CreateTerminalF @"\+="                          (fun _ -> AssignAdd)
        let t_operator_assign_sub   = x.CreateTerminalF @"-="                           (fun _ -> AssignSubtract)
        let t_operator_assign_mul   = x.CreateTerminalF @"\*="                          (fun _ -> AssignMultiply)
        let t_operator_assign_div   = x.CreateTerminalF @"/="                           (fun _ -> AssignDivide)
        let t_operator_assign_con   = x.CreateTerminalF @"&="                           (fun _ -> AssignConcat)
        let t_operator_comp_neq     = x.CreateTerminalF @"<>"                           (fun _ -> Unequal)
        let t_operator_comp_gte     = x.CreateTerminalF @">="                           (fun _ -> GreaterEqual)
        let t_operator_comp_gt      = x.CreateTerminalF @">"                            (fun _ -> Greater)
        let t_operator_comp_lte     = x.CreateTerminalF @"<="                           (fun _ -> LowerEqual)
        let t_operator_comp_lt      = x.CreateTerminalF @"<"                            (fun _ -> Lower)
        let t_operator_comp_eq      = x.CreateTerminalF @"=="                           (fun _ -> EqualCaseSensitive)
        let t_symbol_equal          = x.CreateTerminal  @"="
        let t_symbol_questionmark   = x.CreateTerminal  @"\?"
        let t_symbol_colon          = x.CreateTerminal  @":"
        let t_symbol_dot            = x.CreateTerminal  @"\."
        let t_symbol_comma          = x.CreateTerminal  @","
        let t_symbol_minus          = x.CreateTerminal  @"-"
        let t_symbol_plus           = x.CreateTerminal  @"\+"
        let t_operator_mul          = x.CreateTerminalF @"\*"                           (fun _ -> Multiply)
        let t_operator_div          = x.CreateTerminalF @"/"                            (fun _ -> Divide)
        let t_operator_pow          = x.CreateTerminalF @"^"                            (fun _ -> Power)
        let t_operator_concat       = x.CreateTerminalF @"&"                            (fun _ -> StringConcat)
        let t_symbol_oparen         = x.CreateTerminal  @"\("
        let t_symbol_cparen         = x.CreateTerminal  @"\)"
        let t_symbol_obrack         = x.CreateTerminal  @"\["
        let t_symbol_cbrack         = x.CreateTerminal  @"\]"
     // let t_symbol_ocurly         = x.CreateTerminal  @"\{"
     // let t_symbol_ccurly         = x.CreateTerminal  @"\}"
     // let t_keyword_new           = x.CreateTerminal  @"new"
        let t_keyword_const         = x.CreateTerminal  @"const"
        let t_keyword_byref         = x.CreateTerminal  @"byref"
        let t_keyword_and           = x.CreateTerminalF @"and"                          (fun _ -> And)
        let t_keyword_or            = x.CreateTerminalF @"or"                           (fun _ -> Or)
        let t_keyword_not           = x.CreateTerminalF @"(not|!)"                      (fun _ -> Not)
        let t_literal_true          = x.CreateTerminalF @"true"                         (fun _ -> True)
        let t_literal_false         = x.CreateTerminalF @"false"                        (fun _ -> False)
        let t_literal_null          = x.CreateTerminalF @"null"                         (fun _ -> Null)
        let t_literal_default       = x.CreateTerminalF @"default"                      (fun _ -> Default)
        let t_literal_empty         = x.CreateTerminalF @"empty"                        (fun _ -> String "")
        let t_hex                   = x.CreateTerminalF @"(\+|-)?(0[xX][\da-fA-F]+|[\da-fA-F][hH])" (parse_num "0x" (fun s -> Int64.Parse(s.TrimEnd 'h', NumberStyles.HexNumber)))
        let t_bin                   = x.CreateTerminalF @"(\+|-)?0[bB][01]+"                        (parse_num "0b" (fun s -> Convert.ToInt64(s, 2)))
        let t_oct                   = x.CreateTerminalF @"(\+|-)?0[oO][0-7]+"                       (parse_num "0o" (fun s -> Convert.ToInt64(s, 8)))
        let t_dec                   = x.CreateTerminalF @"(\+|-)?\d+(\.\d+)?([eE](\+|-)?\d+)?"      (fun s -> match Decimal.TryParse s with
                                                                                                              | (true, d) -> d
                                                                                                              | _ -> Decimal.Parse(s, NumberStyles.Float)
                                                                                                              |> Number)
        let t_variable              = x.CreateTerminalF @"$([^\W\d]|[^\W\d]\w*)"                    (fun s -> VARIABLE(s.Substring 1))
        let t_macro                 = x.CreateTerminalF @"@([^\W\d]|[^\W\d]\w*)"                    (fun s -> MACRO(s.Substring 1))
        let t_string_1              = x.CreateTerminalF "\"(([^\"]*\"\"[^\"]*)*|[^\"]+)\""          (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Replace("\"\"", "\"")))
        let t_string_2              = x.CreateTerminalF @"'(([^']*''[^']*)*|[^']+)'"                (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Replace("''", "'")))
        let t_identifier            = x.CreateTerminalF @"([^\W\d]|[^\W\d]\w*)"                     Identifier


        let precedences =
            ([
                AssociativityDirection.Right, [ t_symbol_questionmark ]
                AssociativityDirection.Left, [ t_keyword_or ]
                AssociativityDirection.Left, [ t_keyword_and ]
                AssociativityDirection.Left, [ t_operator_comp_lte; t_operator_comp_lt; t_operator_comp_gt; t_operator_comp_gte ]
                AssociativityDirection.Left, [ t_operator_comp_neq; t_symbol_equal; t_operator_comp_eq ]
                AssociativityDirection.Left, [ t_operator_concat ]
                AssociativityDirection.Left, [ t_symbol_plus; t_symbol_minus ]
                AssociativityDirection.Left, [ t_operator_mul; t_operator_div ]
                AssociativityDirection.Right, [ t_operator_pow ]
            ] : list<AssociativityDirection * ITerminalWrapper list>)
            |> List.map (fun (a, g) -> struct(a, List.toArray g))
            |> List.toArray

        x.SetPrecedenceList precedences


        match mode with
        | ParserMode.FunctionParameters ->
            reduce_1i nt_result nt_params_decl_expr ParameterDeclaration
        | ParserMode.MultiDeclaration ->
            reduce_1i nt_result nt_multi_decl_expr MultiDeclarationExpression
        | ParserMode.ArbitraryExpression ->
            reduce_3i nt_result nt_assg_target nt_assg_op nt_any_expr (fun t o e -> AssignmentExpression(t, o, e))
            reduce_1i nt_result nt_any_expr AnyExpression
        | _ -> 
            sprintf "The parser mode '%O' is either unknown or unsupported." mode
            |> ArgumentOutOfRangeException
            |> raise
 
        reduce_3i nt_params_decl_expr nt_params_decl_expr t_symbol_comma nt_param_decl_expr (fun xs _ x -> xs@[x])
        reduce_1i nt_params_decl_expr nt_param_decl_expr (fun x -> [x])
        
        reduce_3i nt_param_decl_expr nt_const_modifier nt_byref_modifier nt_decl_expr (fun c r (v, e) -> { IsConst = c; IsByRef = r; Variable = v; DefaultValue = e })
        reduce_3i nt_param_decl_expr nt_byref_modifier nt_const_modifier nt_decl_expr (fun r c (v, e) -> { IsConst = c; IsByRef = r; Variable = v; DefaultValue = e })

        reduce_1i nt_const_modifier t_keyword_const (fun _ -> true)
        reduce_ci nt_const_modifier (fun () -> false)
        
        reduce_1i nt_byref_modifier t_keyword_byref (fun _ -> true)
        reduce_ci nt_byref_modifier (fun () -> false)

        reduce_3i nt_multi_decl_expr nt_multi_decl_expr t_symbol_comma nt_decl_expr (fun xs _ x -> xs@[x])
        reduce_1i nt_multi_decl_expr nt_decl_expr (fun x -> [x])
        
        reduce_3i nt_decl_expr t_variable t_symbol_equal nt_any_expr (fun v _ e -> v, Some e)
        reduce_1i nt_decl_expr t_variable (fun v -> v, None)

        // reduce_1i nt_assg_op t_symbol_equal (fun _ -> Assign)
        reduce_1i nt_assg_op t_operator_assign_add (fun _ -> AssignAdd)
        reduce_1i nt_assg_op t_operator_assign_sub (fun _ -> AssignSubtract)
        reduce_1i nt_assg_op t_operator_assign_mul (fun _ -> AssignMultiply)
        reduce_1i nt_assg_op t_operator_assign_div (fun _ -> AssignDivide)
        reduce_1i nt_assg_op t_operator_assign_con (fun _ -> AssignConcat)

        reduce_1i nt_assg_target t_variable VariableAssignment
        reduce_1i nt_assg_target nt_index_expr IndexedAssignment
        reduce_1i nt_assg_target nt_member_expr MemberAssignemnt

        reduce_4i nt_index_expr nt_object_expr t_symbol_obrack nt_any_expr t_symbol_cbrack (fun e _ i _ -> (e, i))

        reduce_3i nt_member_expr nt_object_expr t_symbol_dot t_identifier (fun e _ m -> ExplicitMemberAccess(e, m))
        reduce_2i nt_member_expr t_symbol_dot t_identifier (fun _ m -> ImplicitMemberAccess m)

        reduce_1i nt_object_expr t_variable Variable
        reduce_1i nt_object_expr nt_index_expr Indexer
        reduce_1i nt_object_expr nt_member_expr Member
        reduce_1i nt_object_expr t_macro Macro
        reduce_1i nt_object_expr nt_literal Literal
        reduce_1i nt_object_expr nt_func_call FunctionCall
        reduce_3i nt_object_expr t_symbol_oparen nt_any_expr t_symbol_cparen (fun _ e _ -> e)

        reduce_1i nt_any_expr nt_object_expr id
        reduce_1i nt_any_expr nt_conditional_expr id
        
        //reduce_5i nt_conditional_expr nt_any_expr t_symbol_questionmark nt_any_expr t_symbol_colon nt_any_expr (fun a _ b _ c -> Ternary(a, b, c))
        reduce_5i nt_conditional_expr nt_object_expr t_symbol_questionmark nt_object_expr t_symbol_colon nt_any_expr (fun a _ b _ c -> Ternary(a, b, c))

        let reduce_binary symbol operator =
            reduce_3i nt_any_expr nt_object_expr symbol nt_object_expr (fun a _ b -> Binary(a, operator, b))

        reduce_binary t_keyword_or Or
        reduce_binary t_keyword_and And
        reduce_binary t_operator_comp_lte LowerEqual
        reduce_binary t_operator_comp_lt Lower
        reduce_binary t_operator_comp_gte GreaterEqual
        reduce_binary t_operator_comp_gt Greater
        reduce_binary t_operator_comp_neq Unequal
        reduce_binary t_operator_comp_eq EqualCaseSensitive
        reduce_binary t_symbol_equal EqualCaseInsensitive
        reduce_binary t_operator_concat StringConcat
        reduce_binary t_symbol_plus Add
        reduce_binary t_symbol_minus Subtract
        reduce_binary t_operator_mul Multiply
        reduce_binary t_operator_div Divide
        reduce_binary t_operator_pow Power
        
        reduce_2i nt_any_expr t_symbol_plus nt_any_expr (fun _ e -> Unary(Identity, e))
        reduce_2i nt_any_expr t_symbol_minus nt_any_expr (fun _ e -> Unary(Negate, e))
        reduce_2i nt_any_expr t_keyword_not nt_any_expr (fun _ e -> Unary(Not, e))

        reduce_0i nt_literal t_literal_true
        reduce_0i nt_literal t_literal_false
        reduce_0i nt_literal t_literal_null
        reduce_0i nt_literal t_literal_default
        reduce_0i nt_literal t_literal_empty
        reduce_0i nt_literal t_hex
        reduce_0i nt_literal t_bin
        reduce_0i nt_literal t_oct
        reduce_0i nt_literal t_dec 
        reduce_0i nt_literal t_string_1
        reduce_0i nt_literal t_string_2

        reduce_4i nt_func_call t_identifier t_symbol_oparen nt_args t_symbol_cparen (fun i _ a _ -> DirectFunctionCall(i, a))
        reduce_4i nt_func_call nt_member_expr t_symbol_oparen nt_args t_symbol_cparen (fun m _ a _ -> MemberCall(m, a))

        reduce_0i nt_args nt_arglist
        reduce_ci nt_args (fun () -> [])

        reduce_3i nt_arglist nt_arglist t_symbol_comma nt_any_expr (fun xs _ x -> xs@[x])
        reduce_1i nt_arglist nt_any_expr (fun x -> [x])
