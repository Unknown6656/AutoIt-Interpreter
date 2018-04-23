namespace AutoItExpressionParser

open AutoItExpressionParser.ExpressionAST

open System.Globalization


type long = System.Int64
type decimal = System.Decimal


type ExpressionParser(optimize : bool) =
    inherit AbstractParser<MULTI_EXPRESSION list>()
    member x.UseOptimization = optimize
    override x.BuildParser() =
        let lparse p (f : string -> long) (s : string) =
            let s = s.TrimStart('+').ToLower().Replace(p, "")
            let n, s = if s.[0] = '-' then (true, s.Substring(1))
                        else (false, s)
            let l = f s
            if n then -l else l
            |> decimal
            |> Number
            
        let nt_multi_expressions        = x.nt<MULTI_EXPRESSION list>()
        let nt_multi_expression         = x.nt<MULTI_EXPRESSION>()
        let nt_expression_ext           = x.nt<EXPRESSION>()
        let nt_expression               = x.nt<EXPRESSION>()
        let nt_subexpression            = Array.map (fun _ -> x.nt<EXPRESSION>()) [| 0..8 |]
        let nt_funccall                 = x.nt<FUNCCALL>()
        let nt_funcparams               = x.nt<EXPRESSION list>()
        let nt_literal                  = x.nt<LITERAL>()
        let nt_assignment_expression    = x.nt<ASSIGNMENT_EXPRESSION>()
        let nt_operator_binary_ass      = x.nt<OPERATOR_ASSIGNMENT>()
        let nt_variable_expression      = x.nt<VARIABLE_EXPRESSION>()
        let nt_dot_members              = x.nt<MEMBER list>()
        let nt_dot_member               = x.nt<MEMBER>()
        
        let t_operator_assign_nand      = x.t @"~&&="
        let t_operator_assign_and       = x.t @"&&="
        let t_operator_assign_nxor      = x.t @"~^^="
        let t_operator_assign_xor       = x.t @"^^="
        let t_operator_assign_nor       = x.t @"~\|\|="
        let t_operator_assign_or        = x.t @"\|\|="
        let t_operator_assign_rol       = x.t @"<<<="
        let t_operator_assign_ror       = x.t @">>>="
        let t_operator_assign_shl       = x.t @"<<="
        let t_operator_assign_shr       = x.t @">>="
        let t_operator_assign_add       = x.t @"\+="
        let t_operator_assign_sub       = x.t @"-="
        let t_operator_assign_mul       = x.t @"\*="
        let t_operator_assign_div       = x.t @"/="
        let t_operator_assign_mod       = x.t @"%="
        let t_operator_assign_con       = x.t @"&="
        let t_operator_assign_pow       = x.t @"^="
        let t_operator_bit_nand         = x.t @"~&&"
        let t_operator_bit_and          = x.t @"&&"
        let t_operator_bit_nxor         = x.t @"~^^"
        let t_operator_bit_xor          = x.t @"^^"
        let t_operator_bit_nor          = x.t @"~\|\|"
        let t_operator_bit_or           = x.t @"\|\|"
        let t_operator_bit_rol          = x.t @"<<<"
        let t_operator_bit_ror          = x.t @">>>"
        let t_operator_bit_shl          = x.t @"<<"
        let t_operator_bit_shr          = x.t @">>"
        let t_operator_bit_not          = x.t @"~"
        let t_operator_comp_neq         = x.t @"<>"
        let t_operator_comp_gte         = x.t @">="
        let t_operator_comp_gt          = x.t @">"
        let t_operator_comp_lte         = x.t @"<="
        let t_operator_comp_lt          = x.t @"<"
        let t_operator_comp_eq          = x.t @"=="
        let t_symbol_equal              = x.t @"="
        let t_symbol_questionmark       = x.t @"\?" // TODO
        let t_symbol_colon              = x.t @":" // TODO
        let t_symbol_dot                = x.t @"\."
        let t_symbol_comma              = x.t @","
        let t_symbol_minus              = x.t @"-"
        let t_symbol_plus               = x.t @"\+"
        let t_symbol_asterisk           = x.t @"\*"
        let t_symbol_slash              = x.t @"/"
        let t_symbol_percent            = x.t @"%"
        let t_symbol_hat                = x.t @"^"
        let t_symbol_ampersand          = x.t @"&"
        let t_symbol_oparen             = x.t @"\("
        let t_symbol_cparen             = x.t @"\)"
        let t_symbol_obrack             = x.t @"\["
        let t_symbol_cbrack             = x.t @"\]"
        let t_keyword_to                = x.t @"to"
        let t_keyword_nand              = x.t @"nand"
        let t_keyword_nor               = x.t @"nor"
        let t_keyword_and               = x.t @"and"
        let t_keyword_nxor              = x.t @"nxor"
        let t_keyword_xor               = x.t @"xor"
        let t_keyword_or                = x.t @"or"
        let t_keyword_not               = x.t @"(not|!)"
        let t_literal_true              = x.tf @"true"                                                 (fun _ -> True)
        let t_literal_false             = x.tf @"false"                                                (fun _ -> False)
        let t_literal_null              = x.tf @"null"                                                 (fun _ -> Null)
        let t_literal_default           = x.tf @"default"                                              (fun _ -> Default)
        let t_hex                       = x.tf @"(\+|-)?(0x[\da-f]+|[\da-f]h)"                         (lparse "0x" (fun s -> long.Parse(s.TrimEnd 'h', NumberStyles.HexNumber)))
        let t_bin                       = x.tf @"(\+|-)?0b[01]+"                                       (lparse "0b" (fun s -> System.Convert.ToInt64(s, 2)))
        let t_oct                       = x.tf @"(\+|-)?0o[0-7]+"                                      (lparse "0o" (fun s -> System.Convert.ToInt64(s, 8)))
        let t_dec                       = x.tf @"(\+|-)?(\d+\.\d*(e(\+|-)?\d+)?|\.?\d+(e(\+|-)?\d+)?)" (fun s -> match decimal.TryParse s with
                                                                                                                 | (true, d) -> d
                                                                                                                 | _ -> decimal.Parse(s, NumberStyles.Float)
                                                                                                                 |> Number
                                                                                                       ) 
        let t_variable                  = x.tf @"$[a-zA-Z_][a-zA-Z0-9_]*"                              (fun s -> VARIABLE(s.Substring 1))
        let t_macro                     = x.tf @"@[a-zA-Z_][a-zA-Z0-9_]*"                              (fun s -> MACRO(s.Substring 1))
        let t_string_1                  = x.tf "\"(([^\"]*\"\"[^\"]*)*|[^\"]+)\""                      (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Trim().Replace("\"\"", "\"")))
        let t_string_2                  = x.tf "'(([^']*''[^']*)*|[^']+)'"                             (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Trim().Replace("''", "'")))
        let t_identifier                = x.tf @"[a-z0-9_]*"                                           id

        let (!@) x = nt_subexpression.[x]

        reduce1 nt_multi_expression nt_expression_ext SingleValue
        reduce3 nt_multi_expression nt_expression_ext t_keyword_to nt_expression_ext (fun a _ b -> ValueRange(a, b))
        reduce1 nt_multi_expressions nt_multi_expression (fun m -> [m])
        reduce3 nt_multi_expressions nt_multi_expression t_symbol_comma nt_multi_expressions (fun m _ ms -> m::ms)

        reduce1 nt_variable_expression t_variable Variable
        reduce3 nt_variable_expression t_variable t_symbol_dot nt_dot_members (fun v _ m -> DotAccess(v, m))
        
        reduce1 nt_dot_members nt_dot_member (fun i -> [i])
        reduce3 nt_dot_members nt_dot_member t_symbol_dot nt_dot_members (fun x _ xs -> x::xs)

        reduce1 nt_dot_member t_identifier Field
        reduce1 nt_dot_member nt_funccall Method
        
        reduce1 nt_expression_ext nt_expression (fun e -> if x.UseOptimization then Analyzer.ProcessExpression e else e)
        reduce1 nt_expression_ext nt_assignment_expression AssignmentExpression

        reduce0 nt_expression !@0

        // TODO  : change precedence inside each precedence group (?)

        reduce0 !@0 !@1
        reduce3 !@0 !@0 t_keyword_nand !@1 (fun a _ b -> BinaryExpression(Nand, a, b))
        reduce3 !@0 !@0 t_keyword_and !@1 (fun a _ b -> BinaryExpression(And, a, b))
        reduce3 !@0 !@0 t_keyword_nor !@1 (fun a _ b -> BinaryExpression(Nor, a, b))
        reduce3 !@0 !@0 t_keyword_nxor !@1 (fun a _ b -> BinaryExpression(Nxor, a, b))
        reduce3 !@0 !@0 t_keyword_xor !@1 (fun a _ b -> BinaryExpression(Xor, a, b))
        reduce3 !@0 !@0 t_keyword_or !@1 (fun a _ b -> BinaryExpression(Or, a, b))
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
        reduce3 !@3 !@3 t_operator_bit_nand !@4 (fun a _ b -> BinaryExpression(BitwiseNand, a, b))
        reduce3 !@3 !@3 t_operator_bit_and !@4 (fun a _ b -> BinaryExpression(BitwiseAnd, a, b))
        reduce3 !@3 !@3 t_operator_bit_nxor !@4 (fun a _ b -> BinaryExpression(BitwiseNxor, a, b))
        reduce3 !@3 !@3 t_operator_bit_xor !@4 (fun a _ b -> BinaryExpression(BitwiseXor, a, b))
        reduce3 !@3 !@3 t_operator_bit_nor !@4 (fun a _ b -> BinaryExpression(BitwiseNor, a, b))
        reduce3 !@3 !@3 t_operator_bit_or !@4 (fun a _ b -> BinaryExpression(BitwiseOr, a, b))
        reduce3 !@3 !@3 t_operator_bit_rol !@4 (fun a _ b -> BinaryExpression(BitwiseRotateLeft, a, b))
        reduce3 !@3 !@3 t_operator_bit_ror !@4 (fun a _ b -> BinaryExpression(BitwiseRotateRight, a, b))
        reduce3 !@3 !@3 t_operator_bit_shl !@4 (fun a _ b -> BinaryExpression(BitwiseShiftLeft, a, b))
        reduce3 !@3 !@3 t_operator_bit_shr !@4 (fun a _ b -> BinaryExpression(BitwiseShiftRight, a, b))
        reduce0 !@4 !@5
        reduce3 !@4 !@4 t_symbol_plus !@5 (fun a _ b -> BinaryExpression(Add, a, b))
        reduce3 !@4 !@4 t_symbol_minus !@5 (fun a _ b -> BinaryExpression(Subtract, a, b))
        reduce0 !@5 !@6
        reduce3 !@5 !@5 t_symbol_asterisk !@6 (fun a _ b -> BinaryExpression(Multiply, a, b))
        reduce3 !@5 !@5 t_symbol_slash !@6 (fun a _ b -> BinaryExpression(Divide, a, b))
        reduce3 !@5 !@5 t_symbol_percent !@6 (fun a _ b -> BinaryExpression(Modulus, a, b))
        reduce0 !@6 !@7
        reduce3 !@6 !@6 t_symbol_hat !@7 (fun a _ b -> BinaryExpression(Power, a, b))
        reduce0 !@7 !@8
        reduce2 !@7 t_symbol_plus !@7 (fun _ e -> e)
        reduce2 !@7 t_symbol_minus !@7 (fun _ e -> UnaryExpression(Negate, e))
        reduce2 !@7 t_keyword_not !@7 (fun _ e -> UnaryExpression(Not, e))
        reduce2 !@7 t_operator_bit_not !@7 (fun _ e -> UnaryExpression(BitwiseNot, e))
        reduce1 !@8 nt_literal Literal
        reduce1 !@8 nt_funccall FunctionCall
        reduce1 !@8 nt_variable_expression VariableExpression
        reduce1 !@8 t_macro Macro
        reduce3 !@8 t_symbol_oparen nt_expression t_symbol_cparen (fun _ e _ -> e)
        reduce4 !@8 nt_variable_expression t_symbol_obrack nt_expression t_symbol_cbrack (fun v _ i _ -> ArrayIndex(v, i))
     // reduce5 !@8 nt_expression t_symbol_questionmark nt_expression t_symbol_colon nt_expression (fun c _ a _ b -> TernaryExpression(c, a, b))

        reduce4 nt_funccall t_identifier t_symbol_oparen nt_funcparams t_symbol_cparen (fun f _ p _ -> (f, p))
        reduce3 nt_funccall t_identifier t_symbol_oparen t_symbol_cparen (fun f _ _ -> (f, []))

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
        
        reduce1 nt_operator_binary_ass t_operator_assign_add (fun _ -> AssignSubtract)
        reduce1 nt_operator_binary_ass t_operator_assign_sub (fun _ -> AssignAdd)
        reduce1 nt_operator_binary_ass t_operator_assign_mul (fun _ -> AssignMultiply)
        reduce1 nt_operator_binary_ass t_operator_assign_div (fun _ -> AssignDivide)
        reduce1 nt_operator_binary_ass t_operator_assign_mod (fun _ -> AssignModulus)
        reduce1 nt_operator_binary_ass t_operator_assign_con (fun _ -> AssignConcat)
        reduce1 nt_operator_binary_ass t_operator_assign_pow (fun _ -> AssignPower)
        reduce1 nt_operator_binary_ass t_operator_assign_nand (fun _ -> AssignNand)
        reduce1 nt_operator_binary_ass t_operator_assign_and (fun _ -> AssignAnd)
        reduce1 nt_operator_binary_ass t_operator_assign_nxor (fun _ -> AssignNxor)
        reduce1 nt_operator_binary_ass t_operator_assign_xor (fun _ -> AssignXor)
        reduce1 nt_operator_binary_ass t_operator_assign_nor (fun _ -> AssignNor)
        reduce1 nt_operator_binary_ass t_operator_assign_or (fun _ -> AssignOr)
        reduce1 nt_operator_binary_ass t_operator_assign_rol (fun _ -> AssignRotateLeft)
        reduce1 nt_operator_binary_ass t_operator_assign_ror (fun _ -> AssignRotateRight)
        reduce1 nt_operator_binary_ass t_operator_assign_shl (fun _ -> AssignShiftLeft)
        reduce1 nt_operator_binary_ass t_operator_assign_shr (fun _ -> AssignShiftRight)
        reduce1 nt_operator_binary_ass t_symbol_equal (fun _ -> Assign)
        
        reduce3 nt_assignment_expression nt_variable_expression nt_operator_binary_ass nt_expression (fun v o e -> Assignment(o, v, e))
        reduce6 nt_assignment_expression nt_variable_expression t_symbol_obrack nt_expression t_symbol_cbrack nt_operator_binary_ass nt_expression (fun v _ i _ o e -> ArrayAssignment(o, v, i, e))

        x.Configuration.LexerSettings.Ignore <- [| @"[\r\n\s]+" |]
