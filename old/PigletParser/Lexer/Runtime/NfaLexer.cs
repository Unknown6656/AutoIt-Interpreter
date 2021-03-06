using System;
using System.Collections.Generic;
using System.Linq;
using Piglet.Lexer.Construction;

namespace Piglet.Lexer.Runtime
{
    internal class NfaLexer<T> : LexerBase<T, HashSet<NFA.State>>
    {
        private readonly NFA nfa;
        private readonly Tuple<NFA.State, Tuple<int, Func<string, T>>>[] actions;

        public NfaLexer(NFA nfa, IEnumerable<NFA> nfas, List<Tuple<string, Func<string, T>>> tokens, int endOfInputTokenNumber)
            : base(endOfInputTokenNumber)
        {
            this.nfa = nfa;
            actions = nfas.Select((n, i) => new Tuple<NFA.State, Tuple<int, Func<string, T>>>(n.States.Single(f => f.AcceptState), new Tuple<int, Func<string, T>>( i,
                i < tokens.Count ? tokens[i].Item2 : null))).ToArray();
        }

        protected override Tuple<int, Func<string, T>> GetAction(HashSet<NFA.State> state)
        {
            // If none of the included states are accepting states we will return null to signal that there is no appropriate
            // action to take
            if (!state.Any(f => f.AcceptState))
            {
                return null;
            }

            // Get the first applicable action. This returns null of there is no action defined but there are accepting
            // states. This is fine, this means an ignored token.
            Tuple<NFA.State, Tuple<int, Func<string, T>>> action = actions.FirstOrDefault(f => state.Contains(f.Item1));
            return action != null && action.Item2.Item2 != null ? action.Item2 : new Tuple<int, Func<string, T>>(int.MinValue, null);
        }

        protected override bool ReachedTermination(HashSet<NFA.State> nextState) => !nextState.Any();

        protected override HashSet<NFA.State> GetNextState(HashSet<NFA.State> state, char input)
        {
            HashSet<NFA.State> nextState = new HashSet<NFA.State>();
            nextState.UnionWith(nfa.Closure(
                nfa.Transitions.Where(t => t.ValidInput.ContainsChar(input) && state.Contains(t.From)).Select(f => f.To).
                    ToArray()));
            return nextState;
        }

        protected override HashSet<NFA.State> GetInitialState()
        {
            HashSet<NFA.State> initialState = new HashSet<NFA.State>();
            initialState.UnionWith(nfa.Closure(new[] { nfa.StartState }));
            return initialState;
        }
    }
}