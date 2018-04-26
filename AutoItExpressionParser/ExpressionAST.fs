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
        
// https://www.autoitscript.com/autoit3/docs/macros.htm
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
    | Identity
    | Negate
    | Not
    | BitwiseNot

type MEMBER =
    | Field of string
    | Method of FUNCCALL
and VARIABLE_EXPRESSION =
    | DotAccess of VARIABLE * MEMBER list
    | Variable of VARIABLE
and FUNCCALL = string * EXPRESSION list
and EXPRESSION =
    | Literal of LITERAL
    | FunctionCall of FUNCCALL
    | VariableExpression of VARIABLE_EXPRESSION
    | Macro of MACRO
    | ArrayIndex of VARIABLE_EXPRESSION * EXPRESSION
    | UnaryExpression of OPERATOR_UNARY * EXPRESSION
    | BinaryExpression of OPERATOR_BINARY * EXPRESSION * EXPRESSION
    | TernaryExpression of EXPRESSION * EXPRESSION * EXPRESSION
    | ToExpression of EXPRESSION * EXPRESSION
    | AssignmentExpression of ASSIGNMENT_EXPRESSION
    | ArrayInitExpression of EXPRESSION list
    // TODO : dot-access of member elements
and ASSIGNMENT_EXPRESSION =
    | Assignment of OPERATOR_ASSIGNMENT * VARIABLE_EXPRESSION * EXPRESSION
    | ArrayAssignment of OPERATOR_ASSIGNMENT * VARIABLE_EXPRESSION * EXPRESSION * EXPRESSION // op, var, index, expr
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

    
let private (!/) = sprintf "AutoItVariantType.%s"

let rec private VarToCSString =
    function
    | DotAccess (v, d) -> d
                          |> List.map (fun d -> "." + match d with
                                                      | Field f -> f
                                                      | Method m -> ToCSString(FunctionCall m)
                                      )
                          |> List.fold (+) v.Name
    | Variable v -> sprintf "$%s" (v.Name)
and private AssToCSString =
    function
    | Assign -> "="
    | AssignAdd -> "+="
    | AssignSubtract -> "-="
    | AssignMultiply -> "*="
    | AssignDivide -> "/="
    | AssignModulus -> "%="
    | AssignConcat -> "&="
    | AssignPower -> "^="

    // TODO
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
and private BinToCSString o a b =
    sprintf (match o with
            | StringConcat -> "(%s & %s)"
            | EqualCaseSensitive -> "(%s == %s)"
            | EqualCaseInsensitive -> "AutoItVariantType.Equals(%s, %s, true)"
            | Unequal -> "(%s != %s)"
            | Greater -> "(%s > %s)"
            | GreaterEqual -> "(%s >= %s)"
            | Lower -> "(%s < %s)"
            | LowerEqual -> "(%s <= %s)"
            | And -> "(%s && %s)"
            | Xor -> "((bool)%s ^ (bool)%s)"
            | Nxor -> "!((bool)%s ^ (bool)%s)"
            | Nor -> "!(%s || %s)"
            | Nand -> "!(%s && %s)"
            | Or -> "(%s || %s)"
            | Add -> "(%s + %s)"
            | Subtract -> "(%s - %s)"
            | Multiply -> "(%s * %s)"
            | Divide -> "(%s / %s)"
            | Modulus -> "(%s %% %s)"
            | Power -> "(%s ^ %s)"
            | BitwiseNand -> "AutoItVariantType.BitwiseNand(%s, %s)"
            | BitwiseAnd -> "AutoItVariantType.BitwiseAnd(%s, %s)"
            | BitwiseNxor -> "AutoItVariantType.BitwiseNxor(%s, %s)"
            | BitwiseXor -> "AutoItVariantType.BitwiseXor(%s, %s)"
            | BitwiseNor -> "AutoItVariantType.BitwiseNor(%s, %s)"
            | BitwiseOr -> "AutoItVariantType.BitwiseOr(%s, %s)"
            | BitwiseRotateLeft -> "AutoItVariantType.BitwiseRol(%s, %s)"
            | BitwiseRotateRight -> "AutoItVariantType.BitwiseRor(%s, %s)"
            | BitwiseShiftLeft -> "AutoItVariantType.BitwiseShl(%s, %s)"
            | BitwiseShiftRight -> "AutoItVariantType.BitwiseShr(%s, %s)"
            ) a b
and private ToCSString =
    function
    | Literal l ->
        match l with
        | Null -> !/"Null"
        | Default -> !/"Default"
        | True -> "true"
        | False -> "false"
        | Number d -> d.ToString()
        | String s -> sprintf "\"%s\"" (s.Replace("\\", "\\\\").Replace("\"", "\\\""))
    | FunctionCall (f, es) -> sprintf "%s(%s)" f (String.Join (", ", (List.map ToCSString es)))
    | VariableExpression v -> VarToCSString v
    | Macro m -> sprintf "___macros___[\"%s\"]" m.Name
    | ArrayIndex (v, e) ->  sprintf "%s[%s]" (VarToCSString v) (ToCSString e)
    | UnaryExpression (o, e) ->
        match o with
        | Identity -> ""
        | Negate -> "-"
        | Not -> "!"
        | BitwiseNot -> "~"
        + ToCSString e
    | BinaryExpression (o, x, y) -> BinToCSString o (ToCSString x) (ToCSString y)
    | TernaryExpression (x, y, z) -> sprintf "(%s ? %s : %s)" (ToCSString x) (ToCSString y) (ToCSString z)
    | ToExpression (f, t) -> sprintf "%s to %s" (ToCSString f) (ToCSString t)
    | AssignmentExpression a ->
        match a with
        | Assignment (o, v, e) -> sprintf "%s %s %s" (VarToCSString v) (AssToCSString o) (ToCSString e)
        | ArrayAssignment (o, v, i, e) -> sprintf "%s[%s] %s %s" (VarToCSString v) (ToCSString i) (AssToCSString o) (ToCSString e)
    | ArrayInitExpression e -> sprintf "[ %s ]" (e
                                                 |> List.map ToCSString
                                                 |> String.concat ", ")

[<ExtensionAttribute>]
let Print e = ToCSString e
