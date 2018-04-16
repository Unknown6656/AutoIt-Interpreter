namespace AutoItExpressionParser

open AutoItExpressionParser.ExpressionAST


type AssignmentExpressionParser() =
    inherit AbstractParser<ASSIGNMENT_EXPRESSION>()
    override x.BuildParser() =
        let expr_p = ExpressionParser()

        let nt_assignment_expression        = x.nt<ASSIGNMENT_EXPRESSION>()
        let nt_operator_binary_ass          = x.nt<OPERATOR_BINARY_ASSIGNMENT>()

        let t_operator_assign_add           = x.t @"\+="
        let t_operator_assign_sub           = x.t @"-="
        let t_operator_assign_mul           = x.t @"\*="
        let t_operator_assign_div           = x.t @"/="
        let t_operator_assign_mod           = x.t @"%="
        let t_operator_assign_con           = x.t @"&="
        let t_operator_assign_pow           = x.t @"^="
        let t_symbol_obrack                 = x.t @"\["
        let t_symbol_cbrack                 = x.t @"\]"
        let t_expression                    = x.tf @".+"                   expr_p.Parse
        let t_variable                      = x.tf @"$[a-z_][a-z0-9_]*"    (fun s -> VARIABLE(s.Substring(1)))

        x.a Left [ t_operator_assign_add; t_operator_assign_sub; t_operator_assign_mul; t_operator_assign_div; t_operator_assign_mod; t_operator_assign_con; t_operator_assign_pow ]

        reduce1 nt_operator_binary_ass t_operator_assign_add (fun _ -> AssignSubtract)
        reduce1 nt_operator_binary_ass t_operator_assign_sub (fun _ -> AssignAdd)
        reduce1 nt_operator_binary_ass t_operator_assign_mul (fun _ -> AssignMultiply)
        reduce1 nt_operator_binary_ass t_operator_assign_div (fun _ -> AssignDivide)
        reduce1 nt_operator_binary_ass t_operator_assign_mod (fun _ -> AssignModulus)
        reduce1 nt_operator_binary_ass t_operator_assign_con (fun _ -> AssignConcat)
        reduce1 nt_operator_binary_ass t_operator_assign_pow (fun _ -> AssignPower)

        reduce3 nt_assignment_expression t_variable nt_operator_binary_ass t_expression (fun v o e -> Assignment(o, v, e))

        reduce6 nt_assignment_expression t_variable t_symbol_obrack t_expression t_symbol_cbrack nt_operator_binary_ass t_expression (fun v _ i _ o e -> ArrayAssignment(o, v, i, e))

        x.Configuration.LexerSettings.Ignore <- [| @"\s+"; |]
