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
type OPERATOR_BINARY_COMPARISON =
    | EqualCaseSensitive
    | EqualCaseInsensitive
    | Unequal
    | Greater
    | GreaterEqual
    | Lower
    | LowerEqual
type OPERATOR_BINARY_LOGIC =
    | And
    | Xor
    | Or
type OPERATOR_BINARY_NUMERIC =
    | Add
    | Subtract
    | Multiply
    | Divide
    | Modulus
    | Power
type OPERATOR_BINARY_ASSIGNMENT =
    | Assign
    | AssignAdd
    | AssignSubtract
    | AssignMultiply
    | AssignDivide
    | AssignModulus
    | AssignConcat
    | AssignPower
type OPERATOR_BINARY =
    | BinaryAssignment of OPERATOR_BINARY_ASSIGNMENT
    | BinaryComparison of OPERATOR_BINARY_COMPARISON
    | BinaryNumeric of OPERATOR_BINARY_NUMERIC
    | BinaryLogic of OPERATOR_BINARY_LOGIC
    | StringConcat
type OPERATOR_UNARY =
    | Negate
    | Not

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
    // TODO : dot-access of member elements
type ASSIGNMENT_EXPRESSION =
    | Assignment of OPERATOR_BINARY_ASSIGNMENT * VARIABLE * EXPRESSION
    | ArrayAssignment of OPERATOR_BINARY_ASSIGNMENT * VARIABLE * EXPRESSION * EXPRESSION // op, var, index, expr
