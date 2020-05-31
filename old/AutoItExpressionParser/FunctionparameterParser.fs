namespace AutoItExpressionParser

open AutoItExpressionParser.ExpressionAST

open System.Globalization


type FunctionparameterParser(optimize : bool) =
    inherit AbstractParser<FUNCTION_PARAMETER[]>()
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
            
        let nt_decl                 = x.nt<FUNCTION_PARAMETER[]>        "function-declaration"
        let nt_decll                = x.nt<FUNCTION_PARAMETER list>     "parameters"
        let nt_decli                = x.nt<FUNCTION_PARAMETER>          "parameter"
        let nt_decl_def             = x.nt<FUNCTION_PARAMETER_DEFVAL>   "default-parameter"
        let nt_literal              = x.nt<LITERAL>                     "literal"
        let t_symbol_equal          = x.t @"="
        let t_symbol_comma          = x.t @","
        let t_keyword_const         = x.t @"const"
        let t_keyword_byref         = x.t @"byref"
        let t_literal_true          = x.tf @"true"                                                 (fun _ -> True)
        let t_literal_false         = x.tf @"false"                                                (fun _ -> False)
        let t_literal_null          = x.tf @"null"                                                 (fun _ -> Null)
        let t_literal_default       = x.tf @"default"                                              (fun _ -> Default)
        let t_hex                   = x.tf @"(\+|-)?(0x[\da-f]+|[\da-f]h)"                         (lparse "0x" (fun s -> long.Parse(s.TrimEnd 'h', NumberStyles.HexNumber)))
        let t_bin                   = x.tf @"(\+|-)?0b[01]+"                                       (lparse "0b" (fun s -> System.Convert.ToInt64(s, 2)))
        let t_oct                   = x.tf @"(\+|-)?0o[0-7]+"                                      (lparse "0o" (fun s -> System.Convert.ToInt64(s, 8)))
        let t_dec                   = x.tf @"(\+|-)?(\d+\.\d*(e(\+|-)?\d+)?|\.?\d+(e(\+|-)?\d+)?)" (fun s -> match decimal.TryParse s with
                                                                                                                 | (true, d) -> d
                                                                                                                 | _ -> decimal.Parse(s, NumberStyles.Float)
                                                                                                                 |> Number) 
        let t_variable              = x.tf @"$[a-zA-Z_][a-zA-Z0-9_]*"                              (fun s -> VARIABLE(s.Substring 1))
        let t_macro                 = x.tf @"@[a-zA-Z_][a-zA-Z0-9_]*"                              (fun s -> MACRO(s.Substring 1))
        let t_string_1              = x.tf "\"(([^\"]*\"\"[^\"]*)*|[^\"]+)\""                      (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Trim().Replace("\"\"", "\"")))
        let t_string_2              = x.tf "'(([^']*''[^']*)*|[^']+)'"                             (fun s -> String(s.Remove(s.Length - 1).Remove(0, 1).Trim().Replace("''", "'")))

        let mpar v r c = { Variable = v; Type = Mandatory { IsByRef = r; IsConst = c } }

        reduce1 nt_decl nt_decll List.toArray
        reducef nt_decl (fun () -> [||])
        
        reduce3 nt_decll nt_decli t_symbol_comma nt_decll (fun e _ es -> e::es)
        reduce1 nt_decll nt_decli (fun e -> [e])
        
        reduce3 nt_decli t_keyword_const t_keyword_byref t_variable (fun _ _ v -> mpar v true true)
        reduce3 nt_decli t_keyword_byref t_keyword_const t_variable (fun _ _ v -> mpar v true true)
        reduce2 nt_decli t_keyword_const t_variable (fun _ v -> mpar v false true)
        reduce2 nt_decli t_keyword_byref t_variable (fun _ v -> mpar v true false)
        reduce1 nt_decli t_variable (fun v -> mpar v false false)
        reduce3 nt_decli t_variable t_symbol_equal nt_decl_def (fun v _ d ->  { Variable = v; Type = Optional d })
        
        reduce1 nt_decl_def t_macro Mac
        reduce1 nt_decl_def nt_literal Lit

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


        x.Configuration.LexerSettings.Ignore <- [| @"[\r\n\s]+" |]
        x.Configuration.LexerSettings.IgnoreCase <- true