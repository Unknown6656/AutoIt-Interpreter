module AutoItExpressionParser.ExpressionParser

open AutoItExpressionParser.Util
open AutoItExpressionParser.ExpressionAST

open System.Globalization


type int64 = System.Int64
type decimal = System.Decimal


let private conf = create()

conf.LexerSettings.IgnoreCase <- true

let private nt<'a> = nterm<'a> conf
let private tf<'a> = termf<'a> conf
let private t = term conf
let private a d x = assoc conf d x |> ignore

let private nt_expression           = nt<EXPRESSION>()
let private nt_subexpr              = Array.map (fun _ -> nt<EXPRESSION>()) [| 0..10 |]
let private nt_funccall             = nt<FUNCCALL>()
let private nt_funcparams           = nt<EXPRESSION list>()
let private nt_literal              = nt<LITERAL>()

let private t_operator_comp_neq     = t @"<>"
let private t_operator_comp_gte     = t @">="
let private t_operator_comp_gt      = t @">"
let private t_operator_comp_lte     = t @"<="
let private t_operator_comp_lt      = t @"<"
let private t_operator_comp_eq      = t @"=="
let private t_symbol_equal          = t @"="
let private t_symbol_questionmark   = t @"\?"
// let private t_symbol_dot            = t @"\."
let private t_symbol_colon          = t @":"
let private t_symbol_comma          = t @","
let private t_symbol_minus          = t @"-"
let private t_symbol_plus           = t @"\+"
let private t_symbol_asterisk       = t @"\*"
let private t_symbol_slash          = t @"/"
let private t_symbol_percent        = t @"%"
let private t_symbol_hat            = t @"^"
let private t_symbol_ampersand      = t @"&"
let private t_symbol_oparen         = t @"\("
let private t_symbol_cparen         = t @"\)"
let private t_symbol_obrack         = t @"\["
let private t_symbol_cbrack         = t @"\]"
let private t_keyword_and           = tf @"and"                                                  (fun _ -> And)
let private t_keyword_xor           = tf @"xor"                                                  (fun _ -> Xor)
let private t_keyword_or            = tf @"or"                                                   (fun _ -> Or)
let private t_keyword_not           = tf @"not"                                                  (fun _ -> Not)
let private t_literal_true          = tf @"true"                                                 (fun _ -> True)
let private t_literal_false         = tf @"false"                                                (fun _ -> False)
let private t_literal_null          = tf @"null"                                                 (fun _ -> Null)
let private t_literal_default       = tf @"default"                                              (fun _ -> Default)

let private parse p (f : string -> int64) (s : string) =
    let s = s.TrimStart('+').ToLower().Replace(p, "")
    let n, s = if s.[0] = '-' then (true, s.Substring(1))
               else (false, s)
    let l = f s
    if n then -l else l
    |> decimal
    |> Number

let private t_hex                   = tf @"(\+|-)?(0x[\da-f]+|[\da-f]h)"                         (parse "0x" (fun s -> int64.Parse(s.TrimEnd('h'), NumberStyles.HexNumber)))
let private t_bin                   = tf @"(\+|-)?0b[01]+"                                       (parse "0b" (fun s -> System.Convert.ToInt64(s, 2)))
let private t_oct                   = tf @"(\+|-)?0o[0-7]+"                                      (parse "0o" (fun s -> System.Convert.ToInt64(s, 8)))
let private t_dec                   = tf @"(\+|-)?(\d+\.\d*(e(\+|-)?\d+)?|\.?\d+(e(\+|-)?\d+)?)" (fun s -> Number <| decimal.Parse(s))
let private t_variable              = tf @"$[a-zA-Z_][a-zA-Z0-9_]*"                              (fun s -> VARIABLE(s.Substring(1)))
let private t_macro                 = tf @"@[a-zA-Z_][a-zA-Z0-9_]*"                              (fun s -> MACRO(s.Substring(1)))
let private t_string_1              = tf "\"(([^\"]*\"\"[^\"]*)*|[^\"]+)\""                      (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Trim().Replace("\"\"", "\"")))
let private t_string_2              = tf "'(([^']*''[^']*)*|[^']+)'"                             (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Trim().Replace("''", "'")))
let private t_identifier            = tf @"[a-z0-9_]*"                                           id

let private (!@) x = nt_subexpr.[x]

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

conf.LexerSettings.Ignore <- [| @"\s+" |]

let private Parser = conf.CreateParser()
let Parse (s : string) = Parser.Parse(s.Replace('\t', ' ')) :?> EXPRESSION
