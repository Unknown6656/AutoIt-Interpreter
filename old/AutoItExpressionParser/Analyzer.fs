module AutoItExpressionParser.Analyzer

open AutoItExpressionParser.ExpressionAST

open System


type variant = AutoItCoreLibrary.AutoItVariantType


let rec IsCompiletimeStatic =
    function
    | Macro _
    | DotAccess _
    | ArrayAccess _
    | FunctionCall _
    | ΛFunctionCall _
    | ToExpression _
    | VariableExpression _
    | ArrayInitExpression _
    | AssignmentExpression _
    | UnaryExpression (Dereference, _) -> false
    | Literal _ -> true
    | UnaryExpression (_, e) -> IsCompiletimeStatic e
    | ToExpression (x, y)
    | BinaryExpression (_, x, y) -> IsCompiletimeStatic x && IsCompiletimeStatic y
    | TernaryExpression (x, y, z) -> [x; y; z]
                                     |> List.map IsCompiletimeStatic
                                     |> List.fold (&&) true

let rec IsStatic =
    function
    | DotAccess _
    | FunctionCall _
    | ΛFunctionCall _
    | ToExpression _
    | ArrayInitExpression _
    | AssignmentExpression _ 
    | UnaryExpression (Dereference, _) -> false
    | UnaryExpression (_, e) -> IsStatic e
    | ToExpression (x, y)
    | BinaryExpression (_, x, y) -> IsStatic x && IsStatic y
    | TernaryExpression (x, y, z) -> [x; y; z]
                                     |> List.map IsStatic
                                     |> List.fold (&&) true
    | ArrayAccess (e, i) -> (IsStatic i) && (IsStatic e)
    | _ -> true

