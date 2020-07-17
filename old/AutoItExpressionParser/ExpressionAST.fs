[<System.Runtime.CompilerServices.ExtensionAttribute>]
module AutoItExpressionParser.ExpressionAST

open AutoItExpressionParser.Util

open System.Runtime.CompilerServices
open System


let mutable private _tmp = 0L

type VARIABLE (name : string) =
    member x.Name = if name.StartsWith('$') then name.Substring 1 else name
    override x.ToString() = "$" + x.Name
    override x.GetHashCode() = x.Name.ToLower().GetHashCode()
    override x.Equals o =
        match o with
        | As (m : VARIABLE) -> m.GetHashCode() = x.GetHashCode()
        | _ -> false
    static member NewTemporary =
        _tmp <- _tmp + 1L
        VARIABLE(sprintf "__tmp<>%04x" _tmp)

type MACRO (name : string) =
    member x.Name = if name.StartsWith('@') then name.Substring 1 else name
    override x.ToString() = "@" + x.Name
    override x.GetHashCode() = x.Name.ToLower().GetHashCode()
    override x.Equals o =
        match o with
        | As (m : MACRO) -> m.GetHashCode() = x.GetHashCode()
        | _ -> false

type LITERAL =
    | Null
    | Default
    | True
    | False
    | Number of decimal
    | String of string
type OPERATOR_ASSIGNMENT =
    | Assign
    | AssignAdd
    | AssignSubtract
    | AssignMultiply
    | AssignIntegerDivide
    | AssignDivide
    | AssignModulus
    | AssignConcat
    | AssignPower
    | AssignNand
    | AssignAnd
    | AssignNxor
    | AssignXor
    | AssignNor
    | AssignOr
    | AssignRotateLeft
    | AssignRotateRight
    | AssignShiftLeft
    | AssignShiftRight
type OPERATOR_BINARY =
    | StringConcat
    | EqualCaseSensitive
    | EqualCaseInsensitive
    | Unequal
    | Greater
    | GreaterEqual
    | Lower
    | LowerEqual
    | And
    | Xor
    | Nxor
    | Nor
    | Nand
    | Or
    | Add
    | Subtract
    | Multiply
    | IntegerDivide
    | Divide
    | Modulus
    | Power
    | BitwiseNand
    | BitwiseAnd
    | BitwiseNxor
    | BitwiseXor
    | BitwiseNor
    | BitwiseOr
    | BitwiseRotateLeft
    | BitwiseRotateRight
    | BitwiseShiftLeft
    | BitwiseShiftRight
type OPERATOR_UNARY =
    | String1Index of EXPRESSION * EXPRESSION // START(1), LENGTH
    | StringLength
    | Identity
    | Negate
    | Not
    | BitwiseNot
    | Dereference
and MEMBER =
    | Field of string
    | Method of FUNCCALL
and FUNCCALL = string * EXPRESSION list
and INIT_EXPRESSION =
    | Multiple of INIT_EXPRESSION list
    | Single of EXPRESSION
and EXPRESSION =
    | Literal of LITERAL
    | FunctionCall of FUNCCALL
    | ΛFunctionCall of EXPRESSION * EXPRESSION list
    | VariableExpression of VARIABLE
    | Macro of MACRO
    | UnaryExpression of OPERATOR_UNARY * EXPRESSION
    | BinaryExpression of OPERATOR_BINARY * EXPRESSION * EXPRESSION
    | TernaryExpression of EXPRESSION * EXPRESSION * EXPRESSION
    | ToExpression of EXPRESSION * EXPRESSION
    | AssignmentExpression of ASSIGNMENT_EXPRESSION
    | ArrayInitExpression of EXPRESSION list * INIT_EXPRESSION list // indexers, initexpr
    | ArrayAccess of EXPRESSION * EXPRESSION // index
    | DotAccess of EXPRESSION * MEMBER list
and ASSIGNMENT_EXPRESSION =
    | ScalarAssignment of OPERATOR_ASSIGNMENT * VARIABLE * EXPRESSION
    | ArrayAssignment of OPERATOR_ASSIGNMENT * VARIABLE  * EXPRESSION list * EXPRESSION // op, var, indices, expr
    | ReferenceAssignment of OPERATOR_ASSIGNMENT * EXPRESSION * EXPRESSION
type MULTI_EXPRESSION =
    | SingleValue of EXPRESSION
    | ValueRange of EXPRESSION * EXPRESSION

type FUNCTION_PARAMETER_MODIFIER =
    {
        IsConst : bool
        IsByRef : bool
    }
type FUNCTION_PARAMETER_DEFVAL =
    | Lit of LITERAL
    | Mac of MACRO
type FUNCTION_PARAMETER_TYPE =
    | Mandatory of FUNCTION_PARAMETER_MODIFIER
    | Optional of FUNCTION_PARAMETER_DEFVAL
type FUNCTION_PARAMETER =
    {
        Variable : VARIABLE
        Type : FUNCTION_PARAMETER_TYPE
    }

    
