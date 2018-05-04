namespace AutoItExpressionParser

open AutoItExpressionParser.ExpressionAST
open System


type SerializerSettings =
    {
        MacroDictionary : string
        VariableDictionary : string
        VariableTypeName : string
        FunctionPrefix : string
        FunctionResolver : Func<string, string>
    }

type Serializer (settings : SerializerSettings) =
    let adict = dict [
                         AssignAdd, Add
                         AssignSubtract, Subtract
                         AssignMultiply, Multiply
                         AssignDivide, Divide
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
        let (!/) = sprintf "%s.%s" x.Settings.VariableTypeName
        let printvar = function
                       | Variable v -> sprintf "%s[\"%s\"]" (x.Settings.VariableDictionary) v.Name
                       | DotAccess (v, m) -> "  « object access not yet implemented »  " // TODO
        let printbin a o b =
            let (!!) = sprintf "(%%s %s %%s)"
            let (!<) = sprintf "%s.%s(%%s, %%s)" (x.Settings.VariableTypeName)
            let f = match o with
                    | StringConcat -> !!"&"
                    | EqualCaseSensitive -> !!"=="
                    | EqualCaseInsensitive
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
        let rec printass v o e =
            let e = if o = Assign
                    then printexpr e
                    else printbin v (adict.[o]) (printexpr e)
            sprintf "%s = (%s)%s" v (x.Settings.VariableTypeName) e
        and printexpr e =
            let str = function
                      | Literal l -> match l with
                                     | Null -> !/"Null"
                                     | Default -> !/"Default"
                                     | True -> !/"True"
                                     | False -> !/"False"
                                     | Number d -> d.ToString()
                                     | String s -> sprintf "\"%s\"" (s.Replace("\\", "\\\\")
                                                                      .Replace("\"", "\\\"")
                                                                      .Replace("\r", "\\r")
                                                                      .Replace("\n", "\\n")
                                                                      .Replace("\b", "\\b")
                                                                      .Replace("\a", "\\a")
                                                                      .Replace("\f", "\\f")
                                                                      .Replace("\v", "\\v")
                                                                      .Replace("\t", "\\t")
                                                                      .Replace("\x7f", "\\x7f")
                                                                      .Replace("\0", "\\0"))
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
                      | BinaryExpression (o, x, y) -> printbin (printexpr x) o (printexpr y)
                      | TernaryExpression (x, y, z) -> sprintf "(%s ? %s : %s)" (printexpr x) (printexpr y) (printexpr z)
                      | FunctionCall (f, es) ->
                            match f.ToLower() with
                            | "eval" -> sprintf "%s[%s]" (x.Settings.VariableDictionary) (printexpr es.[0])
                            | "assign" -> sprintf "%s[%s] = %s" (x.Settings.VariableDictionary) (printexpr es.[0]) (printexpr es.[1])
                            | "isdeclared" -> sprintf "%s.ContainsVariable(%s)" (x.Settings.VariableDictionary) (printexpr es.[0])
                            | f -> let fs = match x.Settings.FunctionResolver.Invoke f with
                                            | null -> x.Settings.FunctionPrefix + f
                                            | f -> f
                                   sprintf "%s(%s)" fs (String.Join (", ", (List.map printexpr es)))
                      | AssignmentExpression (Assignment (o, v, e)) -> printass (printvar v) o e
                      | ArrayIndex (v, e) -> sprintf "%s[%s]" (printvar v) (printexpr e)
                      | AssignmentExpression (ArrayAssignment (o, v, i, e)) -> "  « array access not yet implemented »  " // TODO
                      | ArrayInitExpression _
                      | ToExpression _ -> failwith "Invalid expression"
            match e with
            | Literal _ -> sprintf "(%s)(%s)" (x.Settings.VariableTypeName) (str e)
            | _ -> str e
        printexpr e
