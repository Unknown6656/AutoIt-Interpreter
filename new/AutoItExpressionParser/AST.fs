module Unknown6656.AutoIt3.ExpressionParser.AST


type IDENTIFIER = Identifier of string

type VARIABLE (name : string) =
    member _.Name = if name.StartsWith('$') then name.Substring 1 else name
    override x.ToString() = "$" + x.Name
    override x.GetHashCode() = x.Name.ToLower().GetHashCode()
    override x.Equals o =
        match o with
        | As (m : VARIABLE) -> m.GetHashCode() = x.GetHashCode()
        | _ -> false
    static member Discard = VARIABLE "_"

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
    with
        member x.ReferencedVariables =
            match x with
            | Macro _
            | Literal _ -> []
            | Variable v -> [v]
            | FunctionCall e -> e.ReferencedVariables
            | Unary (_, e) -> e.ReferencedVariables
            | Indexer (e1, e2)
            | Binary (e1, _, e2) -> List.distinct (e1.ReferencedVariables@e2.ReferencedVariables)
            | Ternary (e1, e2, e3) -> List.distinct (e1.ReferencedVariables@e2.ReferencedVariables@e3.ReferencedVariables)
            | Member m -> m.ReferencedVariables

and UNARY_EXPRESSION = OPERATOR_UNARY * EXPRESSION

and BINARY_EXPRESSION = EXPRESSION * OPERATOR_BINARY * EXPRESSION

and TERNARY_EXPRESSION = EXPRESSION * EXPRESSION * EXPRESSION

and ASSIGNMENT_TARGET =
    | VariableAssignment of VARIABLE
    | IndexedAssignment of INDEXER_EXPRESSION
    | MemberAssignemnt of MEMBER_EXPRESSION
    with
        member x.ReferencedVariables =
            match x with
            | VariableAssignment v -> [v]
            | IndexedAssignment (e, i) -> List.distinct (e.ReferencedVariables@i.ReferencedVariables)
            | MemberAssignemnt m -> m.ReferencedVariables

and ASSIGNMENT_EXPRESSION = ASSIGNMENT_TARGET * OPERATOR_ASSIGNMENT * EXPRESSION

and MEMBER_EXPRESSION =
    | ExplicitMemberAccess of EXPRESSION * IDENTIFIER
    | ImplicitMemberAccess of IDENTIFIER
    with
        member x.ReferencedVariables =
            match x with
            | ExplicitMemberAccess (e, _) -> e.ReferencedVariables
            | ImplicitMemberAccess _ -> []

and INDEXER_EXPRESSION = EXPRESSION * EXPRESSION // expr, index

and FUNCCALL_ARGUMENTS = EXPRESSION list

and FUNCCALL_EXPRESSION =
    | DirectFunctionCall of IDENTIFIER * FUNCCALL_ARGUMENTS
    | MemberCall of MEMBER_EXPRESSION * FUNCCALL_ARGUMENTS
    with
        member x.ReferencedVariables =
            match x with
            | DirectFunctionCall (_, args) -> args
            | MemberCall (m, args) -> (Member m)::args
            |> List.map (fun arg -> arg.ReferencedVariables)
            |> List.concat
            |> List.distinct

type VARIABLE_DECLARATION = VARIABLE * EXPRESSION option

type PARAMETER_DECLARATION =
    {
        Variable : VARIABLE
        DefaultValue : EXPRESSION option
        IsConst : bool
        IsByRef : bool
    }

type PARSABLE_EXPRESSION =
    | MultiDeclarationExpression of VARIABLE_DECLARATION list
    | AssignmentExpression of ASSIGNMENT_EXPRESSION
    | ParameterDeclaration of PARAMETER_DECLARATION list
    | AnyExpression of EXPRESSION
    with
        /// An array of referenced (not declared!) variables
        member x.ReferencedVariables =
            match x with
            | AssignmentExpression (target, _, expr) -> 
                target.ReferencedVariables@expr.ReferencedVariables
                |> List.distinct
            | AnyExpression expr -> expr.ReferencedVariables
            |> List.toArray
