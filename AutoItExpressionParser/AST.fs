module AutoItExpressionParser.AST

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

type OPERATOR_BINARY_COMPARISON =
    | EqualCaseSensitive
    | EqualCaseInsensitive
    | Unequal
    | Greater
    | GraterEqual
    | Lower
    | LowerEqual
type OPERATOR_BINARY_LOGIC =
    | And
    | Or
type OPERATOR_BINARY_NUMERIC =
    | Add
    | Subtract
    | Multiply
    | Divide
    | Power
type OPERATOR_BINARY_ASSIGNMENT =
    | Assign
    | AssignAdd
    | AssignSubtract
    | AssignMultiply
    | AssignDivide
    | AssignConcat
type OPERATOR_BINARY =
    | BinaryAssignment of OPERATOR_BINARY_ASSIGNMENT
    | BinaryComparison of OPERATOR_BINARY_COMPARISON
    | BinaryNumeric of OPERATOR_BINARY_NUMERIC
    | BinaryLogic of OPERATOR_BINARY_LOGIC
    | StringConcat
type OPERATOR_UNARY =
    | Negate
    | Not
type OPERATOR_TERNARY =
    | Conditional
type EXPRESSION =
    | FunctionCall of string * EXPRESSION list
    | Variable of VARIABLE
    | Macro of MACRO
    | UnaryExpression of OPERATOR_UNARY * EXPRESSION
    | BinaryExpression of OPERATOR_BINARY * EXPRESSION * EXPRESSION
    | TernaryExpression of OPERATOR_TERNARY * EXPRESSION * EXPRESSION * EXPRESSION
