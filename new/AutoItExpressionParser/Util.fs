namespace Unknown6656.AutoIt3.ExpressionParser

open Piglet.Parser.Configuration.Wrapper


[<AutoOpen>]
module Util =
    let (|As|_|) (p:'T) : 'U option =
        let p = p :> obj
        if p :? 'U then Some (p :?> 'U) else None


    let internal reducegf (s : NonTerminalWrapper<'a>) x =
        let p = s.AddProduction()
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg0 (s : NonTerminalWrapper<'a>) a =
        let p = s.AddProduction(a)
        p.SetReduceToFirst()
        p
    let internal reduceg1 (s : NonTerminalWrapper<'a>) a x =
        let p = s.AddProduction(a)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg2 (s : NonTerminalWrapper<'a>) a b x =
        let p = s.AddProduction(a, b)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg3 (s : NonTerminalWrapper<'a>) a b c x =
        let p = s.AddProduction(a, b, c)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg4 (s : NonTerminalWrapper<'a>) a b c d x =
        let p = s.AddProduction(a, b, c, d)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg5 (s : NonTerminalWrapper<'a>) a b c d e x =
        let p = s.AddProduction(a, b, c, d, e)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg6 (s : NonTerminalWrapper<'a>) a b c d e f x =
        let p = s.AddProduction(a, b, c, d, e, f)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg7 (s : NonTerminalWrapper<'a>) a b c d e f g x =
        let p = s.AddProduction(a, b, c, d, e, f, g)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg8 (s : NonTerminalWrapper<'a>) a b c d e f g h x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg9 (s : NonTerminalWrapper<'a>) a b c d e f g h i x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h, i)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg10 (s : NonTerminalWrapper<'a>) a b c d e f g h i j x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h, i, j)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg11 (s : NonTerminalWrapper<'a>) a b c d e f g h i j k x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h, i, j, k)
        p.SetReduceFunction x |> ignore
        p
    let internal reduceg12 (s : NonTerminalWrapper<'a>) a b c d e f g h i j k l x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h, i, j, k, l)
        p.SetReduceFunction x |> ignore
        p
        
    let internal reducef (s : NonTerminalWrapper<'a>) x = ignore <| reducegf s x
    let internal reduce0 (s : NonTerminalWrapper<'a>) a = ignore <| reduceg0 s a
    let internal reduce1 (s : NonTerminalWrapper<'a>) a x = ignore <| reduceg1 s a x
    let internal reduce2 (s : NonTerminalWrapper<'a>) a b x = ignore <| reduceg2 s a b x
    let internal reduce3 (s : NonTerminalWrapper<'a>) a b c x = ignore <| reduceg3 s a b c x
    let internal reduce4 (s : NonTerminalWrapper<'a>) a b c d x = ignore <| reduceg4 s a b c d x
    let internal reduce5 (s : NonTerminalWrapper<'a>) a b c d e x = ignore <| reduceg5 s a b c d e x
    let internal reduce6 (s : NonTerminalWrapper<'a>) a b c d e f x = ignore <| reduceg6 s a b c d e f x
    let internal reduce7 (s : NonTerminalWrapper<'a>) a b c d e f g x = ignore <| reduceg7 s a b c d e f g x
    let internal reduce8 (s : NonTerminalWrapper<'a>) a b c d e f g h x = ignore <| reduceg8 s a b c d e f g h x
    let internal reduce9 (s : NonTerminalWrapper<'a>) a b c d e f g h i x = ignore <| reduceg9 s a b c d e f g h i x
    let internal reduce10 (s : NonTerminalWrapper<'a>) a b c d e f g h i j x = ignore <| reduceg10 s a b c d e f g h i j x
    let internal reduce11 (s : NonTerminalWrapper<'a>) a b c d e f g h i j k x = ignore <| reduceg11 s a b c d e f g h i j k x
    let internal reduce12 (s : NonTerminalWrapper<'a>) a b c d e f g h i j k l x = ignore <| reduceg12 s a b c d e f g h i j k l x
