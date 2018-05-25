using System;

namespace AutoItCoreLibrary
{
    public delegate ref V RefFunc<I, V>(I arg);


    public interface IReadOnly<I, V>
    {
        V this[I i] { get; }
    }

    public interface IWriteOnly<I, V>
    {
        V this[I i] { set; }
    }

    public sealed class Indexer<I, V>
        : IReadOnly<I, V>
        , IWriteOnly<I, V>
    {
        internal Action<I, V> Setter { get; }
        internal Func<I, V> Getter { get; }


        public V this[I i]
        {
            set => Setter(i, value);
            get => Getter(i);
        }

        public Indexer(Func<I, V> getter, Action<I, V> setter)
        {
            Setter = setter is null ? throw new ArgumentException("The setter function must not be null.", nameof(setter)) : setter;
            Getter = getter is null ? throw new ArgumentException("The getter function must not be null.", nameof(getter)) : getter;
        }
    }

    public sealed class ByReferenceIndexer<I, V>
    {
        internal RefFunc<I, V> Getter { get; }


        public ref V this[I i] => ref Getter(i);

        public ByReferenceIndexer(RefFunc<I, V> getter) => Getter = getter is null ? throw new ArgumentException("The getter function must not be null.", nameof(getter)) : getter;

        public static implicit operator Indexer<I, V>(ByReferenceIndexer<I, V> refindexer) => new Indexer<I, V>(i => refindexer[i], (i, v) => refindexer[i] = v);
    }

    public sealed class ReadOnlyIndexer<I, V>
        : IReadOnly<I, V>
    {
        internal Func<I, V> Getter { get; }


        public V this[I i] => Getter(i);

        public ReadOnlyIndexer(Func<I, V> getter) => Getter = getter is null ? throw new ArgumentException("The getter function must not be null.", nameof(getter)) : getter;
    }

    public sealed class WriteOnlyIndexer<I, V>
        : IWriteOnly<I, V>
    {
        internal Action<I, V> Setter { get; }


        public V this[I i]
        {
            set => Setter(i, value);
        }

        public WriteOnlyIndexer(Action<I, V> setter) => Setter = setter is null ? throw new ArgumentException("The setter function must not be null.", nameof(setter)) : setter;
    }
}