let rec ProcessConstants e =
    let rec procconst e =
        let num = variant.FromDecimal >> Some
        match e with
        | Literal l ->
            match l with
            | Number d -> num d
            | False -> Some variant.False
            | Null -> Some variant.Null
            | True -> Some variant.True
            | _ -> None
        | UnaryExpression (o, Constant x) ->
            match o with
            | Identity -> Some x
            | Negate -> Some <| -x
            | Not -> Some <| variant.Not x
            | BitwiseNot -> Some <| variant.BitwiseNot x
            | StringLength -> num (decimal x.Length)
            | String1Index (_, Constant l) when variant.LowerEquals(l, variant.FromDecimal 0m) -> Some <| variant ""
            | String1Index (Constant s, Constant l) ->
                if variant.LowerEquals(l, variant.FromDecimal 0m) then variant ""
                else x.OneBasedSubstring(s, l)
                |> Some
            | _ -> None
        | BinaryExpression (o, Constant x, Constant y) ->
            let (!%) = variant.FromBool >> Some
            let (!/) f = f (x.ToDecimal()) (y.ToDecimal())
                         |> (!%)
            let (!@) f = (f >> Some) (x, y)
            let (!*) f = Some (f x y)
            match o with
            | EqualCaseSensitive -> !% variant.Equals(x, y, false)
            | EqualCaseInsensitive -> !% variant.Equals(x, y, true)
            | Unequal -> !% (not <| variant.Equals(x, y, true))
            | Greater -> !/ (>)
            | GreaterEqual -> !/ (>=)
            | Lower -> !/ (<)
            | LowerEqual -> !/ (<=)
            | Xor -> !/ (<>)
            | Nxor -> !/ (=)
            | And -> !@ variant.And
            | Nor -> !@ variant.Nor
            | Nand -> !@ variant.Nand
            | Or -> !@ variant.Or
            | Add -> !* (+)
            | Subtract -> !* (-)
            | Multiply -> !* (*)
            | Divide -> !* (/)
            | IntegerDivide -> !@ variant.IntegerDivide
            | Modulus -> !* (%)
            | Power -> !@ variant.Power
            | BitwiseNand -> !@ variant.BitwiseNand
            | BitwiseAnd -> !@ variant.BitwiseAnd
            | BitwiseNxor -> !@ variant.BitwiseNxor
            | BitwiseXor -> !@ variant.BitwiseXor
            | BitwiseNor -> !@ variant.BitwiseNor
            | BitwiseOr -> !@ variant.BitwiseOr
            | BitwiseRotateLeft -> !@ variant.BitwiseRol
            | BitwiseRotateRight -> !@ variant.BitwiseRor
            | BitwiseShiftLeft -> !@ variant.BitwiseShl
            | BitwiseShiftRight -> !@ variant.BitwiseShr
            | StringConcat -> !@ variant.Concat
        | TernaryExpression (Constant x, Constant y, Constant z) -> Some (if x.ToBool() then z else y)
        | ArrayAccess(Constant x, Constant y) -> try Some x.[y] with
                                                 | _ -> None
        | _ -> None
    and (|Constant|_|) = procconst
    let num = Number >> Literal
    match e with
    | Constant x -> num (x.ToDecimal())
    | _ ->
        let d = variant.FromDecimal
        match e with
        | UnaryExpression (Identity, x) -> ProcessConstants x
        | BinaryExpression (o, x, y) ->
            match o, x, y with
            | And, Constant c, _ when c = d 0m -> num 0m
            | And, _, Constant c when c = d 0m -> num 0m
            | And, Constant c, e when c = d 1m -> ProcessConstants e
            | And, e, Constant c when c = d 1m -> ProcessConstants e
            | And, e1, (UnaryExpression(Not, e2)) when e1 = e2 -> ProcessConstants e1
            | And, (UnaryExpression(Not, e2)), e1 when e1 = e2 -> ProcessConstants e1
            | Nand, Constant c, _ when c = d 0m -> num 1m
            | Nand, _, Constant c when c = d 0m -> num 1m
            | Or, Constant c, e when c = d 0m -> ProcessConstants e
            | Or, e, Constant c when c = d 0m -> ProcessConstants e
            | Or, Constant c, _ when c = d 1m -> num 1m
            | Or, _, Constant c when c = d 1m -> num 1m
            | Or, e1, (UnaryExpression(Not, e2)) when e1 = e2 -> ProcessConstants e1
            | Or, (UnaryExpression(Not, e2)), e1 when e1 = e2 -> ProcessConstants e1
            | Nor, Constant c, _ when c = d 1m -> num 0m
            | Nor, _, Constant c when c = d 1m -> num 0m
            | BitwiseAnd, Constant c, _ when c = d 0m -> num 0m
            | BitwiseAnd, _, Constant c when c = d 0m -> num 0m
            | BitwiseOr, Constant c, e when c = d 0m -> ProcessConstants e
            | BitwiseOr, e, Constant c when c = d 0m -> ProcessConstants e
            | BitwiseXor, Constant c, e when c = d 0m -> ProcessConstants e
            | BitwiseXor, e, Constant c when c = d 0m -> ProcessConstants e
            | BitwiseRotateLeft, Constant c, _ when c = d 0m -> num 0m
            | BitwiseRotateLeft, e, Constant c when c = d 0m -> ProcessConstants e
            | BitwiseRotateRight, Constant c, _ when c = d 0m -> num 0m
            | BitwiseRotateRight, e, Constant c when c = d 0m -> ProcessConstants e
            | BitwiseShiftLeft, Constant c, _ when c = d 0m -> num 0m
            | BitwiseShiftLeft, e, Constant c when c = d 0m -> ProcessConstants e
            | BitwiseShiftRight, Constant c, _ when c = d 0m -> num 0m
            | BitwiseShiftRight, e, Constant c when c = d 0m -> ProcessConstants e
            | Add, Constant c, e when c = d 0m -> ProcessConstants e
            | Add, e, Constant c when c = d 0m -> ProcessConstants e
            | Subtract, e, Constant c when c = d 0m -> ProcessConstants e
            | Subtract, Constant c, e when c = d 0m -> UnaryExpression(Negate, ProcessConstants e)
            | Multiply, Constant c, _ when c = d 0m -> num 0m
            | Multiply, _, Constant c when c = d 0m -> num 0m
            | Multiply, Constant c, e when c = d 1m -> ProcessConstants e
            | Multiply, e, Constant c when c = d 1m -> ProcessConstants e
            | Multiply, Constant c, e when c = d 2m -> let pc = ProcessConstants e
                                                       BinaryExpression(Add, pc, pc)
            | Multiply, e, Constant c when c = d 2m -> let pc = ProcessConstants e
                                                       BinaryExpression(Add, pc, pc)
            | Divide, e, Constant c when c = d 1m -> ProcessConstants e
            | Power, Constant c, _ when c = d 0m -> num 0m
            | Power, _, Constant c when c = d 0m -> num 1m
            | Power, e, Constant c when c = d 1m -> ProcessConstants e
            | _ ->
                let proc() =
                    let px = ProcessConstants x
                    let py = ProcessConstants y
                    if (px <> x) || (py <> y) then
                        ProcessConstants <| BinaryExpression(o, px, py)
                    else
                        e
                if IsStatic e then
                    if x = y then
                        match o with
                        | Nxor
                        | BitwiseNxor
                        | EqualCaseSensitive
                        | EqualCaseInsensitive
                        | GreaterEqual
                        | LowerEqual
                        | Divide -> num 1m
                        | Xor
                        | BitwiseXor
                        | Subtract
                        | Unequal
                        | Lower
                        | Greater
                        | Modulus -> num 0m
                        | Or
                        | And
                        | BitwiseOr
                        | BitwiseAnd -> x
                        | Nor
                        | Nand -> UnaryExpression(Not, x)
                        | BitwiseNor
                        | BitwiseNand -> UnaryExpression(BitwiseNot, x)
                        | _ -> proc()
                    elif IsCompiletimeStatic e then
                        match o with
                        | Unequal -> num 1m
                        | EqualCaseSensitive
                        | EqualCaseInsensitive -> num 0m
                        | _ -> proc()
                    else
                        proc()
                else
                    proc()
        | TernaryExpression (x, y, z) ->
            if (y = z) || (EvaluatesToTrue x) then
                ProcessConstants y
            elif EvaluatesToFalse x then
                ProcessConstants z
            else
                let px = ProcessConstants x
                let py = ProcessConstants y
                let pz = ProcessConstants z
                if (px <> x) || (py <> y) || (pz <> z) then
                    ProcessConstants <| TernaryExpression(px, py, pz)
                else
                    e
        | FunctionCall (f, [p]) when match p with
                                     | Literal _ -> f.Equals("execute", StringComparison.InvariantCultureIgnoreCase)
                                     | _ -> false
                                     -> FunctionCall("Identity", [p])
        | FunctionCall (f, ps) -> FunctionCall(f, List.map ProcessConstants ps)
        | ΛFunctionCall (e, ps) -> ΛFunctionCall(ProcessConstants e, List.map ProcessConstants ps)
        | ArrayAccess(e, i) -> ArrayAccess(ProcessConstants e, ProcessConstants i)
        | AssignmentExpression ae -> match ae with
                                     | ScalarAssignment (o, v, e) -> ScalarAssignment(o, v, ProcessConstants e)
                                     | ArrayAssignment (o, v, i, e) -> ArrayAssignment(o, v, List.map ProcessConstants i, ProcessConstants e)
                                     | ReferenceAssignment (o, v, e) -> ReferenceAssignment(o, ProcessConstants v, ProcessConstants e)
                                     |> AssignmentExpression
        | _ -> e

