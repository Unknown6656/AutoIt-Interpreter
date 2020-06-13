namespace Unknown6656.AutoIt3.ExpressionParser


[<AutoOpen>]
module Util =
    let inline (?) a b c = if a then b else c

    let (|As|_|) (p:'T) : 'U option =
        let p = p :> obj
        if p :? 'U then Some (p :?> 'U) else None
