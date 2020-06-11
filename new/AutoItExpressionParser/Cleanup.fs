namespace Unknown6656.AutoIt3.ExpressionParser

open AST


module Cleanup =
    let CleanUpExpression expression =
        match expression with
        | AnyExpression(Binary(Variable var, EqualCaseInsensitive, source)) -> (VariableAssignment var, Assign, source)
        | AnyExpression(Binary(Indexer idx, EqualCaseInsensitive, source)) -> (IndexedAssignment idx, Assign, source)
        | AnyExpression(Binary(Member memb, EqualCaseInsensitive, source)) -> (MemberAssignemnt memb, Assign, source)
        | AssignmentExpression e -> e
        | AnyExpression e -> (VariableAssignment VARIABLE.Discard, Assign, e)
        |> fun (target, op, expr) -> struct(target, op, expr)