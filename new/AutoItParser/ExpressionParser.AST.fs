module Unknown6656.AutoIt3.Parser.ExpressionParser.AST

open Unknown6656.AutoIt3.Parser
open System


type IDENTIFIER = Identifier of string
    with override x.ToString() = match x with Identifier i -> i

type VARIABLE (name : string) =
    let name = name.TrimStart '$'
    member _.Name = name
    member _.IsDiscard = name = "_"
    override _.ToString() = "$" + name
    override _.GetHashCode() = name.GetHashCode StringComparison.InvariantCultureIgnoreCase
    override _.Equals o = match o with
                          | :? VARIABLE as m -> name.Equals(m.Name, StringComparison.InvariantCultureIgnoreCase)
                          | _ -> false
    static member Discard = VARIABLE "_"

type MACRO (name : string) =
    let name = name.TrimStart '@'
    member _.Name = name
    override _.ToString() = "@" + name
    override _.GetHashCode() = name.GetHashCode StringComparison.InvariantCultureIgnoreCase
    override _.Equals o = match o with
                          | :? MACRO as m -> name.Equals(m.Name, StringComparison.InvariantCultureIgnoreCase)
                          | _ -> false

type LITERAL =
    | Null
    | Default
    | True
    | False
    | Number of float
    | String of string
    with
        override x.ToString() =
            match x with
            | Null -> "Null"
            | Default -> "Default"
            | True -> "True"
            | False -> "False"
            | Number d -> d.ToString()
            | String s -> sprintf "\"%s\"" s

type OPERATOR_ASSIGNMENT =
    | Assign
    | AssignAdd
    | AssignSubtract
    | AssignMultiply
    | AssignDivide
    | AssignConcat
    with
        override x.ToString() =
            match x with
            | Assign -> "="
            | AssignAdd -> "+="
            | AssignSubtract -> "-="
            | AssignMultiply -> "*="
            | AssignDivide -> "/="
            | AssignConcat -> "&="

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
    with
        override x.ToString() =
            match x with
            | StringConcat -> "&"
            | EqualCaseSensitive -> "=="
            | EqualCaseInsensitive -> "="
            | Unequal -> "<>"
            | Greater -> ">"
            | GreaterEqual -> ">="
            | Lower -> "<"
            | LowerEqual -> "<="
            | And -> "And"
            | Or -> "Or"
            | Add -> "+"
            | Subtract -> "-"
            | Multiply -> "*"
            | Divide -> "/"
            | Power -> "^"

type CAST_OPERATOR = 
    | CBool
    | CInt
    | CNumber
    | CString
    | CBinary

type OPERATOR_UNARY =
    | Identity
    | Negate
    | Not
    | Cast of CAST_OPERATOR
    with
        override x.ToString() =
            match x with
            | Identity -> "+"
            | Negate -> "-"
            | Not -> "!"
            | Cast c -> sprintf "(:%O)" c

type EXPRESSION =
    | Literal of LITERAL
    | Variable of VARIABLE
    | Macro of MACRO
    | FunctionName of IDENTIFIER
    | ReferenceTo of VARIABLE
    | Unary of UNARY_EXPRESSION
    | Binary of BINARY_EXPRESSION
    | Ternary of TERNARY_EXPRESSION
    | Member of MEMBER_EXPRESSION
    | Indexer of INDEXER_EXPRESSION
    | FunctionCall of FUNCCALL_EXPRESSION
    with
        override x.ToString() =
            match x with
            | Literal l -> l.ToString()
            | Variable v -> v.ToString()
            | Macro m -> m.ToString()
            | FunctionName n -> n.ToString()
            | Unary (u, e) -> sprintf "(%O%O)" u e
            | Binary (e1, b, e2) -> sprintf "(%O %O %O)" e1 b e2
            | Ternary (a, b, c) -> sprintf "(%O ? %O : %O)" a b c
            | Member m -> m.ToString()
            | Indexer (e, i) -> sprintf "%O[%O]" e i
            | FunctionCall f -> f.ToString()
            | ReferenceTo v -> sprintf "Byref %O" v
        member x.ReferencedVariables =
            match x with
            | Macro _
            | FunctionName _
            | Literal _ -> []
            | ReferenceTo v
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
        override x.ToString() =
            match x with
            | VariableAssignment v -> v.ToString()
            | IndexedAssignment (e, i) -> sprintf "%O[%O]" e i
            | MemberAssignemnt m -> m.ToString()
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
        override x.ToString() =
            match x with
            | ExplicitMemberAccess (e, i) -> sprintf "%O.%O" e i
            | ImplicitMemberAccess i -> sprintf ".%O" i
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
    override x.ToString() =
        let printargs = List.map (fun a -> a.ToString()) >> String.concat ", "
        match x with
        | DirectFunctionCall (i, a) -> sprintf "%O(%O)" i (printargs a)
        | MemberCall (m, a)-> sprintf "%O(%O)" m (printargs a)
        member x.ReferencedVariables =
            match x with
            | DirectFunctionCall (_, args) -> args
            | MemberCall (m, args) -> (Member m)::args
            |> List.map (fun arg -> arg.ReferencedVariables)
            |> List.concat
            |> List.distinct

