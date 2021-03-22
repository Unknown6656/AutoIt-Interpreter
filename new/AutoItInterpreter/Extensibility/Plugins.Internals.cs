using System.Collections.Concurrent;

using Unknown6656.AutoIt3.Runtime;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Internals
{
    public sealed class InternalsFunctionProvider
        : AbstractFunctionProvider
    {
        internal static readonly ConcurrentDictionary<string, ((Variant key, Variant value)[] collection, int index)> _iterators = new();


        public InternalsFunctionProvider(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterFunction(nameof(__iterator_create), 2, __iterator_create);
            RegisterFunction(nameof(__iterator_destroy), 1, __iterator_destroy);
            RegisterFunction(nameof(__iterator_canmove), 1, __iterator_canmove);
            RegisterFunction(nameof(__iterator_movenext), 1, __iterator_movenext);
            RegisterFunction(nameof(__iterator_currentkey), 1, __iterator_currentkey);
            RegisterFunction(nameof(__iterator_currentvalue), 1, __iterator_currentvalue);
        }

        public static FunctionReturnValue __iterator_create(CallFrame frame, Variant[] args)
        {
            Variant source = args[1];

            if (args[1].IsIndexable)
                _iterators.TryAdd(args[0].ToString(), (source.ToOrderedMap(frame.Interpreter), 0));

            return Variant.Null;
        }

        public static FunctionReturnValue __iterator_destroy(CallFrame frame, Variant[] args)
        {
            _iterators.TryRemove(args[0].ToString(), out _);

            return Variant.Null;
        }

        public static FunctionReturnValue __iterator_canmove(CallFrame frame, Variant[] args) =>
            Variant.FromBoolean(_iterators.TryGetValue(args[0].ToString(), out var iterator) && iterator.index < iterator.collection.Length);

        public static FunctionReturnValue __iterator_movenext(CallFrame frame, Variant[] args)
        {
            if (_iterators.TryGetValue(args[0].ToString(), out var iterator))
                _iterators.TryUpdate(args[0].ToString(), (iterator.collection, iterator.index + 1), iterator);

            return Variant.Null;
        }

        public static FunctionReturnValue __iterator_currentkey(CallFrame frame, Variant[] args)
        {
            if (_iterators.TryGetValue(args[0].ToString(), out var iterator))
                return iterator.collection[iterator.index].key;

            return Variant.Null;
        }

        public static FunctionReturnValue __iterator_currentvalue(CallFrame frame, Variant[] args)
        {
            if (_iterators.TryGetValue(args[0].ToString(), out var iterator))
                return iterator.collection[iterator.index].value;

            return Variant.Null;
        }
    }
}
