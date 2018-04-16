namespace AutoItExpressionParser

open AutoItExpressionParser.ExpressionAST

open System.Globalization


type long = System.Int64
type decimal = System.Decimal

type ExpressionParser() =
    inherit AbstractParser<EXPRESSION>()
    override x.BuildParser() =
        let nt_expression           = x.nt<EXPRESSION>()
        let nt_subexpr              = Array.map (fun _ -> x.nt<EXPRESSION>()) [| 0..7 |]
        let nt_funccall             = x.nt<FUNCCALL>()
        let nt_funcparams           = x.nt<EXPRESSION list>()
        let nt_literal              = x.nt<LITERAL>()

        let t_operator_comp_neq     = x.t @"<>"
        let t_operator_comp_gte     = x.t @">="
        let t_operator_comp_gt      = x.t @">"
        let t_operator_comp_lte     = x.t @"<="
        let t_operator_comp_lt      = x.t @"<"
        let t_operator_comp_eq      = x.t @"=="
        let t_symbol_equal          = x.t @"="
        let t_symbol_questionmark   = x.t @"\?"
        // let t_symbol_dot            = x.t @"\."
        let t_symbol_colon          = x.t @":"
        let t_symbol_comma          = x.t @","
        let t_symbol_minus          = x.t @"-"
        let t_symbol_plus           = x.t @"\+"
        let t_symbol_asterisk       = x.t @"\*"
        let t_symbol_slash          = x.t @"/"
        let t_symbol_percent        = x.t @"%"
        let t_symbol_hat            = x.t @"^"
        let t_symbol_ampersand      = x.t @"&"
        let t_symbol_oparen         = x.t @"\("
        let t_symbol_cparen         = x.t @"\)"
        let t_symbol_obrack         = x.t @"\["
        let t_symbol_cbrack         = x.t @"\]"
        let t_keyword_to            = x.t @"to"
        let t_keyword_and           = x.tf @"and"                                                  (fun _ -> And)
        let t_keyword_xor           = x.tf @"xor"                                                  (fun _ -> Xor)
        let t_keyword_or            = x.tf @"or"                                                   (fun _ -> Or)
        let t_keyword_not           = x.tf @"not"                                                  (fun _ -> Not)
        let t_literal_true          = x.tf @"true"                                                 (fun _ -> True)
        let t_literal_false         = x.tf @"false"                                                (fun _ -> False)
        let t_literal_null          = x.tf @"null"                                                 (fun _ -> Null)
        let t_literal_default       = x.tf @"default"                                              (fun _ -> Default)

        let parse p (f : string -> long) (s : string) =
            let s = s.TrimStart('+').ToLower().Replace(p, "")
            let n, s = if s.[0] = '-' then (true, s.Substring(1))
                        else (false, s)
            let l = f s
            if n then -l else l
            |> decimal
            |> Number

        let t_hex                   = x.tf @"(\+|-)?(0x[\da-f]+|[\da-f]h)"                         (parse "0x" (fun s -> long.Parse(s.TrimEnd('h'), NumberStyles.HexNumber)))
        let t_bin                   = x.tf @"(\+|-)?0b[01]+"                                       (parse "0b" (fun s -> System.Convert.ToInt64(s, 2)))
        let t_oct                   = x.tf @"(\+|-)?0o[0-7]+"                                      (parse "0o" (fun s -> System.Convert.ToInt64(s, 8)))
        let t_dec                   = x.tf @"(\+|-)?(\d+\.\d*(e(\+|-)?\d+)?|\.?\d+(e(\+|-)?\d+)?)" (fun s -> Number <| decimal.Parse(s))
        let t_variable              = x.tf @"$[a-zA-Z_][a-zA-Z0-9_]*"                              (fun s -> VARIABLE(s.Substring(1)))
        let t_macro                 = x.tf @"@[a-zA-Z_][a-zA-Z0-9_]*"                              (fun s -> MACRO(s.Substring(1)))
        let t_string_1              = x.tf "\"(([^\"]*\"\"[^\"]*)*|[^\"]+)\""                      (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Trim().Replace("\"\"", "\"")))
        let t_string_2              = x.tf "'(([^']*''[^']*)*|[^']+)'"                             (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Trim().Replace("''", "'")))
        let t_identifier            = x.tf @"[a-z0-9_]*"                                           id

        let (!@) x = nt_subexpr.[x]

        reduce0 nt_expression !@0

        reduce0 !@0 !@1
        reduce3 !@0 !@0 t_keyword_and !@1 (fun a _ b -> BinaryExpression(And, a, b))
        reduce3 !@0 !@0 t_keyword_or !@1 (fun a _ b -> BinaryExpression(Or, a, b))
        reduce3 !@0 !@0 t_keyword_xor !@1 (fun a _ b -> BinaryExpression(Xor, a, b))
        reduce0 !@1 !@2
        reduce3 !@1 !@1 t_operator_comp_lte !@2 (fun a _ b -> BinaryExpression(LowerEqual, a, b))
        reduce3 !@1 !@1 t_operator_comp_lt !@2 (fun a _ b -> BinaryExpression(Lower, a, b))
        reduce3 !@1 !@1 t_operator_comp_gte !@2 (fun a _ b -> BinaryExpression(GreaterEqual, a, b))
        reduce3 !@1 !@1 t_operator_comp_gt !@2 (fun a _ b -> BinaryExpression(Greater, a, b))
        reduce3 !@1 !@1 t_operator_comp_neq !@2 (fun a _ b -> BinaryExpression(Unequal, a, b))
        reduce3 !@1 !@1 t_operator_comp_eq !@2 (fun a _ b -> BinaryExpression(EqualCaseSensitive, a, b))
        reduce3 !@1 !@1 t_symbol_equal !@2 (fun a _ b -> BinaryExpression(EqualCaseInsensitive, a, b))
        reduce0 !@2 !@3
        reduce3 !@2 !@2 t_symbol_ampersand !@3 (fun a _ b -> BinaryExpression(StringConcat, a, b))
        reduce0 !@3 !@4
        reduce3 !@3 !@3 t_symbol_plus !@4 (fun a _ b -> BinaryExpression(Add, a, b))
        reduce3 !@3 !@3 t_symbol_minus !@4 (fun a _ b -> BinaryExpression(Subtract, a, b))
        reduce0 !@4 !@5
        reduce3 !@4 !@4 t_symbol_asterisk !@5 (fun a _ b -> BinaryExpression(Multiply, a, b))
        reduce3 !@4 !@4 t_symbol_slash !@5 (fun a _ b -> BinaryExpression(Divide, a, b))
        reduce3 !@4 !@4 t_symbol_percent !@5 (fun a _ b -> BinaryExpression(Modulus, a, b))
        reduce0 !@5 !@6
        reduce3 !@5 !@5 t_symbol_hat !@6 (fun a _ b -> BinaryExpression(Power, a, b))
        reduce0 !@6 !@7
        reduce2 !@6 t_symbol_plus !@6 (fun _ e -> e)
        reduce2 !@6 t_symbol_minus !@6 (fun _ e -> UnaryExpression(Negate, e))
        reduce2 !@6 t_keyword_not !@6 (fun _ e -> UnaryExpression(Not, e))
        reduce1 !@7 nt_literal Literal
        reduce1 !@7 nt_funccall FunctionCall
        reduce1 !@7 t_variable Variable
        reduce1 !@7 t_macro Macro
        reduce3 !@7 t_symbol_oparen nt_expression t_symbol_cparen (fun _ e _ -> e)
        reduce4 !@7 t_variable t_symbol_obrack nt_expression t_symbol_cbrack (fun v _ i _ -> ArrayIndex(v, i))
        reduce3 !@7 nt_expression t_keyword_to nt_expression (fun a _ b -> ToExpression(a, b))
        //reduce5 !@7 nt_expression t_symbol_questionmark nt_expression t_symbol_colon nt_expression (fun c _ a _ b -> TernaryExpression(c, a, b))

        reduce4 nt_funccall t_identifier t_symbol_oparen nt_funcparams t_symbol_cparen (fun f _ p _ -> (f, p))

        reduce3 nt_funcparams nt_expression t_symbol_comma nt_funcparams (fun e _ p -> e::p)
        reduce1 nt_funcparams nt_expression (fun e -> [e])

        reduce0 nt_literal t_literal_true
        reduce0 nt_literal t_literal_false
        reduce0 nt_literal t_literal_null
        reduce0 nt_literal t_literal_default
        reduce0 nt_literal t_string_1
        reduce0 nt_literal t_string_2
        reduce0 nt_literal t_hex
        reduce0 nt_literal t_dec
        reduce0 nt_literal t_oct
        reduce0 nt_literal t_bin

        x.Configuration.LexerSettings.Ignore <- [| @"\s+" |]

