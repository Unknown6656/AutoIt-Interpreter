namespace Unknown6656.AutoIt3.ExpressionParser

open System.Text.RegularExpressions
open System.Globalization
open System

open Piglet.Parser.Configuration.Generic

open AST


type ExpressionParser() =
    inherit ParserConstructor<PARSABLE_EXPRESSION>()

    member private x.CreateTerminalF s f = x.CreateTerminal(s, fun s -> f s)
    override x.Construct nt_result =
        let parse_num prefix (parser : string -> Int64) (input : string) =
            let s = input.TrimStart('+').ToLower().Replace(prefix, "")
            let n, s = if s.[0] = '-' then (true, s.Substring(1)) else (false, s)
            let l = parser s
            if n then -l else l
            |> Decimal
            |> Number
            
        let nt_expression               = x.CreateNonTerminal<EXPRESSION>                 "expr"
        let nt_literal                  = x.CreateNonTerminal<LITERAL>                    "literal"
        let nt_unary_expression         = x.CreateNonTerminal<UNARY_EXPRESSION>           "un-expr"
        let nt_binary_expression        = x.CreateNonTerminal<BINARY_EXPRESSION>          "bin-expr"
        let nt_ternary_expression       = x.CreateNonTerminal<TERNARY_EXPRESSION>         "ter-expr"
        let nt_assignment_expression    = x.CreateNonTerminal<ASSIGNMENT_EXPRESSION>      "assg-expr"
        let nt_assg_target              = x.CreateNonTerminal<ASSIGNMENT_TARGET>          "assg-targ"
        let nt_indexer_expression       = x.CreateNonTerminal<INDEXER_EXPRESSION>         "idx-expr"
        let nt_member_expression        = x.CreateNonTerminal<MEMBER_EXPRESSION>          "member-expr"
        let nt_funccall_expression      = x.CreateNonTerminal<FUNCCALL_EXPRESSION>        "funccall-expr"
        let nt_funccall_arguments       = x.CreateNonTerminal<FUNCCALL_ARGUMENTS>         "funccall-args"
        let nt_operator_unary           = x.CreateNonTerminal<OPERATOR_UNARY>             "un-op"
        let nt_operator_binary          = x.CreateNonTerminal<OPERATOR_BINARY>            "bin-op"
        let nt_operator_binary_assg     = x.CreateNonTerminal<OPERATOR_ASSIGNMENT>        "assg-op"

        let t_operator_assign_add       = x.CreateTerminalF @"\+="                              (fun _ -> AssignAdd)
        let t_operator_assign_sub       = x.CreateTerminalF @"-="                               (fun _ -> AssignSubtract)
        let t_operator_assign_mul       = x.CreateTerminalF @"\*="                              (fun _ -> AssignMultiply)
        let t_operator_assign_div       = x.CreateTerminalF @"/="                               (fun _ -> AssignDivide)
        let t_operator_assign_con       = x.CreateTerminalF @"&="                               (fun _ -> AssignConcat)
        let t_operator_comp_neq         = x.CreateTerminalF @"<>"                               (fun _ -> Unequal)
        let t_operator_comp_gte         = x.CreateTerminalF @">="                               (fun _ -> GreaterEqual)
        let t_operator_comp_gt          = x.CreateTerminalF @">"                                (fun _ -> Greater)
        let t_operator_comp_lte         = x.CreateTerminalF @"<="                               (fun _ -> LowerEqual)
        let t_operator_comp_lt          = x.CreateTerminalF @"<"                                (fun _ -> Lower)
        let t_operator_comp_eq          = x.CreateTerminalF @"=="                               (fun _ -> EqualCaseSensitive)
        let t_symbol_equal              = x.CreateTerminal @"="
        let t_symbol_questionmark       = x.CreateTerminal @"\?"
        let t_symbol_colon              = x.CreateTerminal @":"
        let t_symbol_dot                = x.CreateTerminal @"\."
        let t_symbol_comma              = x.CreateTerminal @","
        let t_symbol_minus              = x.CreateTerminal @"-"
        let t_symbol_plus               = x.CreateTerminal @"\+"
        let t_opeator_mul               = x.CreateTerminalF @"\*"                               (fun _ -> Multiply)
        let t_opeator_div               = x.CreateTerminalF @"/"                                (fun _ -> Divide)
        let t_opeator_pow               = x.CreateTerminalF @"^"                                (fun _ -> Power)
        let t_opeator_concat            = x.CreateTerminalF @"&"                                (fun _ -> StringConcat)
        let t_symbol_oparen             = x.CreateTerminal @"\("
        let t_symbol_cparen             = x.CreateTerminal @"\)"
        let t_symbol_obrack             = x.CreateTerminal @"\["
        let t_symbol_cbrack             = x.CreateTerminal @"\]"
     // let t_symbol_ocurly             = x.CreateTerminal @"\{"
     // let t_symbol_ccurly             = x.CreateTerminal @"\}"
        let t_keyword_new               = x.CreateTerminal @"new"
        let t_keyword_and               = x.CreateTerminalF @"and"                              (fun _ -> And)
        let t_keyword_or                = x.CreateTerminalF @"or"                               (fun _ -> Or)
        let t_keyword_not               = x.CreateTerminalF @"(not|!)"                          (fun _ -> Not)
        let t_literal_true              = x.CreateTerminalF @"true"                             (fun _ -> True)
        let t_literal_false             = x.CreateTerminalF @"false"                            (fun _ -> False)
        let t_literal_null              = x.CreateTerminalF @"null"                             (fun _ -> Null)
        let t_literal_default           = x.CreateTerminalF @"default"                          (fun _ -> Default)
        let t_literal_empty             = x.CreateTerminalF @"empty"                            (fun _ -> String "")
        let t_hex                       = x.CreateTerminalF @"(\+|-)?(0x[\da-f]+|[\da-f]h)"     (parse_num "0x" (fun s -> Int64.Parse(s.TrimEnd 'h', NumberStyles.HexNumber)))
        let t_bin                       = x.CreateTerminalF @"(\+|-)?0b[01]+"                   (parse_num "0b" (fun s -> Convert.ToInt64(s, 2)))
        let t_oct                       = x.CreateTerminalF @"(\+|-)?0o[0-7]+"                  (parse_num "0o" (fun s -> Convert.ToInt64(s, 8)))
        let t_dec                       = x.CreateTerminalF @"(\+|-)?\d+(\.\d+)?(e(\+|-)?\d+)?" (fun s -> match Decimal.TryParse s with
                                                                                                          | (true, d) -> d
                                                                                                          | _ -> Decimal.Parse(s, NumberStyles.Float)
                                                                                                          |> Number)
        let t_variable                  = x.CreateTerminalF @"$[a-z_]\w*"                       (fun s -> VARIABLE(s.Substring 1))
        let t_macro                     = x.CreateTerminalF @"@[a-z_]\w*"                       (fun s -> MACRO(s.Substring 1))
        let t_string_1                  = x.CreateTerminalF "\"(([^\"]*\"\"[^\"]*)*|[^\"]+)\""  (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Replace("\"\"", "\"")))
        let t_string_2                  = x.CreateTerminalF @"'(([^']*''[^']*)*|[^']+)'"        (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Replace("''", "'")))
        let t_identifier                = x.CreateTerminalF @"[_a-z]\w*"                        Identifier








        (*
        reduce0 nt_result nt_expression
        
        reduce3 nt_expression t_symbol_oparen nt_expression t_symbol_cparen (fun _ e _ -> e)
        reduce1 nt_expression nt_funccall FunctionCall
        reduce1 nt_expression nt_unary_expression id
        reduce1 nt_expression nt_binary_expression id
        reduce1 nt_expression nt_ternary_expression id
        reduce1 nt_expression nt_assignment_expression AssignmentExpression
        reduce1 nt_expression nt_literal Literal
        reduce1 nt_expression t_macro Macro
        reduce2 nt_expression nt_expression nt_array_indexer (fun a i -> ArrayAccess(a, i))
        reduce2 nt_expression nt_expression nt_dot_member (fun e m -> DotAccess(e, m))
        reduce1 nt_expression nt_dot_member ContextualDotAccess

        reduce nt_funccall nt_dot_membername

        reduce1 nt_literal t_literal_true id
        reduce1 nt_literal t_literal_false id
        reduce1 nt_literal t_literal_null id
        reduce1 nt_literal t_literal_default id
        reduce1 nt_literal t_literal_empty id
        reduce1 nt_literal t_hex id
        reduce1 nt_literal t_bin id
        reduce1 nt_literal t_oct id
        reduce1 nt_literal t_dec id
        reduce1 nt_literal t_string_1 id
        reduce1 nt_literal t_string_2 id

        reduce2 nt_unary_expression nt_operator_unary nt_expression (fun o e -> UnaryExpression(o, e))
        reduce1 nt_operator_unary t_keyword_not id
        reduce1 nt_operator_unary t_symbol_plus (fun _ -> Identity)
        reduce1 nt_operator_unary t_symbol_minus (fun _ -> Negate)

        reduce3 nt_binary_expression nt_expression nt_operator_binary nt_expression (fun e1 o e2 -> BinaryExpression(o, e1, e2))
        reduce1 nt_operator_binary t_operator_comp_neq id
        reduce1 nt_operator_binary t_operator_comp_gte id
        reduce1 nt_operator_binary t_operator_comp_gt id
        reduce1 nt_operator_binary t_operator_comp_lte id
        reduce1 nt_operator_binary t_operator_comp_lt id
        reduce1 nt_operator_binary t_operator_comp_eq id
        reduce1 nt_operator_binary t_symbol_minus (fun _ -> Subtract)
        reduce1 nt_operator_binary t_symbol_plus (fun _ -> Add)
        reduce1 nt_operator_binary t_opeator_mul id
        reduce1 nt_operator_binary t_opeator_div id
        reduce1 nt_operator_binary t_opeator_pow id
        reduce1 nt_operator_binary t_opeator_concat id
        reduce1 nt_operator_binary t_keyword_and id
        reduce1 nt_operator_binary t_keyword_or id

        reduce5 nt_ternary_expression nt_expression t_symbol_questionmark nt_expression t_symbol_colon nt_expression (fun c _ a _ b -> TernaryExpression(c, a, b))

        reduce3 nt_assignment_expression t_variable nt_operator_binary_assg nt_expression (fun v o e -> ScalarAssignment(v, o, e))
        reduce4 nt_assignment_expression nt_expression nt_dot_membername nt_operator_binary_assg nt_expression (fun e1 m o e2 -> MemberAssignment(e1, m, o, e2))
        reduce4 nt_assignment_expression nt_expression nt_array_indexers nt_operator_binary_assg nt_expression (fun e1 xs o e2 -> ArrayAssignment(e1, xs, o, e2))
        reduce1 nt_operator_binary_assg t_operator_assign_add id
        reduce1 nt_operator_binary_assg t_operator_assign_sub id
        reduce1 nt_operator_binary_assg t_operator_assign_mul id
        reduce1 nt_operator_binary_assg t_operator_assign_div id
        reduce1 nt_operator_binary_assg t_operator_assign_con id
        reduce1 nt_operator_binary_assg t_symbol_equal (fun _ -> Assign)

        reduce3 nt_array_indexer t_symbol_obrack nt_expression t_symbol_cbrack (fun _ e _ -> e)
        reduce2 nt_array_indexers nt_array_indexers nt_array_indexer (fun xs x -> xs@[x])
        reduce1 nt_array_indexers nt_array_indexer (fun x -> [x])
        *)
        ()

