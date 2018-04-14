using Piglet.Parser.Configuration;

namespace Piglet.Parser
{
    /// <summary>
    /// The parserfactory is the main way of obtaining parsers from Piglet.
    /// </summary>
    public static class ParserFactory
    {
        /// <summary>
        /// Create a code based configurator
        /// </summary>
        /// <typeparam name="T">Semantic value type of tokens</typeparam>
        /// <returns>A configurator, ready for use</returns>
        public static IParserConfigurator<T> Configure<T>() => new ParserConfigurator<T>();
    }
}