namespace AutoItExpressionParser

open Microsoft.FSharp.Quotations.Patterns

open Piglet.Parser.Configuration
open Piglet.Parser


[<AutoOpen>]
module Util =
    let internal (+>) (o : obj[]) n = unbox o.[n]

    let GetModuleType = function
                        | PropertyGet (_, propertyInfo, _) -> propertyInfo.DeclaringType
                        | _ -> failwith "Expression is no property."

    let (|As|_|) (p:'T) : 'U option =
        let p = p :> obj
        if p :? 'U then Some (p :?> 'U) else None

type OperatorAssociativity = 
    | Left
    | Right

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

[<AbstractClass>]
type AbstractParser<'a>() =
    let config = ParserFactory.Configure<obj>()
    let mutable parser : IParser<obj> = null
    member x.Configuration with get() = config
    member internal x.t = x.Configuration.CreateTerminal >> TerminalWrapper<string>
    member internal x.tf<'T> regex (onParse : (string -> 'T)) = TerminalWrapper<'T>(x.Configuration.CreateTerminal(regex, (fun s -> box (onParse s))))
    member internal x.nt<'T> name = name
                                    |> x.Configuration.CreateNonTerminal
                                    |> NonTerminalWrapper<'T>
    member internal x.a d s =
        let arg = List.map (fun (f : SymbolWrapper<_>) -> downcast f.Symbol)
               >> List.toArray
        match d with
        | Left -> x.Configuration.LeftAssociative(arg s)
        | Right -> x.Configuration.RightAssociative(arg s)
        |> ignore
    abstract BuildParser : unit -> unit
    member x.Initialize() =
        x.BuildParser()
        parser <- (x.Configuration : IParserConfigurator<obj>).CreateParser()
    member x.Parse (s : string) =
        if parser = null then
            x.Initialize()
        parser.Parse(s.Replace('\t', ' ')) :?> 'a

        
type int = System.Int32
type char = System.Char
type long = System.Int64
type decimal = System.Decimal
            
[<AutoOpen>]
module ProductionUtil =
    let internal reducegf (s : NonTerminalWrapper<'a>) x =
        let p = s.AddProduction()
        p.SetReduceFunction x
        p
    let internal reduceg0 (s : NonTerminalWrapper<'a>) a =
        let p = s.AddProduction(a)
        p.SetReduceToFirst()
        p
    let internal reduceg1 (s : NonTerminalWrapper<'a>) a x =
        let p = s.AddProduction(a)
        p.SetReduceFunction x
        p
    let internal reduceg2 (s : NonTerminalWrapper<'a>) a b x =
        let p = s.AddProduction(a, b)
        p.SetReduceFunction x
        p
    let internal reduceg3 (s : NonTerminalWrapper<'a>) a b c x =
        let p = s.AddProduction(a, b, c)
        p.SetReduceFunction x
        p
    let internal reduceg4 (s : NonTerminalWrapper<'a>) a b c d x =
        let p = s.AddProduction(a, b, c, d)
        p.SetReduceFunction x
        p
    let internal reduceg5 (s : NonTerminalWrapper<'a>) a b c d e x =
        let p = s.AddProduction(a, b, c, d, e)
        p.SetReduceFunction x
        p
    let internal reduceg6 (s : NonTerminalWrapper<'a>) a b c d e f x =
        let p = s.AddProduction(a, b, c, d, e, f)
        p.SetReduceFunction x
        p
    let internal reduceg7 (s : NonTerminalWrapper<'a>) a b c d e f g x =
        let p = s.AddProduction(a, b, c, d, e, f, g)
        p.SetReduceFunction x
        p
    let internal reduceg8 (s : NonTerminalWrapper<'a>) a b c d e f g h x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h)
        p.SetReduceFunction x
        p
    let internal reduceg9 (s : NonTerminalWrapper<'a>) a b c d e f g h i x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h, i)
        p.SetReduceFunction x
        p
    let internal reduceg10 (s : NonTerminalWrapper<'a>) a b c d e f g h i j x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h, i, j)
        p.SetReduceFunction x
        p
    let internal reduceg11 (s : NonTerminalWrapper<'a>) a b c d e f g h i j k x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h, i, j, k)
        p.SetReduceFunction x
        p
    let internal reduceg12 (s : NonTerminalWrapper<'a>) a b c d e f g h i j k l x =
        let p = s.AddProduction(a, b, c, d, e, f, g, h, i, j, k, l)
        p.SetReduceFunction x
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
