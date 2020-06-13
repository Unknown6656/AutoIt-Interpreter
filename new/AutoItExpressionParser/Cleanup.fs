namespace Unknown6656.AutoIt3.ExpressionParser

open AST
open System


module Cleanup =
    let AsNumber =
        function
        | Null
        | False -> 0m
        | True -> 1m
        | Default -> -1m
        | Number d -> d
        | String s ->
            match Decimal.TryParse s with
            | true, d -> d
            | _ -> 0m
    
    let AsBoolean =
        function
        | Null
        | Default
        | Number 0m
        | String ""
        | False -> false
        | _ -> true

    let AsString =
        function
        | Null -> ""
        | Default -> "Default"
        | Number d -> d.ToString()
        | String s -> s
        | False -> "False"
        | True -> "True"

    let rec FoldConstants =
        function
        | Unary (Identity, Literal Null) -> Literal False
        | Unary (Identity, e) -> e
        | Unary (Negate, Literal Default) -> Literal Default
        | Unary (Negate, Literal Null) -> Literal Null
        | Unary (Negate, Literal l) -> 
            -(AsNumber l)
            |> Number
            |> Literal
        | Unary (Not, Literal l) -> 
            (?) (not(AsBoolean l)) True False
            |> Literal
        | Unary (op, e) as exp ->
            let res = Unary(op, FoldConstants e)
            (?) (res = exp) id FoldConstants <| res
        | Binary (Literal l1, op, Literal e2) ->
            // TODO : fold binary
            ()
        | Binary (e1, op, e2) as exp ->
            let e1' = FoldConstants e1
            let e2' = FoldConstants e2
            if e1' = e1 && e2' = e2 then exp
            else
                (e1', op, e2')
                |> Binary
                |> FoldConstants
        | Ternary (a, b, c) as exp ->
            let b' = FoldConstants b
            let c' = FoldConstants c
            match FoldConstants a with
            | Literal True -> b'
            | Literal False -> c'
            | a' -> Ternary(a', b', c')
        | Member e -> Member(FoldMember e)
        | Indexer (e, i) -> Indexer(FoldConstants e, FoldConstants i)
        | FunctionCall (DirectFunctionCall (i, a)) ->
            (i, List.map FoldConstants a)
            |> DirectFunctionCall
            |> FunctionCall
        | FunctionCall (MemberCall (e, a)) ->
            (FoldMember e, List.map FoldConstants a)
            |> MemberCall
            |> FunctionCall
        | e -> e

    and FoldMember =
        function
        | ExplicitMemberAccess (e, i) ->
            ExplicitMemberAccess(FoldConstants e, i)
        | m -> m 

    let CleanUpExpression expression =
        match expression with
        | AnyExpression(Binary(Variable var, EqualCaseInsensitive, source)) -> (VariableAssignment var, Assign, source)
        | AnyExpression(Binary(Indexer idx, EqualCaseInsensitive, source)) -> (IndexedAssignment idx, Assign, source)
        | AnyExpression(Binary(Member memb, EqualCaseInsensitive, source)) -> (MemberAssignemnt memb, Assign, source)
        | AssignmentExpression e -> e
        | AnyExpression e -> (VariableAssignment VARIABLE.Discard, Assign, e)
        |> fun (target, op, expr) -> struct(target, op, expr)