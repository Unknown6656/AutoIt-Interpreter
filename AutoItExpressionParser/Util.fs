[<AutoOpen>]
module AutoItExpressionParser.Util

open Piglet.Parser.Configuration


let private (+>) (o : obj[]) n = unbox o.[n]

type ProductionWrapperBase (p : IProduction<obj>) =
    member x.Production = p
    member x.SetReduceToFirst () = p.SetReduceToFirst()
    member x.SetPrecedence(precedenceGroup) = p.SetPrecedence(precedenceGroup)

type ProductionWrapper<'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : (unit -> 'T)) =
        p.SetReduceFunction (fun o -> box (f ()))

type ProductionWrapper<'a,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'T)) =
        p.SetReduceFunction (fun o -> o+>0
                                      |> f
                                      |> box)

type ProductionWrapper<'a,'b,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1)))

type ProductionWrapper<'a,'b,'c,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2)))

type ProductionWrapper<'a,'b,'c,'d,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'd -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2) (o+>3)))

type ProductionWrapper<'a,'b,'c,'d,'e,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'd -> 'e -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2) (o+>3) (o+>4)))

type ProductionWrapper<'a,'b,'c,'d,'e,'f,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2) (o+>3) (o+>4) (o+>5)))

type ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'g -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2) (o+>3) (o+>4) (o+>5) (o+>6)))

type ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'g -> 'h -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2) (o+>3) (o+>4) (o+>5) (o+>6) (o+>7)))

type ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'i,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'g -> 'h -> 'i -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2) (o+>3) (o+>4) (o+>5) (o+>6) (o+>7) (o+>8)))

type ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'g -> 'h -> 'i -> 'j -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2) (o+>3) (o+>4) (o+>5) (o+>6) (o+>7) (o+>8)))
        
type ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j,'k,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'g -> 'h -> 'i -> 'j -> 'k -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2) (o+>3) (o+>4) (o+>5) (o+>6) (o+>7) (o+>8) (o+>9)))
        
type ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j,'k,'l,'T> (p : IProduction<obj>) =
    inherit ProductionWrapperBase(p)
    member x.SetReduceFunction (f : ('a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'g -> 'h -> 'i -> 'j -> 'k -> 'l -> 'T)) =
        p.SetReduceFunction (fun o -> box (f (o+>0) (o+>1) (o+>2) (o+>3) (o+>4) (o+>5) (o+>6) (o+>7) (o+>8) (o+>9) (o+>10)))

type SymbolWrapper<'T> (symbol : ISymbol<obj>) =
    member x.Symbol = symbol

type TerminalWrapper<'T> (terminal : ITerminal<obj>) =
    inherit SymbolWrapper<'T>(terminal)

type NonTerminalWrapper<'T> (nonTerminal : INonTerminal<obj>) =
    inherit SymbolWrapper<'T>(nonTerminal)
    
    let (!>) (p : SymbolWrapper<'a>) = p.Symbol;

    member x.AddProduction () =
        nonTerminal.AddProduction()
        |> ProductionWrapper<'T>
        
    member x.AddProduction p = nonTerminal.AddProduction !>p
                               |> ProductionWrapper<'a,'T>
        
    member x.AddProduction (p1, p2) =
        nonTerminal.AddProduction(!>p1, !>p2)
        |> ProductionWrapper<'a,'b,'T>
        
    member x.AddProduction (p1, p2, p3) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3)
        |> ProductionWrapper<'a,'b,'c,'T>
        
    member x.AddProduction (p1, p2, p3, p4) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3, !>p4)
        |> ProductionWrapper<'a,'b,'c,'d,'T>
        
    member x.AddProduction (p1, p2, p3, p4, p5) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3, !>p4, !>p5)
        |> ProductionWrapper<'a,'b,'c,'d,'e,'T>
        
    member x.AddProduction (p1, p2, p3, p4, p5, p6) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3, !>p4, !>p5, !>p6)
        |> ProductionWrapper<'a,'b,'c,'d,'e,'f,'T>
        
    member x.AddProduction (p1, p2, p3, p4, p5, p6, p7) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3, !>p4, !>p5, !>p6, !>p7)
        |> ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'T>
        
    member x.AddProduction (p1, p2, p3, p4, p5, p6, p7, p8) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3, !>p4, !>p5, !>p6, !>p7, !>p8)
        |> ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'T>

    member x.AddProduction (p1, p2, p3, p4, p5, p6, p7, p8, p9) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3, !>p4, !>p5, !>p6, !>p7, !>p8, !>p9)
        |> ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'i,'T>
        
    member x.AddProduction (p1, p2, p3, p4, p5, p6, p7, p8, p9, p10) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3, !>p4, !>p5, !>p6, !>p7, !>p8, !>p9, !>p10)
        |> ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j,'T>
        
    member x.AddProduction (p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3, !>p4, !>p5, !>p6, !>p7, !>p8, !>p9, !>p10, !>p11)
        |> ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j,'k,'T>
        
    member x.AddProduction (p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12) =
        nonTerminal.AddProduction(!>p1, !>p2, !>p3, !>p4, !>p5, !>p6, !>p7, !>p8, !>p9, !>p10, !>p11, !>p12)
        |> ProductionWrapper<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j,'k,'l,'T>


let (|As|_|) (p:'T) : 'U option =
    let p = p :> obj
    if p :? 'U then Some (p :?> 'U) else None
