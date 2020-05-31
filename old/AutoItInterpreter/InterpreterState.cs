using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;

using AutoItExpressionParser.SyntaxHighlightning;
using AutoItInterpreter.Preprocessed;
using AutoItInterpreter.PartialAST;
using AutoItExpressionParser;
using AutoItCoreLibrary;

namespace AutoItInterpreter
{
    using static InterpreterConstants;
    using static PInvoke;


    public struct RawLine
    {
        public int[] OriginalLineNumbers { get; }
        public string Content { get; }
        public FileInfo File { get; }

        public DefinitionContext Context => new DefinitionContext(File, OriginalLineNumbers[0], OriginalLineNumbers.Length > 1 && OriginalLineNumbers[1] > OriginalLineNumbers[0] ? (int?)OriginalLineNumbers[1] : null);


        public RawLine(string c, int[] l, FileInfo f) => (Content, OriginalLineNumbers, File) = (c, l, f);

        public override string ToString() => $"{File.Name}@{string.Join(":", OriginalLineNumbers)} \"{Content?.Trim() ?? ""}\"";
    }

    internal static class InterpreterConstants
    {
        public const string CSHARP_INLINE = "§___csharp___";
        public const string DISCARD_VARIBLE = "$___discard___";
        public const string ERROR_VARIBLE = "$___error___";
        public const string GLOBAL_FUNC_NAME = "__global<>";
        public static readonly string CMP_INCLUDE_DIR = $"{typeof(Interpreter).Assembly.Location}/../include/";
        public static readonly string[] RESERVED_KEYWORDS = SyntaxHighlighter.Keywords;

        private static readonly OS[] ALL_OS = new[] { OS.Windows, OS.MacOS, OS.Linux };
        private static readonly CompilerIntrinsicMessage[] NO_MSG = new CompilerIntrinsicMessage[0];
        public static readonly BuiltinFunctionInformation[] BUILT_IN_FUNCTIONS;


        static InterpreterConstants()
        {
            BUILT_IN_FUNCTIONS = new BuiltinFunctionInformation[]
            {
                ("eval", "", 1, 0, false, ALL_OS, false, NO_MSG),
                ("assign", "", 2, 1, false, ALL_OS, false, NO_MSG),
                ("isdeclared", "", 1, 0, false, ALL_OS, false, NO_MSG)
            }.Concat(from m in typeof(AutoItFunctions).GetMethods(BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public)
                     let attrs = m.GetCustomAttributes(true)
                     where attrs.Any(attr => attr is BuiltinFunctionAttribute)
                     let us = attrs.Any(attr => attr is RequiresUnsafeAttribute)
                     let os = attrs.Filter<object, CompatibleOSAttribute>().FirstOrDefault()?.Systems ?? ALL_OS
                     let msgs = attrs.Filter<object, CompilerIntrinsicMessage>().ToArray()
                     let pars = m.GetParameters()
                     let opars = pars.Where(p => p.IsOptional).ToArray()
                     let varargs = (pars.Length > 0) && pars[pars.Length - 1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0
                     select new BuiltinFunctionInformation(m.Name.ToLower(), m.Name, pars.Length - opars.Length, opars.Length, varargs, os, us, msgs)).ToArray();
        }

        public static bool IsReservedCall(string name)
        {
            name = name?.ToLower() ?? "";

            return new[] { GLOBAL_FUNC_NAME, ERROR_VARIBLE, DISCARD_VARIBLE, CSHARP_INLINE }.Concat(RESERVED_KEYWORDS).Contains(name)
                || name.Match("^__(<>.+|.+<>)$", out _);
        }

        public static bool IsReservedName(string name) => BUILT_IN_FUNCTIONS.Any(x => x.Name == name?.ToLower()) || IsReservedCall(name);
    }

    internal sealed class ReversedLabelStack
    {
        private readonly List<AST_LABEL> _stack = new List<AST_LABEL>();


        /// <summary>level 1 == top-most</summary>
        public AST_LABEL this[uint level] => level == 0 || level > _stack.Count ? null : _stack[_stack.Count - (int)level];

        public void Push(AST_LABEL lb) => _stack.Add(lb);

        public void Clear() => _stack.Clear();

        public AST_LABEL Pop()
        {
            if (_stack.Count > 0)
            {
                int index = _stack.Count - 1;
                AST_LABEL lb = _stack[index];

                _stack.RemoveAt(index);

                return lb;
            }
            else
                return null;
        }
    }

    public abstract class AbstractParserState
    {
        private protected List<InterpreterError> _errors;

        public Dictionary<string, (string Signature, string Library, DefinitionContext Context)> PInvokeFunctions { get; }
        public List<(string Func, DefinitionContext Context)> StartFunctions { get; }
        public List<(string Func, DefinitionContext Context)> ExitFunctions { get; }
        public List<string> Namespaces { get; }
        public CompileInfo CompileInfo { private protected set; get; }
        public FileInfo RootDocument { get; set; }
        public Language Language { get; set; }
        public bool IsIncludeOnce { set; get; }
        public bool RequireAdmin { set; get; }
        public bool UseTrayIcon { set; get; }

