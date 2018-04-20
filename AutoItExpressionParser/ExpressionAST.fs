module AutoItExpressionParser.ExpressionAST

open AutoItExpressionParser.Util


type VARIABLE (name : string) =
    member x.Name = name
    override x.ToString() = "$" + x.Name
    override x.GetHashCode() = x.Name.ToLower().GetHashCode()
    override x.Equals o =
        match o with
        | As (m : VARIABLE) -> m.GetHashCode() = x.GetHashCode()
        | _ -> false
        
// https://www.autoitscript.com/autoit3/docs/macros.htm
type MACRO (name : string) =
    member x.Name = name
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
    | AssignAnd
    | AssignXor
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
    | Nor
    | Nand
    | Or
    | Add
    | Subtract
    | Multiply
    | Divide
    | Modulus
    | Power
    | BitwiseAnd
    | BitwiseXor
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

type FUNCCALL = string * EXPRESSION list
and EXPRESSION =
    | Literal of LITERAL
    | FunctionCall of FUNCCALL
    | Variable of VARIABLE
    | Macro of MACRO
    | ArrayIndex of VARIABLE * EXPRESSION
    | UnaryExpression of OPERATOR_UNARY * EXPRESSION
    | BinaryExpression of OPERATOR_BINARY * EXPRESSION * EXPRESSION
    | TernaryExpression of EXPRESSION * EXPRESSION * EXPRESSION
    | ToExpression of EXPRESSION * EXPRESSION
    | AssignmentExpression of ASSIGNMENT_EXPRESSION
    // TODO : dot-access of member elements
and ASSIGNMENT_EXPRESSION =
    | Assignment of OPERATOR_ASSIGNMENT * VARIABLE * EXPRESSION
    | ArrayAssignment of OPERATOR_ASSIGNMENT * VARIABLE * EXPRESSION * EXPRESSION // op, var, index, expr
type CASE_EXPRESSION =
    | SingleValue of EXPRESSION
    | ValueRange of EXPRESSION * EXPRESSION