type VARIABLE_DECLARATION =
    | Scalar of EXPRESSION option
    | Array of int * EXPRESSION list
    | Map of unit
    with
        override x.ToString() = 
            match x with
            | Scalar None -> ""
            | Scalar (Some e) -> sprintf " = %O" e
            | Array (s, es) ->
                es
                |> List.map(fun e -> e.ToString())
                |> String.concat ", "
                |> sprintf "[%d] = [%O]" s
            | Map _ -> "[]"

type NAMED_VARIABLE_DECLARATION = VARIABLE * VARIABLE_DECLARATION

type PARAMETER_DECLARATION =
    {
        Variable : VARIABLE
        DefaultValue : EXPRESSION option
        IsConst : bool
        IsByRef : bool
    }
    with
        member x.IsOptional = x.DefaultValue.IsSome
        override x.ToString() =
            let c = (?) x.IsConst "Const " ""
            let r = (?) x.IsByRef "ByRef " ""
            let d = match x.DefaultValue with
                    | Some e -> " = " + e.ToString()
                    | None -> ""
            sprintf "%s%s%O%s" c r x.Variable d

type PARSABLE_EXPRESSION =
    | MultiDeclarationExpression of NAMED_VARIABLE_DECLARATION list
    | AssignmentExpression of ASSIGNMENT_EXPRESSION
    | ParameterDeclaration of PARAMETER_DECLARATION list
    | ToExpression of EXPRESSION * EXPRESSION
    | AnyExpression of EXPRESSION
    with
        override x.ToString() =
            match x with
            | MultiDeclarationExpression n ->
                n
                |> List.map (fun (v, d) -> v.ToString() + d.ToString())
                |> String.concat ", "
                |> sprintf "Decl %O"
            | AssignmentExpression (t, o, e) -> sprintf "%O %O %O" t o e
            | ParameterDeclaration p -> 
                p
                |> List.map (fun p -> p.ToString())
                |> String.concat ", "
                |> sprintf "(%O)"
            | AnyExpression e -> e.ToString()
            | ToExpression (f, t) -> sprintf "%O To %O" f t
        /// An array of referenced (not declared!) variables
        member x.ReferencedVariables =
            match x with
            | MultiDeclarationExpression decls ->
                decls
                |> List.collect (snd >> function
                                        | Map _
                                        | Scalar None -> []
                                        | Array (_, es) -> es
                                        | Scalar (Some e) -> [e])
                |> List.collect (fun e -> e.ReferencedVariables)
            | ToExpression (e1, e2) -> e1.ReferencedVariables @ e2.ReferencedVariables
            | AssignmentExpression (target, _, expr) -> target.ReferencedVariables @ expr.ReferencedVariables
            | AnyExpression expr -> expr.ReferencedVariables
            | ParameterDeclaration decls ->
                decls
                |> List.collect (fun d -> match d.DefaultValue with
                                          | Some e -> [e]
                                          | None -> [])
                |> List.collect (fun e -> e.ReferencedVariables)
            |> List.distinct
            |> List.toArray