        public bool Fatal => Errors.Any(err => err.Type == ErrorType.Fatal);

        public InterpreterError[] Errors => _errors.ToArray();


        public AbstractParserState()
        {
            PInvokeFunctions = new Dictionary<string, (string, string, DefinitionContext)>();
            _errors = new List<InterpreterError>();
            StartFunctions = new List<(string, DefinitionContext)>();
            ExitFunctions = new List<(string, DefinitionContext)>();
            Namespaces = new List<string>();
            CompileInfo = new CompileInfo();
            UseTrayIcon = true;
        }

        public void ReportError(string msg, DefinitionContext ctx, int num) => _errors.Add(new InterpreterError(msg, ctx, num, ErrorType.Fatal));

        public void ReportWarning(string msg, DefinitionContext ctx, int num) => _errors.Add(new InterpreterError(msg, ctx, num, ErrorType.Warning));

        public void ReportNote(string msg, DefinitionContext ctx, int num) => _errors.Add(new InterpreterError(msg, ctx, num, ErrorType.Note));

        internal void Report(InterpreterError error) => _errors.Add(error);

        internal void ReportKnownError(string errname, DefinitionContext ctx, params object[] args) => ReportError(Language[errname, args], ctx, Language.GetErrorNumber(errname));

        internal void ReportKnownWarning(string errname, DefinitionContext ctx, params object[] args) => ReportWarning(Language[errname, args], ctx, Language.GetErrorNumber(errname));

        internal void ReportKnownNote(string errname, DefinitionContext ctx, params object[] args) => ReportNote(Language[errname, args], ctx, Language.GetErrorNumber(errname));

        internal void RemoveLastErrorOrWarning()
        {
            if (_errors.Count > 0)
                _errors.RemoveAt(_errors.Count - 1);
        }

        internal void ElevateWarningsToErrors()
        {
            foreach (InterpreterError err in Errors)
                err.ElevateSeriousness();
        }
    }

    public sealed class InterpreterState
        : AbstractParserState
    {
        public (string, PINVOKE_SIGNATURE)[] PInvokeSignatures { get; set; }
        public (FileInfo Path, RawLine[] Lines)[] Sources { get; set; }
        public Dictionary<string, AST_FUNCTION> ASTFunctions { get; }
        public Dictionary<string, FUNCTION> Functions { get; }
        internal DebugPrintUtil.FinalResult Result { get; set; }
        public FileInfo OutputFile { get; set; }


        public InterpreterState()
        {
            Functions = new Dictionary<string, FUNCTION>();
            ASTFunctions = new Dictionary<string, AST_FUNCTION>();
        }

        public static InterpreterState Convert(PreInterpreterState ps)
        {
            InterpreterState s = new InterpreterState
            {
                IsIncludeOnce = ps.IsIncludeOnce,
                RequireAdmin = ps.RequireAdmin,
                UseTrayIcon = ps.UseTrayIcon,
                CompileInfo = ps.CompileInfo,
                Language = ps.Language,
            };
            s.StartFunctions.AddRange(ps.StartFunctions);
            s.ExitFunctions.AddRange(ps.ExitFunctions);
            s.Namespaces.AddRange(ps.Namespaces);
            s._errors.AddRange(ps.Errors);

            foreach (KeyValuePair<string, (string, string, DefinitionContext)> kvp in ps.PInvokeFunctions)
                s.PInvokeFunctions[kvp.Key] = kvp.Value;

            return s;
        }

        public string GetFunctionSignature(string funcname) => $"func {funcname}({Functions[funcname].RawParameters})";
    }

    public sealed class PreInterpreterState
        : AbstractParserState
    {
        public Dictionary<string, FunctionScope> Functions { get; }
        public InterpreterContext CurrentContext { set; get; }
        public List<string> IncludeOncePaths { get; }

        public FunctionScope GlobalFunction
        {
            set => Functions[GLOBAL_FUNC_NAME] = value;
            get => Functions[GLOBAL_FUNC_NAME];
        }


        public PreInterpreterState()
        {
            Functions = new Dictionary<string, FunctionScope> { [GLOBAL_FUNC_NAME] = null };
            IncludeOncePaths = new List<string>();
            UseTrayIcon = true;
        }

        public string GetFunctionSignature(string funcname) => $"func {funcname}({Functions[funcname].ParameterExpression})";
    }

    public sealed class FunctionScope
    {
        public List<(string Line, DefinitionContext Context)> Lines { get; }
        public string ParameterExpression { get; }
        public DefinitionContext Context { get; }


        public FunctionScope(DefinitionContext ctx, string @params)
        {
            Lines = new List<(string, DefinitionContext)>();
            ParameterExpression = @params ?? "";
            Context = ctx;
        }
    }