and ProcessExpression e =
    let assign_dic =
        [
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
        |> dict
    let p = ProcessConstants e
    match p with
    | AssignmentExpression (ScalarAssignment (Assign, v, VariableExpression w)) when v = w -> VariableExpression v
    | AssignmentExpression (ScalarAssignment (o, v, e)) when o <> Assign ->
        AssignmentExpression (
            ScalarAssignment(
                Assign,
                v,
                BinaryExpression (
                    assign_dic.[o],
                    VariableExpression v,
                    ProcessConstants e
                )
            )
        )
    // TODO
    | _ -> p

and EvaluatesTo(from, ``to``) = ProcessExpression from = ProcessExpression ``to``

and EvaluatesToFalse e = EvaluatesTo (e, Literal False)

and EvaluatesToTrue e = EvaluatesTo (e, Literal True)

let GetConstantValue e =
    match ProcessConstants e with
    | Literal l ->
        match l with
        | LITERAL.String s -> Some (variant s)
        | Number d -> Some (variant.FromDecimal d)
        | Null -> Some variant.Null
        | Default -> Some variant.Default
        | True -> Some variant.True
        | False -> Some variant.False
    | _ -> None

let rec GetFunctionCallExpressions (e : EXPRESSION) : FUNCCALL list =
    match e with
    | Macro _
    | Literal _
    | VariableExpression _ -> []
    | FunctionCall f -> [[f]] @ List.map GetFunctionCallExpressions (snd f)
    | ΛFunctionCall (_, e) -> [[null, e]] @ List.map GetFunctionCallExpressions e
    | ArrayAccess (_, e)
    | UnaryExpression (_, e) 
    | AssignmentExpression (ScalarAssignment (_, _, e)) -> [GetFunctionCallExpressions e]
    | AssignmentExpression (ArrayAssignment (_, _, x, y)) -> GetFunctionCallExpressions y::List.map GetFunctionCallExpressions x
    | AssignmentExpression (ReferenceAssignment (_, x, y))
    | ToExpression (x, y)
    | BinaryExpression (_, x, y) -> [GetFunctionCallExpressions x; GetFunctionCallExpressions y]
    | TernaryExpression (x, y, z) -> [GetFunctionCallExpressions x; GetFunctionCallExpressions y; GetFunctionCallExpressions z]
    | ArrayInitExpression (i, es) ->
        let rec gfce = function
                       | Single e -> GetFunctionCallExpressions e
                       | Multiple es -> List.map gfce es
                                     |> List.concat
        List.map gfce es
        @ List.map GetFunctionCallExpressions i
    | DotAccess (_, m) -> List.choose (function
                                       | Method f -> Some [f]
                                       | _ -> None) m
    |> List.concat

let rec GetVariables (e : EXPRESSION) : VARIABLE list =
    match e with
    | Literal _
    | Macro _ -> []
    | FunctionCall (_, es) -> List.map GetVariables es
    | ΛFunctionCall (v, es) -> GetVariables v::List.map GetVariables es
    | VariableExpression v -> [[v]]
    | AssignmentExpression (ScalarAssignment (_, v, e)) -> [[v]; GetVariables e]
    | AssignmentExpression (ArrayAssignment (_, v, i, e)) -> (v::GetVariables e)::List.map GetVariables i
    | DotAccess (e, _)
    | UnaryExpression (_, e) -> [GetVariables e]
    | AssignmentExpression (ReferenceAssignment (_, x, y))
    | ArrayAccess (x, y)
    | ToExpression (x, y)
    | BinaryExpression (_, x, y) -> [GetVariables x; GetVariables y]
    | TernaryExpression (x, y, z) -> [GetVariables x; GetVariables y; GetVariables z]
    | ArrayInitExpression (i, es) -> let rec gv = function
                                                  | Single e -> GetVariables e
                                                  | Multiple es -> List.map gv es
                                                                |> List.concat
                                     List.map GetVariables i @ List.map gv es
    |> List.concat

let rec GetArrayDimensions e =
    match List.map (function Single _ -> Some 0 | Multiple e -> GetArrayDimensions e) e
        |> List.distinct with
    | [Some x] -> Some (x + 1)
    | [] -> Some 0
    | _ -> None

let GetMatrixDimensions e =
    let rec getdim e =
        match List.choose (function Single _ -> Some [] | Multiple e -> getdim e) e
           |> List.distinct with
        | [x] -> Some (e.Length::x)
        | [] -> Some [e.Length]
        | _ -> None
    match getdim e with
    | Some l -> List.toArray l
    | None -> [||]
