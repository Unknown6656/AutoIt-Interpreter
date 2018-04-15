module AutoItExpressionParser.ExpressionParser

open AutoItExpressionParser.Util
open AutoItExpressionParser.ExpressionAST

open System.Globalization


type int64 = System.Int64
type decimal = System.Decimal


let private conf = create()
let private nt<'a> = nterm<'a> conf
let private tf<'a> = termf<'a> conf
let private t = term conf
let private a d x = assoc conf d x |> ignore

let private nt_expression           = nt<EXPRESSION>()
let private nt_funccall             = nt<FUNCCALL>()
let private nt_funcparams           = nt<EXPRESSION list>()
let private nt_literal              = nt<LITERAL>()
let private nt_operator_unary       = nt<OPERATOR_UNARY>()
let private nt_operator_binary_log  = nt<OPERATOR_BINARY_LOGIC>()
let private nt_operator_binary_num  = nt<OPERATOR_BINARY_NUMERIC>()
let private nt_operator_binary_cmp  = nt<OPERATOR_BINARY_COMPARISON>()
let private nt_operator_binary      = nt<OPERATOR_BINARY>()

let private t_operator_comp_neq     = t @"<>"
let private t_operator_comp_gte     = t @">="
let private t_operator_comp_gt      = t @">"
let private t_operator_comp_lte     = t @"<="
let private t_operator_comp_lt      = t @"<"
let private t_operator_comp_eq      = t @"=="
let private t_symbol_equal          = t @"="
let private t_symbol_questionmark   = t @"\?"
let private t_symbol_dot            = t @"."
let private t_symbol_colon          = t @":"
let private t_symbol_comma          = t @","
let private t_symbol_minus          = t @"-"
let private t_symbol_plus           = t @"+"
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
let private t_variable              = tf @"$[a-z_][a-z0-9_]*"                                    (fun s -> VARIABLE(s.Substring(1)))
let private t_macro                 = tf @"@[a-z_][a-z0-9_]*"                                    (fun s -> MACRO(s.Substring(1)))
let private t_string                = tf "\"(([^\"]*\"\"[^\"]*)*|[^\"]+)\""                      (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Trim()))
let private t_identifier            = tf @"[a-z0-9_]*"                                           id

a Left [ t_keyword_and; t_keyword_xor; t_keyword_or ]
a Left [ t_symbol_questionmark ]
a Left [ t_symbol_colon ]
a Right [ t_operator_comp_lt; t_operator_comp_gt; t_operator_comp_lte;t_operator_comp_gte; t_operator_comp_neq; t_operator_comp_eq; t_symbol_equal ]
a Left [ t_symbol_ampersand ]
a Left [ t_symbol_minus; t_symbol_plus ]
a Left [ t_symbol_asterisk; t_symbol_slash; t_symbol_percent ]
a Left [ t_symbol_hat ]
a Left [ t_keyword_not ]
a Right [ t_symbol_dot ]

let private unaryExpressionPrecedenceGroup  = conf.RightAssociative()

reduce3 nt_expression t_symbol_oparen nt_expression t_symbol_cparen (fun _ e _ -> e)
reduce1 nt_expression nt_literal Literal
reduce1 nt_expression nt_funccall FunctionCall
reduce1 nt_expression t_variable Variable
reduce1 nt_expression t_macro Macro
reduce4 nt_expression t_variable t_symbol_obrack nt_expression t_symbol_cbrack (fun v _ i _ -> ArrayIndex(v, i))
reduce3 nt_expression nt_expression nt_operator_binary nt_expression (fun a o b -> BinaryExpression(o, a, b))
//reduce5 nt_expression nt_expression t_symbol_questionmark nt_expression t_symbol_colon nt_expression (fun c _ a _ b -> TernaryExpression(c, a, b))

let private uexprp = nt_expression.AddProduction(nt_operator_unary, nt_expression)
uexprp.SetReduceFunction (fun o e -> UnaryExpression(o, e))
uexprp.SetPrecedence unaryExpressionPrecedenceGroup

reduce4 nt_funccall t_identifier t_symbol_oparen nt_funcparams t_symbol_cparen (fun f _ p _ -> (f, p))

reduce3 nt_funcparams nt_expression t_symbol_comma nt_funcparams (fun e _ p -> e::p)
reduce1 nt_funcparams nt_expression (fun e -> [e])

reduce0 nt_literal t_literal_true
reduce0 nt_literal t_literal_false
reduce0 nt_literal t_literal_null
reduce0 nt_literal t_literal_default
reduce0 nt_literal t_string
reduce0 nt_literal t_hex
reduce0 nt_literal t_dec
reduce0 nt_literal t_oct
reduce0 nt_literal t_bin

reduce1 nt_operator_binary_num t_symbol_plus (fun _ -> Add)
reduce1 nt_operator_binary_num t_symbol_minus (fun _ -> Subtract)
reduce1 nt_operator_binary_num t_symbol_asterisk (fun _ -> Multiply)
reduce1 nt_operator_binary_num t_symbol_slash (fun _ -> Divide)
reduce1 nt_operator_binary_num t_symbol_percent (fun _ -> Modulus)
reduce1 nt_operator_binary_num t_symbol_hat (fun _ -> Power)

reduce1 nt_operator_binary_cmp t_symbol_equal (fun _ -> EqualCaseInsensitive)
reduce1 nt_operator_binary_cmp t_operator_comp_eq (fun _ -> EqualCaseSensitive)
reduce1 nt_operator_binary_cmp t_operator_comp_neq (fun _ -> Unequal)
reduce1 nt_operator_binary_cmp t_operator_comp_gt (fun _ -> Greater)
reduce1 nt_operator_binary_cmp t_operator_comp_gte (fun _ -> GreaterEqual)
reduce1 nt_operator_binary_cmp t_operator_comp_lt (fun _ -> Lower)
reduce1 nt_operator_binary_cmp t_operator_comp_lte (fun _ -> LowerEqual)

reduce0 nt_operator_binary_log t_keyword_and
reduce0 nt_operator_binary_log t_keyword_xor
reduce0 nt_operator_binary_log t_keyword_or

reduce0 nt_operator_binary nt_operator_binary_num
reduce0 nt_operator_binary nt_operator_binary_cmp
reduce0 nt_operator_binary nt_operator_binary_log
reduce1 nt_operator_binary t_symbol_ampersand (fun _ -> StringConcat)

reduce0 nt_operator_unary t_symbol_minus
reduce0 nt_operator_unary t_keyword_not

conf.LexerSettings.Ignore <- [| @"\s+"; |]

let private Parser = conf.CreateParser()
let Parse (s : string) = Parser.Parse(s.Replace('\t', ' ')) :?> EXPRESSION