    public sealed class InterpreterContext
    {
        public FileInfo SourcePath { get; }
        public string Content { get; }


        public InterpreterContext(string path)
            : this(new FileInfo(path))
        {
        }

        public InterpreterContext(FileInfo path)
        {
            SourcePath = path;

            if (SourcePath.Exists)
                Content = File.ReadAllText(SourcePath.FullName);
        }
    }

    public sealed class CompileInfo
    {
        public string FileName { set; get; } = "AutoItApplication.exe";
        public string IconPath { set; get; }
        public ExecutionLevel ExecLevel { set; get; }
        public Compatibility Compatibility { set; get; }
        public Architecture TargetArchitecture { set; get; }
        public bool AutoItExecuteAllowed { set; get; }
        public bool ConsoleMode { set; get; }
        public byte Compression { set; get; }
        public bool UPX { set; get; }
        public bool InputBoxRes { set; get; }
        public string AssemblyComment { set; get; }
        public string AssemblyCompanyName { set; get; }
        public string AssemblyFileDescription { set; get; }
        public Version AssemblyFileVersion { set; get; }
        public string AssemblyInternalName { set; get; }
        public string AssemblyCopyright { set; get; }
        public string AssemblyTrademarks { set; get; }
        public string AssemblyProductName { set; get; }
        public Version AssemblyProductVersion { set; get; }


        internal CompileInfo()
        {
        }
    }

    public sealed class InterpreterError
    {
        public ErrorType Type { private set; get; }
        public DefinitionContext ErrorContext { get; }
        public string ErrorMessage { get; }
        public int ErrorNumber { get; }


        internal InterpreterError(string msg, DefinitionContext line, int number, ErrorType type)
        {
            Type = type;
            ErrorMessage = msg;
            ErrorContext = line;
            ErrorNumber = number;
        }

        public void @throw() => throw (InvalidProgramException)this;

        public override string ToString() => $"(AU{ErrorNumber:D4})  {ErrorContext}: {ErrorMessage}";

        internal void ElevateSeriousness()
        {
            if (Type == ErrorType.Warning)
                Type = ErrorType.Fatal;
            else if (Type == ErrorType.Note)
                Type = ErrorType.Warning;
        }


        public static implicit operator InvalidProgramException(InterpreterError err) => new InvalidProgramException(err.ToString())
        {
            Source = err.ErrorContext.FilePath.FullName
        };
    }

    public readonly struct BuiltinFunctionInformation
    {
        public CompilerIntrinsicMessage[] IntrinsicMessages { get; }
        public int MandatoryArgumentCount { get; }
        public int OptionalArgumentCount { get; }
        public bool HasParamsArguments { get; }
        public string RealName { get; }
        public bool IsUnsafe { get; }
        public OS[] Systems { get; }
        public string Name { get; }


        internal BuiltinFunctionInformation(string lname, string name, int m_argc, int o_argc, bool @params, OS[] sys, bool @unsafe, CompilerIntrinsicMessage[] attrs)
        {
            Name = lname;
            RealName = name;
            MandatoryArgumentCount = m_argc;
            OptionalArgumentCount = o_argc;
            HasParamsArguments = @params;
            IsUnsafe = @unsafe;
            Systems = sys;
            IntrinsicMessages = attrs;
        }

        public override string ToString()
        {
            IEnumerable<string> args = Enumerable.Range(0, MandatoryArgumentCount).Select(_ => "v").Concat(Enumerable.Range(0, MandatoryArgumentCount).Select(_ => "v?"));

            return $"{(IsUnsafe ? "unsafe " : "")}{Name}({string.Join(", ", args)}{(HasParamsArguments ? ", v..." : "")})";
        }

        public static implicit operator BuiltinFunctionInformation((string lname, string name, int m_argc, int o_argc, bool @params, OS[] sys, bool @unsafe, CompilerIntrinsicMessage[] attrs) t) =>
            new BuiltinFunctionInformation(t.lname, t.name, t.m_argc, t.o_argc, t.@params, t.sys, t.@unsafe, t.attrs);
    }

    public enum ExecutionLevel
    {
        None,
        AsInvoker,
        HighestAvailable,
        RequireAdministrator
    }

    public enum ControlBlock
    {
        __NONE__,
        IfElifElseBlock,
        If,
        ElseIf,
        Else,
        Select,
        Switch,
        Case,
        For,
        While,
        Do,
        With,
    }

    public enum ErrorType
        : byte
    {
        Fatal = 0,
        Warning = 1,
        Note = 2
    }

    public enum Compatibility
    {
        winxp,
        vista,
        win7,
        win8,
        win81,
        win10,
        win,
        linux,
        centos,
        debian,
        fedora,
        gentoo,
        opensuse,
        ol,
        rhel,
        tizen,
        ubuntu,
        linuxmint,
        osx,
        android,
    }
}
