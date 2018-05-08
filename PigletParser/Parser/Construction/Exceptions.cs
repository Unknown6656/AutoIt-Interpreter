using Piglet.Parser.Configuration;

namespace Piglet.Parser.Construction
{
    /// <summary>
    /// Base class for exceptions thrown by the parser generator for ambiguous grammars.
    /// </summary>
    public class AmbiguousGrammarException
        : ParserConfigurationException
    {
        internal AmbiguousGrammarException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// The state number in which the conflict occurred.
        /// </summary>
        public int StateNumber { get; internal set; }

        /// <summary>
        /// The token number that generated the conflict
        /// </summary>
        public int TokenNumber { get; internal set; }

        /// <summary>
        /// The previous value of the parsing table at the point of the conflict.
        /// </summary>
        public int PreviousValue { get; internal set; }

        /// <summary>
        /// The new value that was attempted to be written into the parse table
        /// </summary>
        public int NewValue { get; internal set; }
    }

    /// <summary>
    /// A reduce reduce conflict is thrown if the parser configuration is ambiguous so that multiple reduce actions are valid
    /// at the same points. This is usually indicative of a serious grammar error.
    /// </summary>
    /// <typeparam name="T">Semantic value of symbols used in the grammar</typeparam>
    public sealed class ReduceReduceConflictException<T>
        : AmbiguousGrammarException
    {
        /// <summary>
        /// Create a new reduce reduce conflict exception
        /// </summary>
        /// <param name="message">Exception message</param>
        public ReduceReduceConflictException(string message)
            : base (message)
        {
        }

        /// <summary>
        /// The reduce symbol that existed in the parse table before the new reduce symbol was applied.
        /// </summary>
        public ISymbol<T> PreviousReduceSymbol { get; internal set; }

        /// <summary>
        /// The reduce symbol that the parser generator tried to apply.
        /// </summary>
        public ISymbol<T> NewReduceSymbol { get; internal set; }

        public override string Message => $"The grammar contains a reduce-reduce conflict.\nPrevious shift symbol: {PreviousReduceSymbol}\nNew reduce symbol: {NewReduceSymbol}";
    }

    /// <summary>
    /// A shift reduce conflict exception is thrown by the parser generator when the grammar is
    /// ambiguous in such a way that the parser cannot decide if to shift another token or to reduce
    /// by a given rule.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ShiftReduceConflictException<T>
        : AmbiguousGrammarException
    {
        /// <summary>
        /// Construct a new shift reduce exception
        /// </summary>
        /// <param name="message">Exception message</param>
        public ShiftReduceConflictException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// The shift symbol in the conflict
        /// </summary>
        public ISymbol<T> ShiftSymbol { get; internal set; }

        /// <summary>
        /// The reduce symbol in the conflict
        /// </summary>
        public ISymbol<T> ReduceSymbol { get; internal set; }

        public override string Message => $"The grammar contains a shift-reduce conflict.\nShift symbol: {ShiftSymbol}\nReduce symbol: {ReduceSymbol}";
    }
}