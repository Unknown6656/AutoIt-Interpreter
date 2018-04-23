module AutoItExpressionParser.Analyzer

open AutoItExpressionParser.ExpressionAST
open System.Globalization
open System


let (|Long|_|) str =
    match Int64.TryParse str with
    | true, i -> Some i
    | _ ->
        let str = str.ToLower()
        if str.StartsWith "0x" then
            match Int64.TryParse (str, NumberStyles.HexNumber, null) with
            | true, i -> Some i
            | _ -> None
        else
            None
let (|Decimal|_|) str =
    match Decimal.TryParse str with
    | true, i -> Some i
    | _ -> None
    
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
        let bit (f : int64 -> int64) = int64 >> f >> decimal >> Some
        match e with
        | Literal l ->
            match l with
            | Number d -> Some d
            | False
            | Null
            | Default -> Some 0m
            | True -> Some 1m
            //| String s ->
            //    match s with
            //    | Long d -> Some(decimal d)
            //    | Decimal d -> Some d
            //    | _ -> None
            | _ -> None
        | UnaryExpression (o, Constant x) ->
            match o with
            | Identity -> Some x
            | Negate -> Some -x
            | Not -> Some (if x = 0m then 1m else 0m)
            | BitwiseNot -> bit (~~~) x
        | BinaryExpression (o, Constant x, Constant y) ->
            let (!%) r = Some (if r x y then 1m else 0m)
            let (!@) r = Some (if r (x <> 0m) (y <> 0m) then 1m else 0m)
            let (!*) r = Some (r x y)
            let (!&) (r : int64 -> int64 -> int64) = Some (decimal (r (int64 x) (int64 y)))
            let shl x y = x <<< int(y % 64L)
            let shr x y = x >>> int(y % 64L)
            let rol x y = shl x y ||| x >>> (64 - int(y % 64L))
            let ror x y = shr x y ||| x <<< (64 - int(y % 64L))
            match o with
            | EqualCaseSensitive
            | EqualCaseInsensitive -> !% (=)
            | Unequal -> !% (<>)
            | Greater -> !% (>)
            | GreaterEqual -> !% (>=)
            | Lower -> !% (<)
            | LowerEqual -> !% (<=)
            | And -> !@ (&&)
            | Xor -> !@ (<>)
            | Nxor -> !@ (=)
            | Nor -> !@ (fun x y -> not x && not y)
            | Nand -> !@ (fun x y -> not(x && y))
            | Or -> !@ (||)
            | Add -> !* (+)
            | Subtract -> !* (-)
            | Multiply -> !* (*)
            | Divide -> !* (/)
            | Modulus -> !* (%)
            | Power -> !* (fun x y -> decimal((float x) ** (float y)))
            | BitwiseNand -> !& (fun x y -> ~~~(x &&& y))
            | BitwiseAnd -> !& (&&&)
            | BitwiseNxor -> !& (fun x y -> ~~~(x ^^^ y))
            | BitwiseXor -> !& (^^^)
            | BitwiseNor -> !& (fun x y -> ~~~(x ||| y))
            | BitwiseOr -> !& (|||)
            | BitwiseRotateLeft -> !&rol
            | BitwiseRotateRight -> !&ror
            | BitwiseShiftLeft -> !&shl
            | BitwiseShiftRight -> !&shr
            | _ -> None
        | TernaryExpression (Constant x, Constant y, Constant z) -> Some (if x = 0m then z else y)
        | _ -> None
    and (|Constant|_|) = procconst
    let num = Number >> Literal
    match e with
    | Constant x -> num x
    | _ ->
        match e with
        | BinaryExpression (o, x, y) ->
            match o, x, y with
            | And, Constant c, e when c = 0m -> num 0m
            | And, e, Constant c when c = 0m -> num 0m
            | BitwiseAnd, Constant c, e when c = 0m -> num 0m
            | BitwiseAnd, e, Constant c when c = 0m -> num 0m
            | Multiply, Constant c, e when c = 0m -> num 0m
            | Multiply, e, Constant c when c = 0m -> num 0m
            | Nor, Constant c, e when c = 1m -> num 0m
            | Nor, e, Constant c when c = 1m -> num 0m
            | Nand, Constant c, e when c = 0m -> num 1m
            | Nand, e, Constant c when c = 0m -> num 1m
            | Power, Constant c, e when c = 0m -> num 0m
            | Power, e, Constant c when c = 0m -> num 1m
            | Power, e, Constant c when c = 1m -> ProcessConstants e
            | Add, Constant c, e when c = 0m -> ProcessConstants e
            | Add, e, Constant c when c = 0m -> ProcessConstants e
            | Subtract, e, Constant c when c = 0m -> ProcessConstants e
            | BitwiseOr, Constant c, e when c = 0m -> ProcessConstants e
            | BitwiseOr, e, Constant c when c = 0m -> ProcessConstants e
            | BitwiseXor, Constant c, e when c = 0m -> ProcessConstants e
            | BitwiseXor, e, Constant c when c = 0m -> ProcessConstants e
            | Multiply, Constant c, e when c = 1m -> ProcessConstants e
            | Multiply, e, Constant c when c = 1m -> ProcessConstants e
            | Divide, e, Constant c when c = 1m -> ProcessConstants e
            | BitwiseRotateLeft, Constant c, e when c = 0m -> num 0m
            | BitwiseRotateLeft, e, Constant c when c = 0m -> ProcessConstants e
            | BitwiseRotateRight, Constant c, e when c = 0m -> num 0m
            | BitwiseRotateRight, e, Constant c when c = 0m -> ProcessConstants e
            | BitwiseShiftLeft, Constant c, e when c = 0m -> num 0m
            | BitwiseShiftLeft, e, Constant c when c = 0m -> ProcessConstants e
            | BitwiseShiftRight, Constant c, e when c = 0m -> num 0m
            | BitwiseShiftRight, e, Constant c when c = 0m -> ProcessConstants e


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
                        | Modulus -> num 1m
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
    match ProcessConstants e with
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
    | _ -> e

    
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
    |> List.concat
