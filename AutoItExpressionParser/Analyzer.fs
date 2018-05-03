module AutoItExpressionParser.Analyzer

open AutoItExpressionParser.ExpressionAST

open System.Globalization
open System


type variant = AutoItCoreLibrary.AutoItVariantType

    
let rec IsStatic =
    function
    | FunctionCall _
    | AssignmentExpression _ -> false
    | ArrayIndex (_, e)
    | UnaryExpression (_, e) -> IsStatic e
    | ToExpression (x, y)
    | BinaryExpression (_, x, y) -> IsStatic x && IsStatic y
    | TernaryExpression (x, y, z) -> [x; y; z]
                                     |> List.map IsStatic
                                     |> List.fold (&&) true
    | _ -> true

let rec ProcessConstants e =
    let rec procconst e =
        match e with
        | Literal l ->
            match l with
            | Number d -> variant.FromDecimal d
            | False
            | Null
            | Default -> variant.FromDecimal 0m
            | True -> variant.FromDecimal 1m
            | String s -> variant s
            |> Some
        | UnaryExpression (o, Constant x) ->
            match o with
            | Identity -> x
            | Negate -> -x
            | Not -> variant.Not x
            | BitwiseNot -> variant.BitwiseNot x
            | Length -> variant.FromDecimal (decimal x.Length)
            |> Some
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
            | Index -> !* (fun x y -> x.[y.ToLong()])
        | TernaryExpression (Constant x, Constant y, Constant z) -> Some (if x.ToBool() then z else y)
        | _ -> None
    and (|Constant|_|) = procconst
    let num = Number >> Literal
    match e with
    | Constant x -> num (x.ToDecimal())
    | _ ->
        let d = variant.FromDecimal
        match e with
        | BinaryExpression (o, x, y) ->
            match o, x, y with
            | And, Constant c, _ when c = d 0m -> num 0m
            | And, _, Constant c when c = d 0m -> num 0m
            | BitwiseAnd, Constant c, _ when c = d 0m -> num 0m
            | BitwiseAnd, _, Constant c when c = d 0m -> num 0m
            | Nand, Constant c, _ when c = d 0m -> num 1m
            | Nand, _, Constant c when c = d 0m -> num 1m
            | Nor, Constant c, _ when c = d 1m -> num 0m
            | Nor, _, Constant c when c = d 1m -> num 0m
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
                let stat = IsStatic e
                let proc() =
                    let px = ProcessConstants x
                    let py = ProcessConstants y
                    if (px <> x) || (py <> y) then
                        ProcessConstants <| BinaryExpression(o, px, py)
                    else
                        e
                if stat then
                    if x = y then
                        match o with
                        | EqualCaseSensitive
                        | EqualCaseInsensitive
                        | GreaterEqual
                        | LowerEqual
                        | Divide -> num 1m
                        | BitwiseXor
                        | Xor
                        | Subtract
                        | Unequal
                        | Lower
                        | Greater
                        | Modulus -> num 0m
                        | BitwiseOr
                        | BitwiseAnd -> x
                        | _ -> proc()
                    else
                        match o with
                        | Unequal -> num 1m
                        | EqualCaseSensitive
                        | EqualCaseInsensitive -> num 0m
                        | _ -> proc()
                else
                    proc()
        | TernaryExpression (x, y, z) ->
            if y = z then
                ProcessConstants y
            else
                let px = ProcessConstants x
                let py = ProcessConstants y
                let pz = ProcessConstants z
                if (px <> x) || (py <> y) || (pz <> z) then
                    ProcessConstants <| TernaryExpression(px, py, pz)
                else
                    e
        | FunctionCall (f, ps) -> FunctionCall(f, List.map ProcessConstants ps)
        | ArrayIndex (v, e) -> ArrayIndex(v, ProcessConstants e)
        | AssignmentExpression (Assignment (o, v, e)) -> AssignmentExpression(Assignment(o, v, ProcessConstants e))
        | AssignmentExpression (ArrayAssignment (o, v, i, e)) -> AssignmentExpression(ArrayAssignment(o, v, ProcessConstants i, ProcessConstants e))
        | _ -> e

let rec ProcessExpression e =
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
    | AssignmentExpression (Assignment (Assign, Variable v, VariableExpression (Variable w))) when v = w -> VariableExpression (Variable v)
    | AssignmentExpression (Assignment (o, Variable v, e)) when o <> Assign ->
        let v = Variable v
        AssignmentExpression (
            Assignment (
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

let EvaluatesTo(from, ``to``) = ProcessExpression from = ProcessExpression ``to``

let EvaluatesToFalse e = EvaluatesTo (e, Literal False)

let EvaluatesToTrue e = EvaluatesTo (e, Literal True)

    
let private getvarfunccalls =
    function
    | Variable _ -> []
    | DotAccess (_, m) -> List.choose (function
                                        | Method f -> Some f
                                        | _ -> None) m
let rec GetFunctionCallExpressions (e : EXPRESSION) : FUNCCALL list =
    match e with
    | Literal _
    | Macro _ -> []
    | FunctionCall f -> [[f]]
    | VariableExpression v -> [getvarfunccalls v]
    | AssignmentExpression (Assignment (_, v, i))
    | ArrayIndex (v, i) -> [getvarfunccalls v; GetFunctionCallExpressions i]
    | UnaryExpression (_, e) -> [GetFunctionCallExpressions e]
    | ToExpression (x, y)
    | BinaryExpression (_, x, y) -> [GetFunctionCallExpressions x; GetFunctionCallExpressions y]
    | TernaryExpression (x, y, z) -> [GetFunctionCallExpressions x; GetFunctionCallExpressions y; GetFunctionCallExpressions z]
    | AssignmentExpression (ArrayAssignment(_, v, i, e)) -> [getvarfunccalls v; GetFunctionCallExpressions i; GetFunctionCallExpressions e]
    | ArrayInitExpression xs -> List.map GetFunctionCallExpressions xs
    |> List.concat

let rec GetVariables (e : EXPRESSION) : VARIABLE list =
    let procvar = function
                  | DotAccess (v, _)
                  | Variable v -> v
    match e with
    | Literal _
    | Macro _ -> []
    | FunctionCall (_, es) -> []
    | VariableExpression v -> [[procvar v]]
    | ArrayIndex (v, i)
    | AssignmentExpression (Assignment (_, v, i)) -> [[procvar v]; GetVariables i]
    | UnaryExpression (_, e) -> [GetVariables e]
    | ToExpression (x, y)
    | BinaryExpression (_, x, y) -> [GetVariables x; GetVariables y]
    | TernaryExpression (x, y, z) -> [GetVariables x; GetVariables y; GetVariables z]
    | AssignmentExpression (ArrayAssignment(_, v, i, e)) -> [[procvar v]; GetVariables i; GetVariables e]
    | ArrayInitExpression xs -> List.map GetVariables xs
    |> List.concat
