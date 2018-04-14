using System;

namespace Piglet.Lexer.Runtime
{
    internal class TabularLexer<T> : LexerBase<T, int>
    {
        private readonly TransitionTable<T> _transitiontable;


        public TabularLexer(TransitionTable<T> transitionTable, int endOfInputTokenNumber)
            : base(endOfInputTokenNumber) => this._transitiontable = transitionTable;

        protected override bool ReachedTermination(int nextState) => nextState == -1;

        protected override int GetNextState(int state, char input) => _transitiontable[state, input];

        protected override Tuple<int, Func<string, T>> GetAction(int state) => _transitiontable.GetAction(state);

        protected override int GetInitialState() => 0;
    }
}