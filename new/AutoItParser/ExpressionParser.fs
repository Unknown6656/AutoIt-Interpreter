namespace Unknown6656.AutoIt3.Parser.ExpressionParser

open Unknown6656.AutoIt3.Parser

open System.Globalization
open System

open Piglet.Parser.Configuration.Generic
open Piglet.Parser.Configuration.FSharp
open Piglet.Parser.Construction

open AST


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
            |> float

        x.Configurator.LexerSettings.EscapeLiterals <- false
        x.Configurator.LexerSettings.IgnoreCase <- true
        
        (* AutoIt Expression Grammar (v4):

                [start] := assg-target assg-op any-expr
                         | multi-decl-expr
                         | any-expr

                multi-decl-expr := multi-decl-expr "," named-decl-expr
                                 | named-decl-expr

                named-decl-expr := variable decl-expr

                decl-expr := "[" literal-number "]" "=" array-expr
                           | "[" literal-number "]"
                           | "[" "]" "=" array-expr
                           | "=" array-expr
                           | "=" any-expr
                           | "[" "]"

                array-expr := "[" arg-list "]"

                assg-target := variable
                             | indexer-expr
                             | member-expr

                indexer-expr := object-expr "[" any-expr "]"

                member-expr := object-expr "." identifier
                             | "." identifier

                object-expr := assg-target
                             | macro
                             | literal
                             | identifier
                             | funccall
                             | "(" any-expr ")"
                             | "new" net-identifier
                             | "class" net-identifier

                net-identifier = identifier
                               | identifier "::" net-identifier

                any-expr := object-expr
                          | any-expr "?" any-expr ":" any-expr
                          | any-expr bin-op any-expr
                          | un-op any-expr
                          | "ByRef" variable

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

        let nt_params_decl_expr     = x.CreateNonTerminal<PARAMETER_DECLARATION list>       "params-decl-expr"
        let nt_param_decl_expr      = x.CreateNonTerminal<PARAMETER_DECLARATION>            "param-decl-expr"
        let nt_assg_target          = x.CreateNonTerminal<ASSIGNMENT_TARGET>                "assg-targ"
        let nt_assg_op              = x.CreateNonTerminal<OPERATOR_ASSIGNMENT>              "assg-op"
        let nt_multi_decl_expr      = x.CreateNonTerminal<NAMED_VARIABLE_DECLARATION list>  "multi-decl-expr"
        let nt_named_decl_expr      = x.CreateNonTerminal<NAMED_VARIABLE_DECLARATION>       "named-decl-expr"
        let nt_decl_expr            = x.CreateNonTerminal<VARIABLE_DECLARATION>             "decl-expr"
        let nt_index_expr           = x.CreateNonTerminal<INDEXER_EXPRESSION>               "index-expr"
        let nt_member_expr          = x.CreateNonTerminal<MEMBER_EXPRESSION>                "member-expr"
        let nt_array_expr           = x.CreateNonTerminal<EXPRESSION list>                  "array-expr"
        let nt_object_expr          = x.CreateNonTerminal<EXPRESSION>                       "object-expr"
        let nt_any_expr             = x.CreateNonTerminal<EXPRESSION>                       "any-expr"
        let nt_conditional_expr     = x.CreateNonTerminal<EXPRESSION>                       "cond-expr"
        let nt_func_call            = x.CreateNonTerminal<FUNCCALL_EXPRESSION>              "funccall"
        let nt_literal              = x.CreateNonTerminal<LITERAL>                          "literal"
        let nt_literal_num          = x.CreateNonTerminal<float>                            "literal-num"
        let nt_args                 = x.CreateNonTerminal<FUNCCALL_ARGUMENTS>               "args"
        let nt_arglist              = x.CreateNonTerminal<FUNCCALL_ARGUMENTS>               "arglist"
        let nt_net_identifier       = x.CreateNonTerminal<string>                           "net-identifier"
        let t_operator_assign_add   = x.CreateTerminalF @"\+="                              (fun _ -> AssignAdd)
        let t_operator_assign_sub   = x.CreateTerminalF @"-="                               (fun _ -> AssignSubtract)
        let t_operator_assign_mul   = x.CreateTerminalF @"\*="                              (fun _ -> AssignMultiply)
        let t_operator_assign_div   = x.CreateTerminalF @"/="                               (fun _ -> AssignDivide)
        let t_operator_assign_con   = x.CreateTerminalF @"&="                               (fun _ -> AssignConcat)
        let t_operator_comp_neq     = x.CreateTerminalF @"<>"                               (fun _ -> Unequal)
        let t_operator_comp_gte     = x.CreateTerminalF @">="                               (fun _ -> GreaterEqual)
        let t_operator_comp_gt      = x.CreateTerminalF @">"                                (fun _ -> Greater)
        let t_operator_comp_lte     = x.CreateTerminalF @"<="                               (fun _ -> LowerEqual)
        let t_operator_comp_lt      = x.CreateTerminalF @"<"                                (fun _ -> Lower)
        let t_operator_comp_eq      = x.CreateTerminalF @"=="                               (fun _ -> EqualCaseSensitive)
        let t_symbol_equal          = x.CreateTerminal  @"="
        let t_symbol_questionmark   = x.CreateTerminal  @"\?"
        let t_symbol_colon          = x.CreateTerminal  @":"
        let t_symbol_double_colon   = x.CreateTerminal  @"::"
        let t_symbol_dot            = x.CreateTerminal  @"\."
        let t_symbol_comma          = x.CreateTerminal  @","
        let t_symbol_minus          = x.CreateTerminal  @"-"
        let t_symbol_plus           = x.CreateTerminal  @"\+"
        let t_operator_mul          = x.CreateTerminalF @"\*"                               (fun _ -> Multiply)
        let t_operator_div          = x.CreateTerminalF @"/"                                (fun _ -> Divide)
        let t_operator_pow          = x.CreateTerminalF @"^"                                (fun _ -> Power)
        let t_operator_concat       = x.CreateTerminalF @"&"                                (fun _ -> StringConcat)
        let t_symbol_oparen         = x.CreateTerminal  @"\("
        let t_symbol_cparen         = x.CreateTerminal  @"\)"
        let t_symbol_obrack         = x.CreateTerminal  @"\["
        let t_symbol_cbrack         = x.CreateTerminal  @"\]"
     // let t_symbol_ocurly         = x.CreateTerminal  @"\{"
     // let t_symbol_ccurly         = x.CreateTerminal  @"\}"
        let t_keyword_new           = x.CreateTerminal  @"new"
        let t_keyword_class         = x.CreateTerminal  @"class"
        let t_keyword_to            = x.CreateTerminal  @"to"
        let t_keyword_const         = x.CreateTerminal  @"const"
        let t_keyword_byref         = x.CreateTerminal  @"(by)?ref"
        let t_keyword_and           = x.CreateTerminalF @"and"                                   (fun _ -> And)
        let t_keyword_or            = x.CreateTerminalF @"or"                                    (fun _ -> Or)
        let t_keyword_not           = x.CreateTerminalF @"(not|!)"                               (fun _ -> Not)
        let t_literal_true          = x.CreateTerminalF @"true"                                  (fun _ -> True)
        let t_literal_false         = x.CreateTerminalF @"false"                                 (fun _ -> False)
        let t_literal_null          = x.CreateTerminalF @"null"                                  (fun _ -> Null)
        let t_literal_default       = x.CreateTerminalF @"default"                               (fun _ -> Default)
     // let t_literal_empty         = x.CreateTerminalF @"empty"                                 (fun _ -> String "")
        let t_hex                   = x.CreateTerminalF @"(\+|-)?(0x[\da-fA-F_]+|[\da-fA-F_]+h)" (parse_num "0x" (fun s -> Int64.Parse(s.Replace("_", "").TrimEnd 'h', NumberStyles.HexNumber)))
        let t_bin                   = x.CreateTerminalF @"(\+|-)?0b[01_]+"                       (parse_num "0b" (fun s -> Convert.ToInt64(s.Replace("_", ""), 2)))
        let t_oct                   = x.CreateTerminalF @"(\+|-)?0o[0-7_]+"                      (parse_num "0o" (fun s -> Convert.ToInt64(s.Replace("_", ""), 8)))
        let t_dec                   = x.CreateTerminalF @"(\+|-)?\d+(\.\d+)?(e(\+|-)?\d+)?"      (fun s -> match Double.TryParse s with
                                                                                                           | (true, d) -> d
                                                                                                           | _ -> Double.Parse(s, NumberStyles.Float))
        let t_variable              = x.CreateTerminalF @"$[^\W\d]\w*"                           (fun s -> VARIABLE(s.Substring 1))
        let t_macro                 = x.CreateTerminalF @"@[^\W\d]\w*"                           (fun s -> MACRO(s.Substring 1))
        let t_string_1              = x.CreateTerminalF "\"(([^\"]*\"\"[^\"]*)*|[^\"]+)\""       (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Replace("\"\"", "\"")))
        let t_string_2              = x.CreateTerminalF @"'(([^']*''[^']*)*|[^']+)'"             (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Replace("''", "'")))
        let t_identifier            = x.CreateTerminalF @"[^\W\d]\w*"                            Identifier


        let precedences =
            ([
                Right, [ t_symbol_questionmark ]
                Left, [ t_keyword_or ]
                Left, [ t_keyword_and ]
                Left, [ t_operator_comp_lte; t_operator_comp_lt; t_operator_comp_gt; t_operator_comp_gte ]
                Left, [ t_operator_comp_neq; t_symbol_equal; t_operator_comp_eq ]
                Left, [ t_operator_concat ]
                Left, [ t_symbol_plus; t_symbol_minus ]
                Left, [ t_operator_mul; t_operator_div ]
                Right, [ t_operator_pow ]
            ] : list<Associativity * ITerminalWrapper list>)
            |> List.map (fun (a, g) -> struct(match a with
                                              | Right -> AssociativityDirection.Right
                                              | _ -> AssociativityDirection.Left
                                              , List.toArray g))
            |> List.toArray

        x.SetPrecedenceList precedences

        match mode with
        | ParserMode.FunctionParameters ->
            reduce_1i nt_result nt_params_decl_expr ParameterDeclaration
            reduce_ci nt_result (fun () -> ParameterDeclaration [])

            reduce_3i nt_params_decl_expr nt_params_decl_expr t_symbol_comma nt_param_decl_expr (fun xs _ x -> xs@[x])
            reduce_1i nt_params_decl_expr nt_param_decl_expr (fun x -> [x])

            reduce_3i nt_param_decl_expr t_keyword_const t_keyword_byref nt_named_decl_expr (fun _ _ (v, Scalar e) -> { IsConst = true; IsByRef = true; Variable = v; DefaultValue = e })
            reduce_2i nt_param_decl_expr t_keyword_byref nt_named_decl_expr (fun _ (v, Scalar e) -> { IsConst = false; IsByRef = true; Variable = v; DefaultValue = e })
            reduce_2i nt_param_decl_expr t_keyword_const nt_named_decl_expr (fun _ (v, Scalar e) -> { IsConst = true; IsByRef = false; Variable = v; DefaultValue = e })
            reduce_1i nt_param_decl_expr nt_named_decl_expr (fun (v, Scalar e) -> { IsConst = false; IsByRef = false; Variable = v; DefaultValue = e })
        | ParserMode.MultiDeclaration ->
            reduce_1i nt_result nt_multi_decl_expr MultiDeclarationExpression

            reduce_3i nt_multi_decl_expr nt_multi_decl_expr t_symbol_comma nt_named_decl_expr (fun xs _ x -> xs@[x])
            reduce_1i nt_multi_decl_expr nt_named_decl_expr (fun x -> [x])
        | ParserMode.ArbitraryExpression ->
            reduce_3i nt_result nt_assg_target nt_assg_op nt_any_expr (fun t o e -> AssignmentExpression(t, o, e))
            reduce_3i nt_result nt_any_expr t_keyword_to nt_any_expr (fun f _ t -> ToExpression(f, t))
            reduce_1i nt_result nt_any_expr AnyExpression
        | _ -> 
            sprintf "The parser mode '%O' is either unknown or unsupported." mode
            |> ArgumentOutOfRangeException
            |> raise
 
        reduce_2i nt_named_decl_expr t_variable nt_decl_expr (fun v e -> v, e)

        if mode = ParserMode.MultiDeclaration then
            reduce_5i nt_decl_expr t_symbol_obrack nt_literal_num t_symbol_cbrack t_symbol_equal nt_array_expr (fun _ c _ _ e -> Array(int c, e))
            reduce_4i nt_decl_expr t_symbol_obrack t_symbol_cbrack t_symbol_equal nt_array_expr (fun _ _ _ e -> Array(e.Length, e))
            reduce_3i nt_decl_expr t_symbol_obrack nt_literal_num t_symbol_cbrack (fun _ c _ -> Array(int c, []))
            reduce_2i nt_decl_expr t_symbol_equal nt_array_expr (fun _ e -> Array(e.Length, e))
            reduce_2i nt_decl_expr t_symbol_obrack t_symbol_cbrack (fun _ _ -> Map())
        reduce_2i nt_decl_expr t_symbol_equal nt_any_expr (fun _ e -> Scalar(Some e))
        reduce_ci nt_decl_expr (fun () -> Scalar None)

        reduce_3i nt_array_expr t_symbol_obrack nt_arglist t_symbol_cbrack (fun _ a _ -> a)

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
        reduce_2i nt_object_expr t_keyword_new nt_net_identifier (fun _ i -> FunctionCall(DirectFunctionCall(Identifier "NETNew", [Literal(String i)])))
        reduce_2i nt_object_expr t_keyword_class nt_net_identifier (fun _ i -> FunctionCall(DirectFunctionCall(Identifier "NETClass", [Literal(String i)])))
        reduce_1i nt_object_expr t_identifier FunctionName
        reduce_1i nt_object_expr nt_func_call FunctionCall
        reduce_3i nt_object_expr t_symbol_oparen nt_any_expr t_symbol_cparen (fun _ e _ -> e)

        reduce_1i nt_any_expr nt_object_expr id
        reduce_1i nt_any_expr nt_conditional_expr id

        //reduce_5i nt_conditional_expr nt_any_expr t_symbol_questionmark nt_any_expr t_symbol_colon nt_any_expr (fun a _ b _ c -> Ternary(a, b, c))
        reduce_5i nt_conditional_expr nt_object_expr t_symbol_questionmark nt_object_expr t_symbol_colon nt_any_expr (fun a _ b _ c -> Ternary(a, b, c))

        let reduce_binary symbol operator assoc =
            reduce_3i nt_any_expr nt_object_expr symbol (match assoc with | Left -> nt_any_expr | Right -> nt_object_expr) (fun a _ b -> Binary(a, operator, b))

        reduce_binary t_keyword_or Or Left
        reduce_binary t_keyword_and And Left
        reduce_binary t_operator_comp_lte LowerEqual Left
        reduce_binary t_operator_comp_lt Lower Left
        reduce_binary t_operator_comp_gte GreaterEqual Left
        reduce_binary t_operator_comp_gt Greater Left
        reduce_binary t_operator_comp_neq Unequal Left
        reduce_binary t_operator_comp_eq EqualCaseSensitive Left
        reduce_binary t_symbol_equal EqualCaseInsensitive Left
        reduce_binary t_operator_concat StringConcat Left
        reduce_binary t_symbol_plus Add Left
        reduce_binary t_symbol_minus Subtract Left
        reduce_binary t_operator_mul Multiply Left
        reduce_binary t_operator_div Divide Left
        reduce_binary t_operator_pow Power Right

        reduce_2i nt_any_expr t_keyword_byref t_variable (fun _ v -> ReferenceTo v)
        reduce_2i nt_any_expr t_symbol_plus nt_any_expr (fun _ e -> Unary(Identity, e))
        reduce_2i nt_any_expr t_symbol_minus nt_any_expr (fun _ e -> Unary(Negate, e))
        reduce_2i nt_any_expr t_keyword_not nt_any_expr (fun _ e -> Unary(Not, e))
        
        reduce_1i nt_net_identifier t_identifier (fun (Identifier s) -> s)
        reduce_3i nt_net_identifier t_identifier t_symbol_double_colon nt_net_identifier (fun (Identifier s) _ r -> s + "." + r)

        reduce_0i nt_literal_num t_hex
        reduce_0i nt_literal_num t_bin
        reduce_0i nt_literal_num t_oct
        reduce_0i nt_literal_num t_dec

        reduce_0i nt_literal t_literal_true
        reduce_0i nt_literal t_literal_false
        reduce_0i nt_literal t_literal_null
        reduce_0i nt_literal t_literal_default
     // reduce_0i nt_literal t_literal_empty
        reduce_1i nt_literal nt_literal_num Number
        reduce_0i nt_literal t_string_1
        reduce_0i nt_literal t_string_2

        reduce_4i nt_func_call t_identifier t_symbol_oparen nt_args t_symbol_cparen (fun i _ a _ -> DirectFunctionCall(i, a))
        reduce_4i nt_func_call nt_member_expr t_symbol_oparen nt_args t_symbol_cparen (fun m _ a _ -> MemberCall(m, a))

        reduce_ci nt_args (fun () -> [])
        reduce_0i nt_args nt_arglist

        reduce_1i nt_arglist nt_any_expr (fun x -> [x])
        reduce_3i nt_arglist nt_arglist t_symbol_comma nt_any_expr (fun xs _ x -> xs@[x])
