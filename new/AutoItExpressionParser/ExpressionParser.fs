namespace Unknown6656.AutoIt3.ExpressionParser

open System.Globalization
open System

open Piglet.Parser.Configuration.Generic

open AST
open Piglet.Parser.Construction
open System.Runtime.Serialization


type private Associativity =
    | Left
    | Right

type ExpressionParser() =
    inherit ParserConstructor<PARSABLE_EXPRESSION>()

    member private x.CreateTerminalF s f = x.CreateTerminal(s, fun s -> f s)
    member private x.nt_subexpr d = x.CreateNonTerminal<EXPRESSION>(sprintf "expr-%d" d)
    override x.Construct nt_result =
        let parse_num prefix (parser : string -> Int64) (input : string) =
            let s = input.TrimStart('+').ToLower().Replace(prefix, "")
            let n, s = if s.[0] = '-' then (true, s.Substring(1)) else (false, s)
            let l = parser s
            if n then -l else l
            |> Decimal
            |> Number

        x.Configurator.LexerSettings.IgnoreCase <- true
        
        (* AutoIt Expression Grammar (v4):

                [start] := assg-target assg-op any-expr
                         | any-expr

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


        let nt_assg_target          = x.CreateNonTerminal<ASSIGNMENT_TARGET>    "assg-targ"
        let nt_assg_op              = x.CreateNonTerminal<OPERATOR_ASSIGNMENT>  "assg-op"
        let nt_index_expr           = x.CreateNonTerminal<INDEXER_EXPRESSION>   "index-expr"
        let nt_member_expr          = x.CreateNonTerminal<MEMBER_EXPRESSION>    "member-expr"
        let nt_object_expr          = x.CreateNonTerminal<EXPRESSION>           "object-expr"
        let nt_any_expr             = x.CreateNonTerminal<EXPRESSION>           "any-expr"
        let nt_conditional_expr     = x.CreateNonTerminal<EXPRESSION>           "cond-expr"
        let nt_func_call            = x.CreateNonTerminal<FUNCCALL_EXPRESSION>  "funccall"
        let nt_binary_op            = x.CreateNonTerminal<OPERATOR_BINARY>      "bin-op"
        let nt_unary_op             = x.CreateNonTerminal<OPERATOR_UNARY>       "un-op"
        let nt_literal              = x.CreateNonTerminal<LITERAL>              "literal"
        let nt_args                 = x.CreateNonTerminal<FUNCCALL_ARGUMENTS>   "args"
        let nt_arglist              = x.CreateNonTerminal<FUNCCALL_ARGUMENTS>   "arglist"
        let t_operator_assign_add   = x.CreateTerminalF @"\+="                  (fun _ -> AssignAdd)
        let t_operator_assign_sub   = x.CreateTerminalF @"-="                   (fun _ -> AssignSubtract)
        let t_operator_assign_mul   = x.CreateTerminalF @"\*="                  (fun _ -> AssignMultiply)
        let t_operator_assign_div   = x.CreateTerminalF @"/="                   (fun _ -> AssignDivide)
        let t_operator_assign_con   = x.CreateTerminalF @"&="                   (fun _ -> AssignConcat)
        let t_operator_comp_neq     = x.CreateTerminalF @"<>"                   (fun _ -> Unequal)
        let t_operator_comp_gte     = x.CreateTerminalF @">="                   (fun _ -> GreaterEqual)
        let t_operator_comp_gt      = x.CreateTerminalF @">"                    (fun _ -> Greater)
        let t_operator_comp_lte     = x.CreateTerminalF @"<="                   (fun _ -> LowerEqual)
        let t_operator_comp_lt      = x.CreateTerminalF @"<"                    (fun _ -> Lower)
        let t_operator_comp_eq      = x.CreateTerminalF @"=="                   (fun _ -> EqualCaseSensitive)
        let t_symbol_equal          = x.CreateTerminal  @"="
        let t_symbol_questionmark   = x.CreateTerminal  @"\?"
        let t_symbol_colon          = x.CreateTerminal  @":"
        let t_symbol_dot            = x.CreateTerminal  @"\."
        let t_symbol_comma          = x.CreateTerminal  @","
        let t_symbol_minus          = x.CreateTerminal  @"-"
        let t_symbol_plus           = x.CreateTerminal  @"\+"
        let t_operator_mul          = x.CreateTerminalF @"\*"                   (fun _ -> Multiply)
        let t_operator_div          = x.CreateTerminalF @"/"                    (fun _ -> Divide)
        let t_operator_pow          = x.CreateTerminalF @"^"                    (fun _ -> Power)
        let t_operator_concat       = x.CreateTerminalF @"&"                    (fun _ -> StringConcat)
        let t_symbol_oparen         = x.CreateTerminal  @"\("
        let t_symbol_cparen         = x.CreateTerminal  @"\)"
        let t_symbol_obrack         = x.CreateTerminal  @"\["
        let t_symbol_cbrack         = x.CreateTerminal  @"\]"
     // let t_symbol_ocurly         = x.CreateTerminal  @"\{"
     // let t_symbol_ccurly         = x.CreateTerminal  @"\}"
     // let t_keyword_new           = x.CreateTerminal  @"new"
        let t_keyword_and           = x.CreateTerminalF @"and"                  (fun _ -> And)
        let t_keyword_or            = x.CreateTerminalF @"or"                   (fun _ -> Or)
        let t_keyword_not           = x.CreateTerminalF @"(not|!)"              (fun _ -> Not)
        let t_literal_true          = x.CreateTerminalF @"true"                 (fun _ -> True)
        let t_literal_false         = x.CreateTerminalF @"false"                (fun _ -> False)
        let t_literal_null          = x.CreateTerminalF @"null"                 (fun _ -> Null)
        let t_literal_default       = x.CreateTerminalF @"default"              (fun _ -> Default)
        let t_literal_empty         = x.CreateTerminalF @"empty"                (fun _ -> String "")
        let t_hex                   = x.CreateTerminalF @"(\+|-)?(0[xX][\da-fA-F]+|[\da-fA-F][hH])" (parse_num "0x" (fun s -> Int64.Parse(s.TrimEnd 'h', NumberStyles.HexNumber)))
        let t_bin                   = x.CreateTerminalF @"(\+|-)?0[bB][01]+"                        (parse_num "0b" (fun s -> Convert.ToInt64(s, 2)))
        let t_oct                   = x.CreateTerminalF @"(\+|-)?0[oO][0-7]+"                       (parse_num "0o" (fun s -> Convert.ToInt64(s, 8)))
        let t_dec                   = x.CreateTerminalF @"(\+|-)?\d+(\.\d+)?([eE](\+|-)?\d+)?"      (fun s -> match Decimal.TryParse s with
                                                                                                              | (true, d) -> d
                                                                                                              | _ -> Decimal.Parse(s, NumberStyles.Float)
                                                                                                              |> Number)
        let t_variable              = x.CreateTerminalF @"$[^\W\d]\w*"                              (fun s -> VARIABLE(s.Substring 1))
        let t_macro                 = x.CreateTerminalF @"@[^\W\d]\w*"                              (fun s -> MACRO(s.Substring 1))
        let t_string_1              = x.CreateTerminalF "\"(([^\"]*\"\"[^\"]*)*|[^\"]+)\""          (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Replace("\"\"", "\"")))
        let t_string_2              = x.CreateTerminalF @"'(([^']*''[^']*)*|[^']+)'"                (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Replace("''", "'")))
        let t_identifier            = x.CreateTerminalF @"([^\W\d_]|[^\W\d]\w*)"                    Identifier


        x.SetPrecedenceList (([
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
        |> List.toArray)


        reduce3 nt_result nt_assg_target nt_assg_op nt_any_expr (fun t o e -> AssignmentExpression(t, o, e))
        reduce1 nt_result nt_any_expr AnyExpression

        // reduce1 nt_assg_op t_symbol_equal (fun _ -> Assign)
        reduce1 nt_assg_op t_operator_assign_add (fun _ -> AssignAdd)
        reduce1 nt_assg_op t_operator_assign_sub (fun _ -> AssignSubtract)
        reduce1 nt_assg_op t_operator_assign_mul (fun _ -> AssignMultiply)
        reduce1 nt_assg_op t_operator_assign_div (fun _ -> AssignDivide)
        reduce1 nt_assg_op t_operator_assign_con (fun _ -> AssignConcat)

        reduce1 nt_assg_target t_variable VariableAssignment
        reduce1 nt_assg_target nt_index_expr IndexedAssignment
        reduce1 nt_assg_target nt_member_expr MemberAssignemnt

        reduce4 nt_index_expr nt_object_expr t_symbol_obrack nt_any_expr t_symbol_cbrack (fun e _ i _ -> (e, i))

        reduce3 nt_member_expr nt_object_expr t_symbol_dot t_identifier (fun e _ m -> ExplicitMemberAccess(e, m))
        reduce2 nt_member_expr t_symbol_dot t_identifier (fun _ m -> ImplicitMemberAccess m)
        
        reduce1 nt_object_expr t_variable Variable
        reduce1 nt_object_expr nt_index_expr Indexer
        reduce1 nt_object_expr nt_member_expr Member
        reduce1 nt_object_expr t_macro Macro
        reduce1 nt_object_expr nt_literal Literal
        reduce1 nt_object_expr nt_func_call FunctionCall
        reduce3 nt_object_expr t_symbol_oparen nt_any_expr t_symbol_cparen (fun _ e _ -> e)

        reduce0 nt_any_expr nt_object_expr
        reduce0 nt_any_expr nt_conditional_expr
        
        reduce5 nt_conditional_expr nt_any_expr t_symbol_questionmark nt_any_expr t_symbol_colon nt_any_expr (fun a _ b _ c -> Ternary(a, b, c))

        let reduce_binary symbol operator =
            //reduce3 nt_any_expr nt_any_expr symbol nt_any_expr (fun a _ b -> Binary(a, operator, b))
            reduce3 nt_any_expr nt_object_expr symbol nt_object_expr (fun a _ b -> Binary(a, operator, b))

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
        
        reduce2 nt_any_expr t_symbol_plus nt_any_expr (fun _ e -> Unary(Identity, e))
        reduce2 nt_any_expr t_symbol_minus nt_any_expr (fun _ e -> Unary(Negate, e))
        reduce2 nt_any_expr t_keyword_not nt_any_expr (fun _ e -> Unary(Not, e))

        reduce0 nt_literal t_literal_true
        reduce0 nt_literal t_literal_false
        reduce0 nt_literal t_literal_null
        reduce0 nt_literal t_literal_default
        reduce0 nt_literal t_literal_empty
        reduce0 nt_literal t_hex
        reduce0 nt_literal t_bin
        reduce0 nt_literal t_oct
        reduce0 nt_literal t_dec 
        reduce0 nt_literal t_string_1
        reduce0 nt_literal t_string_2

        reduce4 nt_func_call t_identifier t_symbol_oparen nt_args t_symbol_cparen (fun i _ a _ -> DirectFunctionCall(i, a))
        reduce4 nt_func_call nt_member_expr t_symbol_oparen nt_args t_symbol_cparen (fun m _ a _ -> MemberCall(m, a))

        reduce0 nt_args nt_arglist
        reducef nt_args (fun () -> [])
        
        reduce3 nt_arglist nt_arglist t_symbol_comma nt_any_expr (fun xs _ x -> xs@[x])
        reduce1 nt_arglist nt_any_expr (fun x -> [x])
