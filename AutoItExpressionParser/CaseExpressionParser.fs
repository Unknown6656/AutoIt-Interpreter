namespace AutoItExpressionParser

open AutoItExpressionParser.ExpressionAST


type CaseExpressionParser() =
    inherit AbstractParser<CASE_EXPRESSION[]>()
    override x.BuildParser() =
        let expr_p = ExpressionParser()
        
        let nt_cexpr                = x.nt<CASE_EXPRESSION[]>()
        let nt_case_expressions     = x.nt<CASE_EXPRESSION list>()
        let nt_case_expression      = x.nt<CASE_EXPRESSION>()
        
        let t_symbol_comma          = x.t @","
        let t_keyword_to            = x.t @"to"
        let t_keyword_case          = x.t @"case"
        let t_expression            = x.tf @".+"    expr_p.Parse
        
        reduce2 nt_cexpr t_keyword_case nt_case_expressions (fun _ s -> List.toArray s)
        reduce3 nt_case_expressions nt_case_expression t_symbol_comma nt_case_expressions (fun e _ s -> e::s)
        reduce1 nt_case_expressions nt_case_expression (fun e -> [e])
        reduce3 nt_case_expression t_expression t_keyword_to t_expression (fun f _ t -> ValueRange(f, t))
        reduce1 nt_case_expression t_expression SingleValue

        x.Configuration.LexerSettings.Ignore <- [| @"\s+"; |]
