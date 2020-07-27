using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    using static AST;

    /// <summary>
    /// Represents a collection of variables associated with a common scope.
    /// A scope can be the global variable scope (all variables declared on the global level), or a local scope (e.g. variables declared inside methods).
    /// </summary>
    public sealed class VariableScope
        : IDisposable
    {
        private readonly ConcurrentHashSet<VariableScope> _children = new();
        private readonly ConcurrentHashSet<Variable> _variables = new();


        public VariableScope[] ChildScopes => _children.ToArray();

        /// <summary>
        /// Returns all variables declared in this scope. This value is sequentially equal to <see cref="GlobalVariables"/> if the current scope is the global variable scope.
        /// </summary>
        public Variable[] LocalVariables => _variables.ToArray();

        /// <summary>
        /// Returns all variables declared in the global scope associated with the current interpreter instance.
        /// </summary>
        public Variable[] GlobalVariables => GlobalRoot.LocalVariables;

        /// <summary>
        /// Indicates whether the current variable scope is equal to the global variable scope of the interpreter which is associated with the current instance.
        /// </summary>
        public bool IsGlobalScope => Parent is null;

        /// <summary>
        /// The current scope's internal name. This is mainly used for debugging purposes and can be subject to change.
        /// </summary>
        public string InternalName { get; }

        /// <summary>
        /// Returns the optional call frame with which the current scope is being associated.
        /// </summary>
        public CallFrame? CallFrame { get; }

        /// <summary>
        /// The interpreter instance associated with the current variable scope.
        /// </summary>
        public Interpreter Interpreter { get; }

        /// <summary>
        /// The optional parent variable scope. A value of <see langword="null"/> indicates that the current instance represents the global variable scope.
        /// </summary>
        public VariableScope? Parent { get; }

        /// <summary>
        /// The global root variable scope of the interpreter with which this scope is associated.
        /// </summary>
        public VariableScope GlobalRoot { get; }


        private VariableScope(Interpreter interpreter, CallFrame? frame, VariableScope? parent)
        {
            Parent = parent;
            CallFrame = frame;
            Interpreter = interpreter;
            GlobalRoot = parent?.GlobalRoot ?? this;
            InternalName = parent is null ? "/" : $"{parent.InternalName}/{frame?.CurrentFunction.Name ?? "::"}-{parent._children.Count}";
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Parent is { _children: { } children })
            {
                DestroyAllVariables(false);
                children.Remove(this);
            }

            foreach (VariableScope child in _children.ToArray())
                child.Dispose();

            _children.Dispose();
            _variables.Dispose();
        }

        /// <inheritdoc/>
        public override string ToString() => $"\"{InternalName}\"{(IsGlobalScope ? " (global)" : "")}: {_variables.Count} Variables, {_children.Count} Child scopes";

        /// <summary>
        /// Creates a new temporary variable for the current scope. This variable will only be visible to the current scope and child scopes.
        /// </summary>
        /// <returns>The newly created temporary variable.</returns>
        public Variable CreateTemporaryVariable() => CreateVariable((CallFrame as AU3CallFrame)?.CurrentLocation ?? SourceLocation.Unknown, $"tmp__{Guid.NewGuid():N}", false);

        /// <summary>
        /// Creates a new variable with the given name.
        /// </summary>
        /// <param name="location">The variable's declaration location. Use <see cref="SourceLocation.Unknown"/> if the variable is not associated with any source code line.</param>
        /// <param name="variable">The variable's name.</param>
        /// <param name="isConst">Indicator whether the AutoIt variable has been declared as <see langword="Const"/>.</param>
        /// <returns>The newly created temporary variable.</returns>
        public Variable CreateVariable(SourceLocation location, VARIABLE variable, bool isConst) => CreateVariable(location, variable.Name, isConst);

        /// <summary>
        /// Creates a new variable with the given name.
        /// </summary>
        /// <param name="location">The variable's declaration location. Use <see cref="SourceLocation.Unknown"/> if the variable is not associated with any source code line.</param>
        /// <param name="variable">The variable's name.</param>
        /// <param name="isConst">Indicator whether the AutoIt variable has been declared as <see langword="Const"/>.</param>
        /// <returns>The newly created temporary variable.</returns>
        public Variable CreateVariable(SourceLocation location, string name, bool isConst) => Interpreter.Telemetry.Measure(TelemetryCategory.VariableCreation, delegate
        {
            if (!TryGetVariable(name, VariableSearchScope.Local, out Variable? var))
            {
                var = new Variable(this, location, name, isConst);

                _variables.Add(var);
            }

            return var;
        });

        /// <summary>
        /// Returns whether the scope (or any parent scope) contains a variable with the given name (case-insensitive).
        /// </summary>
        /// <param name="name">The case-insensitive variable name.</param>
        /// <param name="scope">The scope in which the variable should be searched.</param>
        /// <returns>Indicates whether a variable with the given criteria has been found.</returns>
        public bool HasVariable(string name, VariableSearchScope scope) => TryGetVariable(name, scope, out _);

        /// <summary>
        /// Returns whether the scope (or any parent scope) contains a variable with the given name (case-insensitive).
        /// </summary>
        /// <param name="name">The case-insensitive variable name.</param>
        /// <param name="scope">The scope in which the variable should be searched.</param>
        /// <returns>Indicates whether a variable with the given criteria has been found.</returns>
        public bool HasVariable(VARIABLE variable, VariableSearchScope scope) => HasVariable(variable.Name, scope);

        /// <summary>
        /// Finds a variable with the given case-insensitive name in the given search scope (or any parent scope).
        /// </summary>
        /// <param name="name">The case-insensitive variable name.</param>
        /// <param name="scope">The scope in which the variable should be searched.</param>
        /// <param name="variable">The found variable (or <see langword="null"/> if the variable could not be found).</param>
        /// <returns>Indicates whether a variable with the given criteria has been found.</returns>
        public bool TryGetVariable(VARIABLE name, VariableSearchScope scope, [MaybeNullWhen(false), NotNullWhen(true)] out Variable? variable) =>
            TryGetVariable(name.Name, scope, out variable);

        /// <summary>
        /// Finds a variable with the given case-insensitive name in the given search scope (or any parent scope).
        /// </summary>
        /// <param name="name">The case-insensitive variable name.</param>
        /// <param name="scope">The scope in which the variable should be searched.</param>
        /// <param name="variable">The found variable (or <see langword="null"/> if the variable could not be found).</param>
        /// <returns>Indicates whether a variable with the given criteria has been found.</returns>
        public bool TryGetVariable(string name, VariableSearchScope scope, [MaybeNullWhen(false), NotNullWhen(true)] out Variable? variable)
        {
            Variable? v = null;
            bool resolved = Interpreter.Telemetry.Measure(TelemetryCategory.VariableResolving, delegate
            {
                foreach (Variable var in _variables)
                    if (var.Equals(name))
                    {
                        v = var;

                        return true;
                    }

                return Parent is { } && scope != VariableSearchScope.Local ? Parent.TryGetVariable(name, scope, out v) : false;
            });

            variable = v;

            return resolved && variable is { };
        }

        /// <summary>
        /// Destroys the variable associated with the given case-insensitive name in the given search scope.
        /// </summary>
        /// <param name="name">The case-insensitive variable name.</param>
        /// <param name="scope">The scope in which the variable should be destroyed.</param>
        /// <returns>A boolean value indicating whether a variable matching the given criteria has been destroyed.</returns>
        public bool DestroyVariable(string name, VariableSearchScope scope)
        {
            if (TryGetVariable(name, scope, out Variable? var) && _variables.Remove(var))
                return true;
            else if (scope != VariableSearchScope.Local)
                return Parent?.DestroyVariable(name, scope) ?? false;

            return false;
        }

        /// <summary>
        /// Destroys all variables of the current scope and any child scope.
        /// All variables in the parent scopes will be destroyed if <paramref name="recursive"/> is set to <see langword="true"/>.
        /// </summary>
        /// <param name="recursive">
        /// <list type="table">
        ///     <item>
        ///         <term><see langword="false"/></term>
        ///         <description>All variables in the current and any child scope will be destroyed.</description>
        ///     </item>
        ///     <item>
        ///         <term><see langword="true"/></term>
        ///         <description>All variables in the current and any child scope will be destroyed. Furthermore, all variables in the parent scope will be destroyed.</description>
        ///     </item>
        /// </list>
        /// </param>
        public void DestroyAllVariables(bool recursive)
        {
            foreach (VariableScope scope in _children)
                scope.DestroyAllVariables(recursive);

            _children.Clear();

            if (recursive && Parent is { } p)
                p.DestroyAllVariables(recursive);
        }

        /// <summary>
        /// Creates a new child scope associated with the given call frame. The newly created variable will have the current scope as a parent.
        /// </summary>
        /// <param name="frame">The call frame with which the newly created variable scope will be associated.</param>
        /// <returns>The newly created variable scope.</returns>
        public VariableScope CreateChildScope(CallFrame frame)
        {
            VariableScope res = new VariableScope(Interpreter, frame, this);

            _children.Add(res);

            return res;
        }

        /// <summary>
        /// Creates a new global scope for the given AutoIt <see cref="Runtime.Interpreter"/>.
        /// </summary>
        /// <param name="interpreter">Interpreter for which the variable scope will be created.</param>
        /// <returns>The newly created variable scope.</returns>
        public static VariableScope CreateGlobalScope(Interpreter interpreter) => new VariableScope(interpreter, null, null);
    }

    /// <summary>
    /// Represents an object storage unit for global .NET objects.
    /// </summary>
    public sealed class GlobalObjectStorage
        : IDisposable
    {
        private static volatile uint _id = 0;
        private readonly ConcurrentDictionary<uint, object> _objects = new ConcurrentDictionary<uint, object>();


        /// <summary>
        /// The AutoIt interpreter associated with the current global object storage.
        /// </summary>
        public Interpreter Interpreter { get; }

        /// <summary>
        /// A list of all currently unique handles in use. Each handle is associated with a global .NET object.
        /// </summary>
        public Variant[] HandlesInUse => _objects.Keys.Select(Variant.FromHandle).ToArray();

        /// <summary>
        /// The count of current active .NET objects (this is equivalent with the count of <see cref="HandlesInUse"/>).
        /// </summary>
        public int ObjectCount => _objects.Count;

        internal IEnumerable<object> Objects => _objects.Values;


        internal GlobalObjectStorage(Interpreter interpreter)
        {
            Interpreter = interpreter;
        }

        private Variant GetFreeId()
        {
            do
                ++_id;
            while (_objects.Keys.Contains(_id));

            return Variant.FromHandle(_id);
        }

        /// <summary>
        /// Stores the given .NET object into the global object storage and returns the handle associated with the stored object.
        /// </summary>
        /// <typeparam name="T">The generic type of the item (constrained to <see langword="class"/>).</typeparam>
        /// <param name="item">The item to be stored.</param>
        /// <returns>The handle associated with the stored object.</returns>
        public Variant Store<T>(T item) where T : class
        {
            Variant handle = GetFreeId();

            TryUpdate(handle, item);

            return handle;
        }

        public bool TryGet(Variant handle, [MaybeNullWhen(false), NotNullWhen(true)] out object? item) => _objects.TryGetValue((uint)handle, out item);

        /// <summary>
        /// Resolves the given handle to the .NET object with the given type.
        /// </summary>
        /// <typeparam name="T">Type of the object to be resolved (constrained to <see langword="class"/>).</typeparam>
        /// <param name="handle">The handle to be resolved.</param>
        /// <param name="item">The resolved object (or <see langword="null"/> if the object could not be resolved and converted to <typeparamref name="T"/>).</param>
        /// <returns>Indicates whether the object resolution <i>and</i> type conversion were successful.</returns>
        public bool TryGet<T>(Variant handle, out T? item) where T : class
        {
            bool res = TryGet(handle, out object? value);

            item = value as T;

            return res;
        }

        public bool TryUpdate<T>(Variant handle, T item)
            where T : class
        {
            bool success;

            if (success = (handle.Type is VariantType.Handle))
                _objects[(uint)handle] = item;

            return success;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (Variant handle in HandlesInUse)
                Delete(handle);
        }

        /// <summary>
        /// Deletes the object associated with the given object handle and returns whether the deletion was successful.
        /// </summary>
        /// <param name="handle">The handle associated with the object to be deleted.</param>
        /// <returns>Deletion result. <see langword="true"/> indicates that the associated object has been found and deleted. Otherwise <see langword="false"/>.</returns>
        public bool Delete(Variant handle) => handle.Type is VariantType.Handle && Delete((uint)handle);

        private bool Delete(uint id)
        {
            bool success = _objects.TryRemove(id, out object? obj);

            if (success && obj is IDisposable disp)
                disp.Dispose();

            return success;
        }
    }

    /// <summary>
    /// Represents an enumeration containing possible variable search scopes.
    /// </summary>
    public enum VariableSearchScope
    {
        /// <summary>
        /// Represents a local search scope.
        /// </summary>
        Local,
        /// <summary>
        /// Represents a global search scope.
        /// </summary>
        Global
    }
}
