namespace AutoItExpressionParser

open AutoItExpressionParser.ExpressionAST
open System


type ResolvedFunctionParamter =
    {
        IsOptional : bool
        IsByRef : bool
    }

type ResolvedFunction =
    {
        Name : string
        Parameters : ResolvedFunctionParamter[]
    }

type SerializerSettings =
    {
        MacroDictionary : string
        VariableDictionary : string
        VariableTypeName : string
        DiscardName : string
        FunctionResolver : Func<string, EXPRESSION[], ResolvedFunction>
    }

type FunctionParameterCountMismatchException(name, expected, got) =
    inherit Exception()
    member x.FunctionName = name
    member x.ExpectedArgumentCount = expected
    member x.RecievedArgumentCount = got

type Serializer (settings : SerializerSettings) =
    let adict = dict [
                         AssignAdd, Add
                         AssignSubtract, Subtract
                         AssignMultiply, Multiply
                         AssignDivide, Divide
                         AssignIntegerDivide, IntegerDivide
                         AssignModulus, Modulus
                         AssignConcat, StringConcat
                         AssignPower, Power
                         AssignNand, BitwiseNand
                         AssignAnd, BitwiseAnd
                         AssignNxor, BitwiseNxor
                         AssignXor, BitwiseXor
                         AssignNor, BitwiseNor
                         AssignOr, BitwiseOr
                         AssignRotateLeft, BitwiseRotateLeft
                         AssignRotateRight, BitwiseRotateRight
                         AssignShiftLeft, BitwiseShiftLeft
                         AssignShiftRight, BitwiseShiftRight
                     ]
    member x.Settings with get() = settings
    member x.Serialize e =
        let varn = x.Settings.VariableTypeName
        let (!/) = sprintf "%s.%s" varn
        let printvar (v : VARIABLE) = sprintf "%s[\"%s\"]" (x.Settings.VariableDictionary) v.Name
        let printbin a o b =
            let (!!) = sprintf "(%%s %s %%s)"
            let (!<) = sprintf "%s.%s(%%s, %%s)" varn
            let f = match o with
                    | StringConcat -> !!"&"
                    | EqualCaseSensitive -> !!"=="
                    | EqualCaseInsensitive -> !<"EqualsCaseInsensitive"
                    | Unequal -> !!"!="
                    | Greater -> !!">"
                    | GreaterEqual -> !!">="
                    | Lower -> !!"<"
                    | LowerEqual -> !!"<="
                    | And -> !<"And"
                    | Xor -> !<"Xor"
                    | Nxor -> !<"Nxor"
                    | Nor -> !<"Nor"
                    | Nand -> !<"Nand"
                    | Or -> !<"Or"
                    | Add -> !!"+"
                    | Subtract -> !!"-"
                    | Multiply -> !!"*"
                    | Divide -> !!"/"
                    | IntegerDivide -> !<"IntegerDivide"
                    | Modulus -> !!"%"
                    | Power -> !!"^"
                    | BitwiseAnd -> !<"BitwiseAnd"
                    | BitwiseXor -> !<"BitwiseXor"
                    | BitwiseNxor -> !<"BitwiseNxor"
                    | BitwiseNor -> !<"BitwiseNor"
                    | BitwiseNand -> !<"BitwiseNand"
                    | BitwiseOr -> !<"BitwiseOr"
                    | BitwiseRotateLeft -> !<"BitwiseRol"
                    | BitwiseRotateRight -> !<"BitwiseRor"
                    | BitwiseShiftLeft -> !<"BitwiseShl"
                    | BitwiseShiftRight -> !<"BitwiseShr"
                    |> Printf.StringFormat<string->string->string>
            sprintf f a b
        let rec printass v i o e =
            let e = if o = Assign
                    then printexpr e
                    else printbin v (adict.[o]) (printexpr e)
            sprintf "%s%s = (%s)%s" v (match i with
                                       | [] -> ""
                                       | i -> "[" + (String.Join(", ", List.map printexpr i)) + "]") varn e
        and printparams name (ps : EXPRESSION list) (rf : ResolvedFunctionParamter[]) =
            let rf = match rf with
                     | null -> Array.create (ps.Length) ({ IsOptional = false; IsByRef = false })
                     | _ -> if rf.Length = ps.Length then rf
                            else FunctionParameterCountMismatchException(name, rf.Length, ps.Length)
                                 |> raise
                     |> Array.toList
                     |> List.zip ps
                     |> List.map (fun (e, rp) ->
                                      if rp.IsByRef then
                                          match e with
                                          | VariableExpression v -> sprintf "%s.ByReference[\"%s\"]" (x.Settings.VariableDictionary) v.Name
                                          | _ -> sprintf "((%s)%s).MakeReference()" (x.Settings.VariableTypeName) (printexpr e)
                                      else
                                          sprintf "(%s).Clone()" (printexpr e)
                                 )
            String.Join(", ", rf)
        and printexpr e =
            let str = function
                      | Literal l -> match l with
                                     | Null -> !/"Null"
                                     | Default -> !/"Default"
                                     | True -> !/"True"
                                     | False -> !/"False"
                                     | Number d -> d.ToString()
                                     | String s -> "\"" + (s
                                                           |> Seq.map (fun c -> if c > 'ÿ' then
                                                                                    sprintf @"\u%04x" <| uint16 c
                                                                                elif (c < ' ') || ((c > '~') && (c < '¡')) then
                                                                                    sprintf @"\x%02x" <| uint8 c
                                                                                else
                                                                                    c.ToString())
                                                           |> String.Concat) + "\""
                      | Macro m -> sprintf "%s[\"%s\"]" (x.Settings.MacroDictionary) m.Name
                      | VariableExpression v -> printvar v
                      | UnaryExpression (o, e) ->
                            let (!/) s = sprintf "(%s%s)" s (printexpr e)
                            match o with
                            | Identity -> !/""
                            | Negate -> !/"-"
                            | Not -> !/"!"
                            | BitwiseNot -> !/"~"
                            | String1Index (s, l) -> sprintf "(%s).OneBasedSubstring(%s, %s)" (printexpr e) (printexpr s) (printexpr l)
                            | StringLength -> "(" + printexpr e + ").Length"
                            | Dereference -> sprintf "(%s)((%s).Dereference())" varn (printexpr e)
                      | BinaryExpression (o, x, y) -> printbin (printexpr x) o (printexpr y)
                      | TernaryExpression (x, y, z) -> sprintf "(%s ? %s : %s)" (printexpr x) (printexpr y) (printexpr z)
                      | FunctionCall (f, es) ->
                            match f.ToLower() with
                            | "eval" -> sprintf "%s[%s]" (x.Settings.VariableDictionary) (printexpr es.[0])
                            | "assign" -> sprintf "%s[%s] = %s" (x.Settings.VariableDictionary) (printexpr es.[0]) (printexpr es.[1])
                            | "isdeclared" -> sprintf "%s.ContainsVariable(%s)" (x.Settings.VariableDictionary) (printexpr es.[0])
                            | f -> let rf = x.Settings.FunctionResolver.Invoke(f, List.toArray es)
                                   sprintf "%s(%s)" (rf.Name) (printparams (rf.Name) es (rf.Parameters))
                      | ΛFunctionCall (e, es) -> sprintf "(%s).Call(%s)" (printexpr e) (printparams "<anonymous>" es null)
                      | ArrayAccess (e, i) -> sprintf "%s[%s]" (printexpr e) (printexpr i)
                      | DotAccess (e, m) -> sprintf "%s%s" (printexpr e) (m
                                                                          |> List.map (fun m -> "." + match m with
                                                                                                      | Method f -> printexpr (FunctionCall f)
                                                                                                      | Field f -> f)
                                                                          |> String.Concat)
                      | AssignmentExpression ae -> match ae with
                                                   | ScalarAssignment (o, v, e) -> printass (printvar v) [] o e
                                                   | ArrayAssignment (o, v, i, e) -> printass (printvar v) i o e
                                                   | ReferenceAssignment (o, v, e) ->
                                                        match o with
                                                        | Assign -> sprintf "(%s).Dereference(((%s)%s).ToByte())" (printexpr v) varn (printexpr e)
                                                        | _ -> let disc = x.Settings.DiscardName
                                                               let v' = UnaryExpression(Dereference, v)
                                                               sprintf "%s = %s; (%s).Dereference(((%s)%s).ToByte())" disc (printexpr v) disc varn (printbin (printexpr v') (adict.[o]) (printexpr e))
                                                               + (if Analyzer.IsStatic v then sprintf "; %s = %s" (printexpr v) disc else "")
                      | ArrayInitExpression (is, es) ->
                            match es with
                            | [] -> match is with
                                    | [] -> sprintf "%s.NewMatrix(new %s[0])" varn varn
                                    | _ -> sprintf "%s.NewMatrix(%s)" varn (String.Join(", ", List.map printexpr is))
                            | _ ->
                                let rec parr iss = function
                                                   | Single e -> printexpr e
                                                   | Multiple [] -> sprintf "%s.NewMatrix(new %s[0])" varn varn
                                                   | Multiple es ->
                                                        if iss then
                                                            sprintf "%s.NewArray(%s)" varn (String.Join(", ", List.map (parr false) es))
                                                        else
                                                            parr true es.[0]
                                parr false (Multiple es)
                      | ToExpression _ -> failwith "Invalid expression"
            match e with
            | Literal _ -> sprintf "(%s)(%s)" varn (str e)
            | _ -> str e
        printexpr e
