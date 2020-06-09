module Unknown6656.AutoIt3.ExpressionParser.AST



type IDENTIFIER = Identifier of string

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

type EXPRESSION =
    | Literal of LITERAL
    | Variable of VARIABLE
    | Macro of MACRO
    | Unary of UNARY_EXPRESSION
    | Binary of BINARY_EXPRESSION
    | Ternary of TERNARY_EXPRESSION
    | Member of MEMBER_EXPRESSION
    | Indexer of INDEXER_EXPRESSION
    | FunctionCall of FUNCCALL_EXPRESSION

and UNARY_EXPRESSION = OPERATOR_UNARY * EXPRESSION

and BINARY_EXPRESSION = EXPRESSION * OPERATOR_BINARY * EXPRESSION

and TERNARY_EXPRESSION = EXPRESSION * EXPRESSION * EXPRESSION

and ASSIGNMENT_TARGET =
    | VariableAssignment of VARIABLE
    | IndexedAssignment of INDEXER_EXPRESSION
    | MemberAssignemnt of MEMBER_EXPRESSION

and ASSIGNMENT_EXPRESSION = ASSIGNMENT_TARGET * OPERATOR_ASSIGNMENT * EXPRESSION

and MEMBER_EXPRESSION =
    | ExplicitMemberAccess of EXPRESSION * IDENTIFIER
    | ImplicitMemberAccess of IDENTIFIER

and INDEXER_EXPRESSION = EXPRESSION * EXPRESSION // expr, index

and FUNCCALL_ARGUMENTS = EXPRESSION list

and FUNCCALL_EXPRESSION =
    | DirectFunctionCall of IDENTIFIER * FUNCCALL_ARGUMENTS
    | MemberCall of MEMBER_EXPRESSION * FUNCCALL_ARGUMENTS

type PARSABLE_EXPRESSION =
    | AssignmentExpression of ASSIGNMENT_EXPRESSION
    | AnyExpression of EXPRESSION
