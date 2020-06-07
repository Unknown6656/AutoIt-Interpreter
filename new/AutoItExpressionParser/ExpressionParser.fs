namespace Unknown6656.AutoIt3.ExpressionParser

open System.Text.RegularExpressions
open System.Globalization
open System

open Piglet.Parser.Configuration.Generic

open AST


type ExpressionParser() =
    inherit ParserConstructor<EXPRESSION>()

    member private x.CreateTerminalF s f = x.CreateTerminal(s, fun s -> f s)
    override x.Construct nt_result =
        let parse_num prefix (parser : string -> Int64) (input : string) =
            let s = input.TrimStart('+').ToLower().Replace(prefix, "")
            let n, s = if s.[0] = '-' then (true, s.Substring(1)) else (false, s)
            let l = parser s
            if n then -l else l
            |> Decimal
            |> Number

        let nt_array_indexers           = x.CreateNonTerminal<EXPRESSION list>                  "array-indexers"
        let nt_array_indexer            = x.CreateNonTerminal<EXPRESSION>                       "array-indexer"
        let nt_array_init_wrapper       = x.CreateNonTerminal<ARRAY_INIT_EXPRESSION list>       "array-init-wrapper"
        let nt_array_init_expression    = x.CreateNonTerminal<ARRAY_INIT_EXPRESSION list>       "array-init-expression"
        let nt_deref_target_expressions = x.CreateNonTerminal<EXPRESSION>                       "dereferencing-targets"
        let nt_expression_ext           = x.CreateNonTerminal<EXPRESSION>                       "extended-expression"
        let nt_expression               = x.CreateNonTerminal<EXPRESSION>                       "expression"
        let nt_at_expression            = x.CreateNonTerminal<EXPRESSION>                       "at-expression"
     // let nt_subexpression            = Array.map (x.CreateNonTerminal<EXPRESSION> << sprintf "expression-%d") [| 0..52 |]
        let nt_funccall                 = x.CreateNonTerminal<FUNCCALL>                         "function-call"
        let nt_funcparams               = x.CreateNonTerminal<EXPRESSION list>                  "function-parameters"
        let nt_literal                  = x.CreateNonTerminal<LITERAL>                          "literal"
        let nt_operator_binary_ass      = x.CreateNonTerminal<OPERATOR_ASSIGNMENT>              "binary-assignment-operator"
        let nt_dot_members              = x.CreateNonTerminal<MEMBER list>                      "dot-members"
        let nt_dot_member               = x.CreateNonTerminal<MEMBER list>                      "dot-member"
        let nt_inline_array_wrapper     = x.CreateNonTerminal<ARRAY_INIT_EXPRESSION list>       "inline-array-wrapper"
        let nt_inline_array_expression  = x.CreateNonTerminal<ARRAY_INIT_EXPRESSION list>       "inline-array-expression"
        let t_operator_assign_add       = x.CreateTerminal @"\+="
        let t_operator_assign_sub       = x.CreateTerminal @"-="
        let t_operator_assign_mul       = x.CreateTerminal @"\*="
        let t_operator_assign_div       = x.CreateTerminal @"/="
        let t_operator_assign_con       = x.CreateTerminal @"&="
        let t_operator_comp_neq         = x.CreateTerminal @"<>"
        let t_operator_comp_gte         = x.CreateTerminal @">="
        let t_operator_comp_gt          = x.CreateTerminal @">"
        let t_operator_comp_lte         = x.CreateTerminal @"<="
        let t_operator_comp_lt          = x.CreateTerminal @"<"
        let t_operator_comp_eq          = x.CreateTerminal @"=="
        let t_symbol_equal              = x.CreateTerminal @"="
        let t_symbol_questionmark       = x.CreateTerminal @"\?"
        let t_symbol_colon              = x.CreateTerminal @":"
        let t_symbol_dot                = x.CreateTerminal @"\."
        let t_symbol_comma              = x.CreateTerminal @","
        let t_symbol_minus              = x.CreateTerminal @"-"
        let t_symbol_plus               = x.CreateTerminal @"\+"
        let t_symbol_asterisk           = x.CreateTerminal @"\*"
        let t_symbol_slash              = x.CreateTerminal @"/"
        let t_symbol_hat                = x.CreateTerminal @"^"
        let t_symbol_ampersand          = x.CreateTerminal @"&"
        let t_symbol_oparen             = x.CreateTerminal @"\("
        let t_symbol_cparen             = x.CreateTerminal @"\)"
        let t_symbol_obrack             = x.CreateTerminal @"\["
        let t_symbol_cbrack             = x.CreateTerminal @"\]"
        let t_symbol_ocurly             = x.CreateTerminal @"\{"
        let t_symbol_ccurly             = x.CreateTerminal @"\}"
        let t_keyword_to                = x.CreateTerminal @"to"
        let t_keyword_new               = x.CreateTerminal @"new"
        let t_keyword_and               = x.CreateTerminal @"and"
        let t_keyword_or                = x.CreateTerminal @"or"
        let t_keyword_not               = x.CreateTerminal @"(not|!)"
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
        let t_identifier                = x.CreateTerminalF @"[_a-z]\w*"                        id


        reduce1 nt_result nt_literal Literal

        // TODO

        ()


