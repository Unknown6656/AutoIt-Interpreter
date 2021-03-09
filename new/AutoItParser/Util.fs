namespace Unknown6656.AutoIt3.Parser


type Associativity =
    | Left
    | Right

[<AutoOpen>]
module Util =
    let inline (?) a b c = if a then b else c
    
    let inline (|>>) (a, b) f = f a, f b
    
    let inline (|>>>) (a, b, c) f = f a, f b, f c

    let (|As|_|) (p:'T) : 'U option =
        let p = p :> obj
        if p :? 'U then Some (p :?> 'U) else None
