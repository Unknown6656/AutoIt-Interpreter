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

    public abstract class Union3<A, B, C>
    {
        public abstract T Match<T>(Func<A, T> f, Func<B, T> g, Func<C, T> h);
        public virtual bool IsA { get; private protected set; }
        public virtual bool IsB { get; private protected set; }
        public virtual bool IsC { get; private protected set; }

        
        private Union3()
        {
        }

        public void Match(Action<A> f, Action<B> g, Action<C> h) =>
            Match<object>(a =>
            {
                f?.Invoke(a);

                return default;
            }, b =>
            {
                g?.Invoke(b);

                return default;
            }, c =>
            {
                h?.Invoke(c);

                return default;
            });

        public static implicit operator Union3<A, B, C>(A i) => new CaseA(i);

        public static implicit operator Union3<A, B, C>(B i) => new CaseB(i);

        public static implicit operator Union3<A, B, C>(C i) => new CaseC(i);

        public static explicit operator A(Union3<A, B, C> u) => u.Match(a => a, default, default);

        public static explicit operator B(Union3<A, B, C> u) => u.Match(default, b => b, default);

        public static explicit operator C(Union3<A, B, C> u) => u.Match(default, default, c => c);


        public sealed class CaseA
            : Union3<A, B, C>
        {
            public readonly A Item;


            public CaseA(A item) => (Item, IsA) = (item, true);

            public override T Match<T>(Func<A, T> f, Func<B, T> g, Func<C, T> h) => f is null ? default : f(Item);
        }

        public sealed class CaseB
            : Union3<A, B, C>
        {
            public readonly B Item;


            public CaseB(B item) => (Item, IsB) = (item, true);

            public override T Match<T>(Func<A, T> f, Func<B, T> g, Func<C, T> h) => g is null ? default : g(Item);
        }

        public sealed class CaseC
            : Union3<A, B, C>
        {
            public readonly C Item;


            public CaseC(C item) => (Item, IsC) = (item, true);

            public override T Match<T>(Func<A, T> f, Func<B, T> g, Func<C, T> h) => h is null ? default : h(Item);
        }
    }
}