let private AssToAString =
    function
    | Assign -> "="
    | AssignAdd -> "+="
    | AssignSubtract -> "-="
    | AssignMultiply -> "*="
    | AssignIntegerDivide -> "\\="
    | AssignDivide -> "/="
    | AssignModulus -> "%="
    | AssignConcat -> "&="
    | AssignPower -> "^="
    | AssignNand -> "~&&="
    | AssignAnd -> "&&="
    | AssignNxor -> "~^^="
    | AssignXor -> "^^="
    | AssignNor -> "~||="
    | AssignOr -> "||="
    | AssignRotateLeft -> "<<<="
    | AssignRotateRight -> ">>>="
    | AssignShiftLeft -> "<<="
    | AssignShiftRight -> ">>="
let private BinToAString o a b =
    sprintf (match o with
            | StringConcat -> "(%s & %s)"
            | EqualCaseSensitive -> "(%s == %s)"
            | EqualCaseInsensitive -> "(%s = %s)"
            | Unequal -> "(%s != %s)"
            | Greater -> "(%s > %s)"
            | GreaterEqual -> "(%s >= %s)"
            | Lower -> "(%s < %s)"
            | LowerEqual -> "(%s <= %s)"
            | And -> "(%s And %s)"
            | Xor -> "(%s Xor %s)"
            | Nxor -> "(%s Nxor %s)"
            | Nor -> "(%s Nor %s)"
            | Nand -> "(%s Nand %s)"
            | Or -> "(%s Or %s)"
            | Add -> "(%s + %s)"
            | Subtract -> "(%s - %s)"
            | Multiply -> "(%s * %s)"
            | IntegerDivide -> "(%s \\ %s)"
            | Divide -> "(%s / %s)"
            | Modulus -> "(%s %% %s)"
            | Power -> "(%s ^ %s)"
            | BitwiseNand -> "(%s ~&& %s)"
            | BitwiseAnd -> "(%s && %s)"
            | BitwiseNxor -> "(%s ~^^ %s)"
            | BitwiseXor -> "(%s ^^ %s)"
            | BitwiseNor -> "(%s ~|| %s)"
            | BitwiseOr -> "(%s || %s)"
            | BitwiseRotateLeft -> "(%s <<< %s)"
            | BitwiseRotateRight -> "(%s >>> %s)"
            | BitwiseShiftLeft -> "(%s << %s)"
            | BitwiseShiftRight -> "(%s >> %s)"
            ) a b
let rec private ToAString =
    function
    | Literal l ->
        match l with
        | Null -> "null"
        | Default -> "default"
        | True -> "true"
        | False -> "false"
        | Number d -> d.ToString()
        | String s -> sprintf "\"%s\"" (s.Replace("\\", "\\\\").Replace("\"", "\\\""))
    | FunctionCall (f, p) -> sprintf "%s(%s)" f (String.Join (", ", (List.map ToAString p)))
    | ΛFunctionCall (e, p) -> sprintf "%s(%s)" (ToAString e) (String.Join (", ", (List.map ToAString p)))
    | VariableExpression v -> v.ToString()
    | Macro m -> m.ToString()
    | UnaryExpression (o, e) ->
        match o with
        | Identity -> sprintf "%s" (ToAString e)
        | Negate -> sprintf "-%s" (ToAString e)
        | Not -> sprintf "!%s" (ToAString e)
        | BitwiseNot -> sprintf "~%s" (ToAString e)
        | StringLength -> sprintf "%s#" (ToAString e)
        | String1Index (s, l) -> sprintf "(%s @ (%s .. %s))" (ToAString e) (ToAString s) (ToAString l)
        | Dereference -> sprintf "°%s" (ToAString e)
    | BinaryExpression (o, x, y) -> BinToAString o (ToAString x) (ToAString y)
    | TernaryExpression (x, y, z) -> sprintf "(%s ? %s : %s)" (ToAString x) (ToAString y) (ToAString z)
    | ToExpression (f, t) -> sprintf "%s to %s" (ToAString f) (ToAString t)
    | AssignmentExpression (ScalarAssignment (o, v, e)) -> sprintf "%s %s %s" (v.ToString()) (AssToAString o) (ToAString e)
    | AssignmentExpression (ArrayAssignment (o, v, i, e)) -> sprintf "%s[%s] %s %s" (v.ToString()) (String.Join (", ", (List.map ToAString i))) (AssToAString o) (ToAString e)
    | AssignmentExpression (ReferenceAssignment (o, v, e)) -> sprintf "°%s %s %s" (v.ToString()) (AssToAString o) (ToAString e)
    | ArrayInitExpression (_, e) ->
        let rec tstr = function
                       | Single e -> ToAString e
                       | Multiple es -> sprintf "[ %s ]" (es
                                                          |> List.map tstr
                                                          |> String.concat ", ")
        sprintf "[ %s ]" (e
                          |> List.map tstr
                          |> String.concat ", ")
    | ArrayAccess (v, e) -> sprintf "%s[%s]" (ToAString v) (ToAString e)
    | DotAccess (v, d) -> d
                          |> List.map (fun d -> "." + match d with
                                                      | Field f -> f
                                                      | Method m -> ToAString(FunctionCall m)
                                      )
                          |> List.fold (+) (ToAString v)

[<ExtensionAttribute>]
let Print e = ToAString e
