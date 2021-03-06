﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Piglet.Parser.Construction;

namespace Piglet.Parser.Configuration
{
    internal class NonTerminal<T>
        : Symbol<T>
        , INonTerminal<T>
    {
        private readonly IParserConfigurator<T> configurator;
        private readonly IList<NonTerminalProduction> productions;


        public NonTerminal(IParserConfigurator<T> configurator)
        {
            this.configurator = configurator;
            this.productions = new List<NonTerminalProduction>();
        }

        public IEnumerable<IProductionRule<T>> ProductionRules => productions;

        public IProduction<T> AddProduction(params object[] parts)
        {
            if (parts.Any(part => !(part is string || part is ISymbol<T>)))
                throw new ArgumentException("Only string and ISymbol are valid arguments.", nameof(parts));

            NonTerminalProduction nonTerminalProduction = new NonTerminalProduction(configurator, this, parts);

            productions.Add(nonTerminalProduction);

            return nonTerminalProduction;
        }

        public override string ToString() => $"{DebugName} --> {string.Join(" | ", from r in ProductionRules select string.Join(" ", from s in r.Symbols select s is ITerminal<T> ? $"'{s.DebugName}'" : s.DebugName))}";


        internal class NonTerminalProduction
            : IProduction<T>
            , IProductionRule<T>
        {
            private readonly ISymbol<T>[] symbols;
            private readonly INonTerminal<T> resultSymbol;

            public ISymbol<T>[] Symbols => symbols;
            public ISymbol<T> ResultSymbol => resultSymbol;
            public Func<ParseException, T[], T> ReduceAction { get; private set; }
            public IPrecedenceGroup ContextPrecedence { get; private set; }


            public NonTerminalProduction(IParserConfigurator<T> configurator, INonTerminal<T> resultSymbol, object[] symbols)
            {
                this.resultSymbol = resultSymbol;
                this.symbols = new ISymbol<T>[symbols.Length]; // Move production symbols to the list
                int i = 0;

                foreach (object part in symbols)
                {
                    if (part is string regex)
                    {
                        if (configurator.LexerSettings.EscapeLiterals)
                            regex = Regex.Escape(regex);

                        this.symbols[i] = configurator.CreateTerminal(regex, null, true);
                        this.symbols[i].DebugName = part as string; // Set debug name to unescaped string, so it's easy on the eyes.
                    }
                    else
                        this.symbols[i] = (ISymbol<T>)symbols[i];

                    ++i;
                }
            }

            public void SetReduceFunction(Func<T[], T> action) => ReduceAction = (_, f) => action(f);

            public void SetReduceToFirst() => SetReduceFunction(f => f[0]);

            public void SetReduceToIndex(int index) => SetReduceFunction(f => f[index]);

            public void SetPrecedence(IPrecedenceGroup precedenceGroup) => ContextPrecedence = precedenceGroup;

            public void SetErrorFunction(Func<ParseException, T[], T> errorHandler) => ReduceAction = errorHandler;

            public override string ToString()
            {
                string tstr<U>(ISymbol<U> s) => s is ITerminal<U> ? $"'{s.DebugName}'" : s.DebugName;

                return $"{string.Join(" ", symbols.Select(tstr))} --> {tstr(ResultSymbol)}";
            }
        }
    }
}