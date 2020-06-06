module Unknown6656.AutoIt3.ExpressionParser.AST


type VARIABLE (name : string) =
    member x.Name = if name.StartsWith('$') then name.Substring 1 else name
    override x.ToString() = "$" + x.Name
    override x.GetHashCode() = x.Name.ToLower().GetHashCode()
    override x.Equals o =
        match o with
        | As (m : VARIABLE) -> m.GetHashCode() = x.GetHashCode()
        | _ -> false

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
    | AssignConcat

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
    | Or
    | Add
    | Subtract
    | Multiply
    | Divide
    | Power

type OPERATOR_UNARY =
    | Identity
    | Negate
    | Not

type MEMBER =
    | Field of string
    | Method of FUNCCALL

and FUNCCALL = string * EXPRESSION list

and ARRAY_INIT_EXPRESSION =
    | Multiple of ARRAY_INIT_EXPRESSION list
    | Single of EXPRESSION

and EXPRESSION =
    | Literal of LITERAL
    | FunctionCall of FUNCCALL
    | Macro of MACRO
    | UnaryExpression of OPERATOR_UNARY * EXPRESSION
    | BinaryExpression of OPERATOR_BINARY * EXPRESSION * EXPRESSION
    | TernaryExpression of EXPRESSION * EXPRESSION * EXPRESSION
    | ToExpression of EXPRESSION * EXPRESSION
    | AssignmentExpression of ASSIGNMENT_EXPRESSION
    | ArrayInitExpression of EXPRESSION list * ARRAY_INIT_EXPRESSION list // indexers, initexpr
    | ArrayAccess of EXPRESSION * EXPRESSION // index
    | DotAccess of EXPRESSION * MEMBER
    | ContextualDotAccess of MEMBER // inside with expressions!

and ASSIGNMENT_EXPRESSION =
    | ScalarAssignment of VARIABLE * OPERATOR_ASSIGNMENT * EXPRESSION
    | ArrayAssignment of VARIABLE * EXPRESSION list * OPERATOR_ASSIGNMENT * EXPRESSION // op, var, indices, expr

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
