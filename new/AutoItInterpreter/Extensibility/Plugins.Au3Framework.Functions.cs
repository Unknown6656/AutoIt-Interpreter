using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System.IO;
using System;

using Piglet.Parser.Configuration.Generic;

using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;
using Unknown6656.AutoIt3.COM;
using Unknown6656.AutoIt3.CLI;
using Unknown6656.Common;
using Unknown6656.IO;
using Unknown6656.Mathematics;

namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    using static Unknown6656.AutoIt3.Parser.DLLStructParser.AST;

    using TCPHandle = Union<TcpListener, TcpClient>;


    public sealed class FrameworkFunctions
        : AbstractFunctionProvider
    {
        private static readonly Regex REGEX_WS = new(@"[\0\x09-\x0d\x20]{2,}", RegexOptions.Compiled);
        private static readonly Regex REGEX_RUN = new(@"^(?<file>""[^""]*""|[^""]+)(\s+(?<args>.*))?$", RegexOptions.Compiled);
        private static readonly Regex REGEX_DRIVE_ADD = new(@"Drive (?<letter>.:) is now connected to .*The command completed successfully\.", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex REGEX_DRIVE_GET = new(@"Remote name\s+(?<target>.+)", RegexOptions.Compiled);


        public FrameworkFunctions(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterFunction(nameof(AdlibRegister), 1, 2, AdlibRegister);
            RegisterFunction(nameof(AdlibUnRegister), 0, 1, AdlibUnRegister);
            RegisterFunction(nameof(AutoItWinGetTitle), 0, AutoItWinGetTitle, OS.Windows);
            RegisterFunction(nameof(AutoItWinSetTitle), 1, AutoItWinSetTitle);
            // RegisterFunction(nameof(AutoItSetOption), 1, 2, AutoItSetOption, Variant.Default);
            // RegisterFunction("Opt", 1, 2, AutoItSetOption, Variant.Default);
            RegisterFunction(nameof(Assign), 2, 3, Assign);
            RegisterFunction(nameof(BlockInput), 1, BlockInput);
            RegisterFunction(nameof(CDTray), 2, CDTray);
            RegisterFunction(nameof(Abs), 1, Abs);
            RegisterFunction(nameof(ACos), 1, ACos);
            RegisterFunction(nameof(ASin), 1, ASin);
            RegisterFunction(nameof(ATan), 1, ATan);
            RegisterFunction(nameof(Cos), 1, Cos);
            RegisterFunction(nameof(Sin), 1, Sin);
            RegisterFunction(nameof(Exp), 1, Exp);
            RegisterFunction(nameof(Mod), 2, Mod);
            RegisterFunction(nameof(Log), 1, Log);
            RegisterFunction(nameof(Sqrt), 1, Sqrt);
            RegisterFunction(nameof(Floor), 1, Floor);
            RegisterFunction(nameof(Ceiling), 1, Ceiling);
            RegisterFunction(nameof(Round), 1, 2, Round);
            RegisterFunction(nameof(Tan), 1, Tan);
            RegisterFunction(nameof(Asc), 1, Asc);
            RegisterFunction(nameof(AscW), 1, AscW);
            RegisterFunction(nameof(Chr), 1, Chr);
            RegisterFunction(nameof(ChrW), 1, ChrW);
            RegisterFunction(nameof(Beep), 0, 2, Beep, OS.Windows, 500m, 1000m);
            RegisterFunction(nameof(BitAND), 2, 256, BitAND, Enumerable.Repeat((Variant)0xffffffff, 255).ToArray());
            RegisterFunction(nameof(BitOR), 2, 256, BitOR);
            RegisterFunction(nameof(BitXOR), 2, 256, BitXOR);
            RegisterFunction(nameof(BitNOT), 1, BitNOT);
            RegisterFunction(nameof(BitShift), 2, BitShift);
            RegisterFunction(nameof(BitRotate), 1, 3, BitRotate, 1, "W");
            RegisterFunction(nameof(String), 1, String);
            RegisterFunction(nameof(Binary), 1, Binary);
            RegisterFunction(nameof(BinaryLen), 1, BinaryLen);
            RegisterFunction(nameof(BinaryMid), 2, 3, BinaryMid, Variant.Default);
            RegisterFunction(nameof(Number), 1, Number);
            RegisterFunction(nameof(Int), 1, Int);
            RegisterFunction(nameof(Dec), 1, Dec);
            RegisterFunction(nameof(Hex), 1, 2, Hex, Variant.Default);
            RegisterFunction(nameof(BinaryToString), 1, 2, BinaryToString, 1);
            RegisterFunction(nameof(StringToBinary), 1, 2, StringToBinary, 1);
            RegisterFunction(nameof(Call), 1, 256, Call);
            RegisterFunction(nameof(ConsoleWrite), 1, ConsoleWrite);
            RegisterFunction(nameof(ConsoleWriteError), 1, ConsoleWriteError);
            RegisterFunction(nameof(ConsoleRead), 2, ConsoleRead);
            RegisterFunction(nameof(DirCreate), 1, DirCreate);
            RegisterFunction(nameof(DirCopy), 2, 3, DirCopy, Variant.Zero);
            RegisterFunction(nameof(DirGetSize), 1, 2, DirGetSize, Variant.Zero);
            RegisterFunction(nameof(DirMove), 2, 3, DirMove, Variant.Zero);
            RegisterFunction(nameof(DirRemove), 1, 2, DirRemove, Variant.Zero);
            RegisterFunction(nameof(DllCall), 3, 255, DllCall);
            RegisterFunction(nameof(DllCallAddress), 2, 254, DllCallAddress);
            RegisterFunction(nameof(DllCallbackFree), 1, DllCallbackFree);
            RegisterFunction(nameof(DllCallbackGetPtr), 1, DllCallbackGetPtr);
            RegisterFunction(nameof(DllCallbackRegister), 3, DllCallbackRegister);
            RegisterFunction(nameof(DllClose), 1, DllClose);
            RegisterFunction(nameof(DllOpen), 1, DllOpen);
            // RegisterFunction(nameof(DllStructCreate), , DllStructCreate);
            // RegisterFunction(nameof(DllStructGetData), , DllStructGetData);
            // RegisterFunction(nameof(DllStructGetPtr), , DllStructGetPtr);
            // RegisterFunction(nameof(DllStructGetSize), , DllStructGetSize);
            // RegisterFunction(nameof(DllStructSetData), , DllStructSetData);
            RegisterFunction(nameof(DriveMapAdd), 2, 5, DriveMapAdd, OS.Windows);
            RegisterFunction(nameof(DriveMapDel), 1, DriveMapDel, OS.Windows);
            RegisterFunction(nameof(DriveMapGet), 1, DriveMapGet, OS.Windows);
            RegisterFunction(nameof(DriveGetDrive), 1, DriveGetDrive);
            RegisterFunction(nameof(DriveGetFileSystem), 1, DriveGetFileSystem);
            RegisterFunction(nameof(DriveGetLabel), 1, DriveGetLabel);
            RegisterFunction(nameof(DriveSetLabel), 2, DriveSetLabel, OS.Windows);
            RegisterFunction(nameof(DriveGetSerial), 1, DriveGetSerial, OS.Windows);
            RegisterFunction(nameof(DriveGetType), 1, 2, DriveGetType, 1);
            RegisterFunction(nameof(DriveSpaceFree), 1, DriveSpaceFree);
            RegisterFunction(nameof(DriveSpaceTotal), 1, DriveSpaceTotal);
            RegisterFunction(nameof(DriveStatus), 1, DriveStatus);
            RegisterFunction(nameof(MsgBox), 3, 5, MsgBox, Variant.Zero, Variant.Zero);
            RegisterFunction(nameof(EnvGet), 1, EnvGet);
            RegisterFunction(nameof(EnvSet), 1, 2, EnvSet, Variant.Default);
            RegisterFunction(nameof(EnvUpdate), 0, EnvUpdate);
            RegisterFunction(nameof(Execute), 1, Execute);
            RegisterFunction(nameof(Eval), 1, Eval);
            RegisterFunction(nameof(FileChangeDir), 1, FileChangeDir);
            RegisterFunction(nameof(FileClose), 1, FileClose);
            RegisterFunction(nameof(FileCopy), 2, 3, FileCopy, Variant.Zero);
            RegisterFunction(nameof(FileCreateNTFSLink), 2, 3, FileCreateNTFSLink, Variant.False);
            // RegisterFunction(nameof(FileCreateShortcut), , FileCreateShortcut,);
            RegisterFunction(nameof(FileDelete), 1, FileDelete);
            RegisterFunction(nameof(FileExists), 1, FileExists);
            RegisterFunction(nameof(FileFindFirstFile), 1, FileFindFirstFile);
            RegisterFunction(nameof(FileFindNextFile), 1, 2, FileFindNextFile, Variant.False);
            RegisterFunction(nameof(FileFlush), 1, FileFlush);
            RegisterFunction(nameof(FileGetAttrib), 1, FileGetAttrib);
            RegisterFunction(nameof(FileGetEncoding), 1, 2, FileGetEncoding, 1);
            RegisterFunction(nameof(FileGetLongName), 1, FileGetLongName);
            RegisterFunction(nameof(FileGetPos), 1, FileGetPos);
            // RegisterFunction(nameof(FileGetShortcut), , FileGetShortcut);
            RegisterFunction(nameof(FileGetShortName), 1, 2, FileGetShortName, Variant.Zero);
            RegisterFunction(nameof(FileGetSize), 1, FileGetSize);
            RegisterFunction(nameof(FileGetTime), 1, 3, FileGetTime, Variant.Zero, Variant.Zero);
            // RegisterFunction(nameof(FileGetVersion), , FileGetVersion);
            // RegisterFunction(nameof(FileInstall), , FileInstall);
            RegisterFunction(nameof(FileMove), 2, 3, FileMove, Variant.Zero);
            RegisterFunction(nameof(FileOpen), 1, 2, FileOpen, Variant.Zero);
            // RegisterFunction(nameof(FileOpenDialog), , FileOpenDialog);
            RegisterFunction(nameof(FileRead), 1, 2, FileRead, Variant.Default);
            RegisterFunction(nameof(FileReadLine), 1, 2, FileReadLine, 1);
            RegisterFunction(nameof(FileReadToArray), 1, FileReadToArray);
            RegisterFunction(nameof(FileRecycle), 1, FileRecycle);
            RegisterFunction(nameof(FileRecycleEmpty), 0, 1, FileRecycleEmpty, Variant.Default);
            // RegisterFunction(nameof(FileSaveDialog), , FileSaveDialog);
            // RegisterFunction(nameof(FileSelectFolder), , FileSelectFolder);
            RegisterFunction(nameof(FileSetAttrib), 2, 3 , FileSetAttrib, Variant.Zero);
            RegisterFunction(nameof(FileSetEnd), 1, FileSetEnd);
            RegisterFunction(nameof(FileSetPos), 3, FileSetPos);
            RegisterFunction(nameof(FileSetTime), 2, 4, FileSetTime, Variant.Zero, Variant.False);
            RegisterFunction(nameof(FileWrite), 2, FileWrite);
            RegisterFunction(nameof(FileWriteLine), 2, FileWriteLine);
            RegisterFunction(nameof(Hwnd), 1, Hwnd);
            RegisterFunction(nameof(InetClose), 1, InetClose);
            RegisterFunction(nameof(InetGet), 2, 4, InetGet, Variant.Zero, Variant.Zero);
            RegisterFunction(nameof(InetGetInfo), 0, 2, InetGetInfo, Variant.Null, Variant.Default);
            RegisterFunction(nameof(InetGetSize), 1, 2, InetGetSize, Variant.Zero);
            RegisterFunction(nameof(InetRead), 1, 2, InetRead, Variant.Zero);
            RegisterFunction(nameof(IniDelete), 2, 3, IniDelete, Variant.Default);
            RegisterFunction(nameof(IniRead), 3, 4, IniRead, Variant.Default);
            RegisterFunction(nameof(IniReadSection), 2, IniReadSection);
            RegisterFunction(nameof(IniReadSectionNames), 1, IniReadSectionNames);
            RegisterFunction(nameof(IniRenameSection), 3, 4, IniRenameSection, Variant.False);
            RegisterFunction(nameof(IniWrite), 4, IniWrite);
            RegisterFunction(nameof(IniWriteSection), 3, 4, IniWriteSection, 1);
            RegisterFunction(nameof(IsAdmin), 0, IsAdmin);
            RegisterFunction(nameof(IsArray), 1, IsArray);
            RegisterFunction(nameof(IsBinary), 1, IsBinary);
            RegisterFunction(nameof(IsBool), 1, IsBool);
            RegisterFunction(nameof(IsFloat), 1, IsFloat);
            RegisterFunction(nameof(IsFunc), 1, IsFunc);
            RegisterFunction(nameof(IsInt), 1, IsInt);
            RegisterFunction(nameof(IsPtr), 1, IsPtr);
            RegisterFunction(nameof(IsHwnd), 1, IsHwnd);
            RegisterFunction(nameof(IsKeyword), 1, IsKeyword);
            RegisterFunction(nameof(IsNumber), 1, IsNumber);
            // RegisterFunction(nameof(IsObj), 1, IsObj);
            RegisterFunction(nameof(IsString), 1, IsString);
            RegisterFunction(nameof(ObjCreate), 1, 4, ObjCreate, Variant.Default, Variant.Default, Variant.Default);
            // RegisterFunction(nameof(ObjCreateInterface), , ObjCreateInterface);
            // RegisterFunction(nameof(ObjEvent), , ObjEvent);
            RegisterFunction(nameof(ObjGet), 1, 3, ObjGet, Variant.Default, Variant.Default);
            RegisterFunction(nameof(ObjName), 1, 2, ObjName, 1);
            RegisterFunction(nameof(OnAutoItExitRegister), 1, OnAutoItExitRegister);
            RegisterFunction(nameof(OnAutoItExitUnRegister), 1, OnAutoItExitUnRegister);
            RegisterFunction(nameof(Ping), 1, 2, Ping, 4_000);
            RegisterFunction(nameof(ProcessClose), 1, ProcessClose);
            RegisterFunction(nameof(ProcessExists), 1, ProcessExists);
            RegisterFunction(nameof(ProcessGetStats), 1, 2, ProcessGetStats, Variant.Zero);
            RegisterFunction(nameof(ProcessList), 0, 1, ProcessList, Variant.Default);
            RegisterFunction(nameof(ProcessSetPriority), 2, ProcessSetPriority);
            RegisterFunction(nameof(ProcessWait), 1, 2, ProcessWait, Variant.Zero);
            RegisterFunction(nameof(ProcessWaitClose), 1, 2, ProcessWaitClose, Variant.Zero);
            RegisterFunction("Ptr", 1, Hwnd);
            RegisterFunction(nameof(Random), 0, 3, Random, Variant.Zero, 1, Variant.False);
            RegisterFunction(nameof(RegDelete), 1, 2, RegDelete, OS.Windows, Variant.Default);
            RegisterFunction(nameof(RegEnumKey), 2, RegEnumKey, OS.Windows);
            RegisterFunction(nameof(RegEnumVal), 2, RegEnumVal, OS.Windows);
            RegisterFunction(nameof(RegRead), 2, RegRead, OS.Windows);
            RegisterFunction(nameof(RegWrite), 1, 4, RegWrite, OS.Windows, Variant.Default, Variant.Default, Variant.Default);
            RegisterFunction(nameof(Run), 1, 4, Run, Variant.EmptyString, Variant.Default, Variant.Default);
            RegisterFunction(nameof(RunAs), 5, 8, RunAs, OS.Windows, Variant.Default, Variant.Default, Variant.Default);
            RegisterFunction(nameof(RunWait), 1, 4, RunWait, Variant.EmptyString, Variant.Default, Variant.Default);
            RegisterFunction(nameof(RunAsWait), 5, 8, RunAsWait, OS.Windows, Variant.Default, Variant.Default, Variant.Default);
            RegisterFunction(nameof(Shutdown), 1, Shutdown);
            RegisterFunction(nameof(SRandom), 1, SRandom);
            RegisterFunction(nameof(ShellExecute), 1, 5, ShellExecute, Variant.EmptyString, Variant.EmptyString, Variant.Default, Variant.Default);
            RegisterFunction(nameof(ShellExecuteWait), 1, 5, ShellExecuteWait, Variant.EmptyString, Variant.EmptyString, Variant.Default, Variant.Default);
            RegisterFunction(nameof(SoundPlay), 1, 2, SoundPlay, Variant.Zero);
            // RegisterFunction(nameof(SoundSetWaveVolume), 1, SoundSetWaveVolume);
            RegisterFunction(nameof(StringAddCR), 1, StringAddCR);
            RegisterFunction(nameof(StringCompare), 2, 3, StringCompare, Variant.Zero);
            RegisterFunction(nameof(StringFormat), 1, 33, StringFormat);
            RegisterFunction(nameof(StringFromASCIIArray), 1, 4, StringFromASCIIArray, Variant.Zero, -1, Variant.Zero);
            RegisterFunction(nameof(StringInStr), 2, 6, StringInStr, Variant.Zero, 1, 1, Variant.Default);
            RegisterFunction(nameof(StringLeft), 2, StringLeft);
            RegisterFunction(nameof(StringLen), 1, StringLen);
            RegisterFunction(nameof(StringLower), 1, StringLower);
            RegisterFunction(nameof(StringMid), 2, 3, StringMid, -1);
            RegisterFunction(nameof(StringRegExp), 2, 4, StringRegExp, Variant.Zero, 1);
            RegisterFunction(nameof(StringRegExpReplace ), 3, 4, StringRegExpReplace, Variant.Zero);
            RegisterFunction(nameof(StringReplace), 3, 5, StringReplace, Variant.Zero, Variant.Zero);
            RegisterFunction(nameof(StringReverse), 1, 2, StringReverse, Variant.Zero);
            RegisterFunction(nameof(StringRight), 2, StringRight);
            RegisterFunction(nameof(StringSplit), 2, 3, StringSplit, Variant.Zero);
            RegisterFunction(nameof(StringStripCR), 1, StringStripCR);
            RegisterFunction(nameof(StringStripWS), 2, StringStripWS);
            RegisterFunction(nameof(StringToASCIIArray), 1, 4, StringToASCIIArray, Variant.Zero, Variant.Default, Variant.Zero);
            RegisterFunction(nameof(StringTrimLeft), 2, StringTrimLeft);
            RegisterFunction(nameof(StringTrimRight), 2, StringTrimRight);
            RegisterFunction(nameof(StringUpper), 1, StringUpper);
            RegisterFunction("StringIsFloat", 1, IsFloat);
            RegisterFunction("StringIsInt", 1, IsInt);
            RegisterFunction(nameof(StringIsDigit), 1, StringIsDigit);
            RegisterFunction(nameof(StringIsAlNum), 1, StringIsAlNum);
            RegisterFunction(nameof(StringIsAlpha), 1, StringIsAlpha);
            RegisterFunction(nameof(StringIsASCII), 1, StringIsASCII);
            RegisterFunction(nameof(StringIsLower), 1, StringIsLower);
            RegisterFunction(nameof(StringIsSpace), 1, StringIsSpace);
            RegisterFunction(nameof(StringIsUpper), 1, StringIsUpper);
            RegisterFunction(nameof(StringIsXDigit), 1, StringIsXDigit);
            RegisterFunction(nameof(IsDeclared), 1, IsDeclared);
            RegisterFunction(nameof(FuncName), 1, FuncName);
            RegisterFunction(nameof(SetError), 1, 3, SetError);
            RegisterFunction(nameof(SetExtended), 1, 2, SetExtended);
            RegisterFunction(nameof(Sleep), 1, Sleep);
            RegisterFunction(nameof(TCPAccept), 1, TCPAccept);
            RegisterFunction(nameof(TCPCloseSocket), 1, TCPCloseSocket);
            RegisterFunction(nameof(TCPConnect), 2, TCPConnect);
            RegisterFunction(nameof(TCPListen), 2, 3, TCPListen, Variant.Default);
            RegisterFunction(nameof(TCPNameToIP), 1, TCPNameToIP);
            RegisterFunction(nameof(TCPRecv), 2, 3, TCPRecv, Variant.Zero);
            RegisterFunction(nameof(TCPSend), 2, TCPSend);
            RegisterFunction(nameof(TCPShutdown), 0, TCPShutdown);
            RegisterFunction(nameof(TCPStartup), 0, TCPStartup);
            RegisterFunction(nameof(TimerInit), 0, TimerInit);
            RegisterFunction(nameof(TimerDiff), 1, TimerDiff);
            RegisterFunction("UDPBind", 2, UDPListen);
            RegisterFunction(nameof(UDPListen), 2, UDPListen);
            RegisterFunction(nameof(UDPCloseSocket), 1, UDPCloseSocket);
            RegisterFunction(nameof(UDPOpen), 2, 3, UDPOpen, Variant.Zero);
            RegisterFunction(nameof(UDPRecv), 2, 3, UDPRecv, Variant.Zero);
            RegisterFunction(nameof(UDPSend), 2, UDPSend);
            RegisterFunction(nameof(UDPShutdown), 0, UDPShutdown);
            RegisterFunction(nameof(UDPStartup), 0, UDPStartup);
            RegisterFunction(nameof(UBound), 1, 2, UBound, 1);
            RegisterFunction(nameof(VarGetType), 1, VarGetType);
        }

        #region FRAMEWORK FUNCTIONS
        #region A...

        internal static FunctionReturnValue AdlibRegister(CallFrame frame, Variant[] args)
        {
            if (!args[0].IsFunction(out ScriptFunction? function))
                function = frame.Interpreter.ScriptScanner.TryResolveFunction(args[0].ToString());

            int interval = (int)args[1];

            if (function is { })
                frame.Interpreter.TimerManager.RegisterOrUpdateTimer(function, interval);

            return Variant.FromBoolean(function is { });
        }

        internal static FunctionReturnValue AdlibUnRegister(CallFrame frame, Variant[] args)
        {
            ScriptFunction? function = null;

            if (args[0].IsDefault)
                function = frame.Interpreter.TimerManager.MostRecentRegistration;
            else if (!args[0].IsFunction(out function))
                function = frame.Interpreter.ScriptScanner.TryResolveFunction(args[0].ToString());

            if (function is { })
                frame.Interpreter.TimerManager.UnregisterTimer(function);

            return (Variant)frame.Interpreter.TimerManager.ActiveTimerCount;
        }

        internal static FunctionReturnValue Abs(CallFrame frame, Variant[] args) => (Variant)Math.Abs(args[0].ToNumber());

        internal static FunctionReturnValue ACos(CallFrame frame, Variant[] args) => (Variant)Math.Acos((double)args[0].ToNumber());

        internal static FunctionReturnValue Asc(CallFrame frame, Variant[] args) => (Variant)(byte)args[0].ToString().FirstOrDefault();

        internal static FunctionReturnValue AscW(CallFrame frame, Variant[] args) => (Variant)(int)args[0].ToString().FirstOrDefault();

        internal static FunctionReturnValue ASin(CallFrame frame, Variant[] args) => (Variant)Math.Asin((double)args[0].ToNumber());

        internal static FunctionReturnValue Assign(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 =>
            {
                string name = args[0].ToString();
                Variant data = args[1];
                AssignFlags flags = (AssignFlags)(int)args[2];
                VariableScope scope = flags.HasFlag(AssignFlags.ForceGlobal) ? au3.VariableResolver.GlobalRoot : au3.VariableResolver;
                Variable variable;

                if (flags.HasFlag(AssignFlags.ForceGlobal) && flags.HasFlag(AssignFlags.ForceLocal))
                    return Variant.False;
                else if (scope.TryGetVariable(name, flags.HasFlag(AssignFlags.ForceLocal) ? VariableSearchScope.Local : VariableSearchScope.Global, out variable!))
                {
                    if (variable.IsConst || flags.HasFlag(AssignFlags.ExistFail))
                        return Variant.False;
                }
                else if (flags.HasFlag(AssignFlags.Create))
                    variable = scope.CreateVariable(au3.CurrentLocation, name, false);
                else
                    return Variant.False;

                variable.Value = data;

                return Variant.True;
            });

        internal static FunctionReturnValue ATan(CallFrame frame, Variant[] args) => (Variant)Math.Atan((double)args[0].ToNumber());

        internal static FunctionReturnValue AutoItWinGetTitle(CallFrame frame, Variant[] args) => (Variant)Console.Title;

        internal static FunctionReturnValue AutoItWinSetTitle(CallFrame frame, Variant[] args)
        {
            Console.Title = args[0].ToString();

            return Variant.Null;
        }

        #endregion
        #region B...

        internal static FunctionReturnValue Beep(CallFrame frame, Variant[] args)
        {
            Console.Beep((int)args[0], (int)args[1]);

            return Variant.True;
        }

        internal static FunctionReturnValue Binary(CallFrame frame, Variant[] args) => (Variant)args[0].ToBinary();

        internal static FunctionReturnValue BinaryLen(CallFrame frame, Variant[] args) => (Variant)args[0].ToBinary().Length;

        internal static FunctionReturnValue BinaryMid(CallFrame frame, Variant[] args)
        {
            byte[] bytes = args[0].ToBinary();
            int start = (int)args[1] - 1;
            int count = (int)args[2];

            if (start < 0 || start >= bytes.Length)
                return Variant.EmptyBinary;
            else if (args[2].IsDefault)
                return (Variant)bytes[start..];
            else if (start + count > bytes.Length)
                return Variant.EmptyBinary;
            else
                return (Variant)bytes[start..(start + count)];
        }

        internal static FunctionReturnValue BinaryToString(CallFrame frame, Variant[] args) => (Variant)From.Bytes(args[0].ToBinary()).ToString((int)args[1] switch
        {
            1 => Encoding.GetEncoding(1252),
            2 => Encoding.Unicode,
            3 => Encoding.BigEndianUnicode,
            4 => Encoding.UTF8,
            _ => BytewiseEncoding.Instance,
        });

        internal static FunctionReturnValue BitAND(CallFrame frame, Variant[] args) => args.Aggregate(Variant.BitwiseAnd);

        internal static FunctionReturnValue BitOR(CallFrame frame, Variant[] args) => args.Aggregate(Variant.BitwiseOr);

        internal static FunctionReturnValue BitNOT(CallFrame frame, Variant[] args) => ~args[0];

        internal static FunctionReturnValue BitXOR(CallFrame frame, Variant[] args) => args.Aggregate(Variant.BitwiseOr);

        internal static FunctionReturnValue BitShift(CallFrame frame, Variant[] args) => args[0] >> (int)args[1];

        internal static FunctionReturnValue BitRotate(CallFrame frame, Variant[] args)
        {
            Variant rotate(int size)
            {
                int amount = (int)args[1];
                long value = (long)args[0];
                long rotmask = -1L >> (64 - size);
                long @static = value & ~rotmask;
                long variable = value & rotmask;
                long rotated = (variable << amount) | (variable >> (size - amount));

                rotated &= rotmask;
                rotated |= @static;

                return rotated;
            }

            return args[2].ToString().ToUpperInvariant() switch
            {
                "B" => rotate(8),
                "W" => rotate(16),
                "D" => rotate(32),
                "Q" => rotate(64),
                _ => FunctionReturnValue.Error(-1),
            };
        }

        internal static FunctionReturnValue BlockInput(CallFrame frame, Variant[] args) => NativeInterop.DoPlatformDependent<Variant>(() => NativeInterop.BlockInput(args[0].ToBoolean()), () => false);

        #endregion
        #region C...

        internal static FunctionReturnValue Call(CallFrame frame, Variant[] args)
        {
            Variant[] call_args = frame.PassedArguments.Length == 2 &&
                                  args[1] is { Type: VariantType.Array } arr &&
                                  arr.TryGetIndexed(frame.Interpreter, 0, out Variant caa) &&
                                  caa.ToString().Equals("CallArgArray", StringComparison.InvariantCultureIgnoreCase) ? arr.ToArray(frame.Interpreter) : args;

            call_args = call_args[1..];

            if (!args[0].IsFunction(out ScriptFunction? func))
                func = frame.Interpreter.ScriptScanner.TryResolveFunction(args[0].ToString());

            if (func is null)
                return FunctionReturnValue.Error(0xDEAD, 0xBEEF);
            else if (call_args.Length > func.ParameterCount.MaximumCount)
                Array.Resize(ref call_args, func.ParameterCount.MaximumCount);

            FunctionReturnValue result = frame.Call(func, call_args);

            if (result.IsFatal(out _))
                result = FunctionReturnValue.Error(0xDEAD, 0xBEEF);

            return result;
        }

        internal static unsafe FunctionReturnValue CDTray(CallFrame frame, Variant[] args)
        {
            try
            {
                return NativeInterop.DoPlatformDependent<Variant>(delegate
                {
                    int dwbytes = 0;
                    void* cdrom = NativeInterop.CreateFile($"\\\\.\\{args[0]}", 0xc0000000u, 0, null, 3, 0, null);

                    return args[1].ToString().ToLower() switch
                    {
                        "open" => NativeInterop.DeviceIoControl(cdrom, 0x2d4808, null, 0, null, 0, &dwbytes, null),
                        "closed" => NativeInterop.DeviceIoControl(cdrom, 0x2d480c, null, 0, null, 0, &dwbytes, null),
                        _ => false
                    };
                }, delegate
                {
                    int cdrom = NativeInterop.Linux__open(args[0].ToString(), 0x0800);

                    return args[1].ToString().ToLower() switch
                    {
                        "open" or "closed" => NativeInterop.Linux__ioctl(cdrom, 0x5309, __arglist()), // TODO ?
                        _ => false
                    };
                });
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue Ceiling(CallFrame frame, Variant[] args) => (Variant)Math.Ceiling(args[0].ToNumber());

        internal static FunctionReturnValue Chr(CallFrame frame, Variant[] args) => (Variant)((char)(int)args[0]).ToString();

        internal static FunctionReturnValue ChrW(CallFrame frame, Variant[] args) => (Variant)((char)(byte)args[0]).ToString();

        internal static FunctionReturnValue ConsoleWriteError(CallFrame frame, Variant[] args) =>
            frame.Interpreter.Telemetry.Measure<FunctionReturnValue>(TelemetryCategory.ScriptConsoleOut, delegate
            {
                string s = args[0].ToString();

                Console.Error.Write(s);

                return Variant.FromNumber(s.Length);
            });

        internal static FunctionReturnValue ConsoleWrite(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            frame.Print(s);

            return Variant.FromNumber(s.Length);
        }

        internal static FunctionReturnValue ConsoleRead(CallFrame frame, Variant[] args) =>
            frame.Interpreter.Telemetry.Measure<FunctionReturnValue>(TelemetryCategory.ScriptConsoleIn, delegate
            {
                bool peek = args[0].ToBoolean();
                bool binary = args[1].ToBoolean();
                int input = peek ? Console.In.Peek() : Console.In.Read();

                if (input < 0)
                    return FunctionReturnValue.Error(1);
                else if (binary)
                    return Variant.FromBinary(new[] { (byte)(input & 0xff) });
                else
                    return Variant.FromString(new string(new[] { (char)input }));
            });

        internal static FunctionReturnValue Cos(CallFrame frame, Variant[] args) => (Variant)Math.Cos((double)args[0].ToNumber());

        #endregion
        #region D...

        internal static FunctionReturnValue Dec(CallFrame frame, Variant[] args) => (Variant)(long.TryParse(args[0].ToString(), NumberStyles.HexNumber, null, out long l) ? l : 0L);

        #endregion
        #region DIR...

        internal static FunctionReturnValue DirCreate(CallFrame frame, Variant[] args)
        {
            try
            {
                DirectoryInfo dir = new(args[0].ToString());

                if (!dir.Exists)
                    dir.Create();


                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue DirCopy(CallFrame frame, Variant[] args)
        {
            try
            {
                static void copy_rec(DirectoryInfo source, DirectoryInfo target, bool overwrite)
                {
                    foreach (DirectoryInfo dir in source.GetDirectories())
                        copy_rec(dir, target.CreateSubdirectory(dir.Name), overwrite);

                    foreach (FileInfo src in source.GetFiles())
                    {
                        string dest = Path.Combine(target.FullName, src.Name);

                        if (!File.Exists(dest) || overwrite)
                            src.CopyTo(dest, overwrite);
                    }
                }

                copy_rec(new DirectoryInfo(args[0].ToString()), new DirectoryInfo(args[1].ToString()), args[2].ToBoolean());

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue DirMove(CallFrame frame, Variant[] args)
        {
            try
            {
                static void move_rec(DirectoryInfo source, DirectoryInfo target, bool overwrite)
                {
                    foreach (DirectoryInfo dir in source.GetDirectories())
                        move_rec(dir, target.CreateSubdirectory(dir.Name), overwrite);

                    foreach (FileInfo src in source.GetFiles())
                    {
                        string dest = Path.Combine(target.FullName, src.Name);

                        if (!File.Exists(dest) || overwrite)
                            src.MoveTo(dest, overwrite);
                    }

                    source.Delete();
                }

                move_rec(new DirectoryInfo(args[0].ToString()), new DirectoryInfo(args[1].ToString()), args[2].ToBoolean());

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue DirRemove(CallFrame frame, Variant[] args)
        {
            try
            {
                static void delete_rec(DirectoryInfo dir, bool recursive)
                {
                    if (recursive)
                    {
                        foreach (DirectoryInfo sub in dir.GetDirectories())
                            delete_rec(sub, recursive);

                        foreach (FileInfo src in dir.GetFiles())
                            src.Delete();
                    }

                    dir.Delete();
                }

                delete_rec(new DirectoryInfo(args[0].ToString()), args[2].ToBoolean());

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue DirGetSize(CallFrame frame, Variant[] args)
        {
            try
            {
                long file_count = 0;
                long dir_count = 0;
                long total_size = 0;
                void count_rec(DirectoryInfo dir, bool recurse)
                {
                    if (recurse)
                        foreach (DirectoryInfo sub in dir.GetDirectories())
                        {
                            ++dir_count;

                            count_rec(sub, recurse);
                        }

                    foreach (FileInfo file in dir.GetFiles())
                    {
                        ++file_count;
                        total_size += file.Length;
                    }
                }

                count_rec(new DirectoryInfo(args[0].ToString()), args[2].ToNumber() is 0d or 1d);

                return args[2].ToNumber() is 1 ? Variant.FromArray(frame.Interpreter, total_size, file_count, dir_count) : total_size;
            }
            catch
            {
                return FunctionReturnValue.Error(-1m, 1, 0);
            }
        }

        #endregion
        #region DLL...

        private static FunctionReturnValue InternalDllCall(Interpreter interpreter, nint funcptr, string raw_signature, Variant[] args)
        {
            try
            {
                if (interpreter.ParserProvider.DLLStructParser.TryParse(raw_signature, out ParserResult<SIGNATURE>? result) &&
                    DelegateBuilder.Instance.CreateNativeDelegateType(result!.ParsedValue) is NativeDelegateWrapper @delegate)
                {
                    return @delegate.CallCPPfromAutoit(funcptr, interpreter, args);
                }
            }
            catch (Exception ex)
            {
                MainProgram.PrintDebugMessage(ex.ToString());
            }

            return FunctionReturnValue.Error(3);
        }

        internal static FunctionReturnValue DllCall(CallFrame frame, Variant[] args)
        {
            if (!args[0].TryResolveHandle(frame.Interpreter, out LibraryHandle? dllhandle))
                try
                {
                    dllhandle = new LibraryHandle(args[0].ToString());
                }
                catch
                {
                }

            if (dllhandle?.IsLoaded is false)
                dllhandle.LoadLibrary();

            if (dllhandle?.IsLoaded is null or false)
                return FunctionReturnValue.Error(1);

            nint funcptr = NativeInterop.DoPlatformDependent<Func<nint, string, nint>>(
                NativeInterop.GetProcAddress,
                NativeInterop.Linux__dlsym,
                NativeInterop.MacOS__dlsym
            )(dllhandle.Handle, args[2].ToString());

            if (funcptr == default)
                return FunctionReturnValue.Error(3);

            int argc = frame.PassedArguments.Length - 3;

            if ((argc % 2) != 0)
                return FunctionReturnValue.Error(4);

            Variant[] arguments = args.Skip(3).Take(argc).Where((_, i) => (i % 2) == 1).ToArray();
            string raw_signature = args.Skip(3).Take(argc).Where((_, i) => (i % 2) == 0).Prepend(args[1]).StringJoin(", ");

            return InternalDllCall(frame.Interpreter, funcptr, raw_signature, arguments);
        }

        // TODO : DllGetAddress ?

        internal static FunctionReturnValue DllCallAddress(CallFrame frame, Variant[] args)
        {
            int argc = frame.PassedArguments.Length - 2;

            if ((argc % 2) != 0)
                return FunctionReturnValue.Error(4);

            Variant[] arguments = args.Skip(2).Take(argc).Where((_, i) => (i % 2) == 1).ToArray();
            string raw_signature = args.Skip(2).Take(argc).Where((_, i) => (i % 2) == 0).Prepend(args[0]).StringJoin(", ");
            nint funcptr = (nint)(long)args[1];

            // TODO ?

            return InternalDllCall(frame.Interpreter, funcptr, raw_signature, arguments);
        }

        internal static FunctionReturnValue DllCallbackFree(CallFrame frame, Variant[] args)
        {
            if (args[0].TryResolveHandle(frame.Interpreter, out UserFunctionCallback? _))
                frame.Interpreter.GlobalObjectStorage.Delete(args[0]);

            return Variant.Zero;
        }

        internal static FunctionReturnValue DllCallbackGetPtr(CallFrame frame, Variant[] args)
        {
            if (args[0].TryResolveHandle(frame.Interpreter, out UserFunctionCallback? callback))
                return (Variant)(long)callback.FunctionPointer;

            return Variant.Zero;
        }

        internal static FunctionReturnValue DllCallbackRegister(CallFrame frame, Variant[] args)
        {
            try
            {
                if (!args[0].IsFunction(out ScriptFunction? function))
                    function = frame.Interpreter.ScriptScanner.TryResolveFunction(args[0].ToString());

                if (function is { })
                {
                    var callback = UserFunctionCallback.CreateNativeCallback(function, frame.Interpreter);
                    string raw_signature = args[1].ToString() + ", " + args[2].ToString().Replace(';', ',');

                    if (frame.Interpreter.ParserProvider.DLLStructParser.TryParse(raw_signature, out ParserResult<SIGNATURE>? result) &&
                        DelegateBuilder.Instance.CreateUserFunctionCallback(result!.ParsedValue, callback) is UserFunctionCallback @delegate)
                    {
                        return frame.Interpreter.GlobalObjectStorage.Store(@delegate);
                    }
                }
            }
            catch (Exception ex)
            {
                MainProgram.PrintDebugMessage(ex.ToString());
            }

            return Variant.False;
        }

        internal static FunctionReturnValue DllClose(CallFrame frame, Variant[] args)
        {
            if (args[0].TryResolveHandle(frame.Interpreter, out LibraryHandle? _))
                frame.Interpreter.GlobalObjectStorage.Delete(args[0]);

            return Variant.Zero;
        }

        internal static FunctionReturnValue DllOpen(CallFrame frame, Variant[] args)
        {
            try
            {
                return frame.Interpreter.GlobalObjectStorage.Store(new LibraryHandle(args[0].ToString()));
            }
            catch
            {
            }

            return Variant.FromNumber(-1);
        }

        // internal static FunctionReturnValue DllStructCreate(CallFrame frame, Variant[] args)
        // {
        //     string struct_type = args[0].ToString();
        //     long pointer = args[1]
        // 
        // }
        // 
        // internal static FunctionReturnValue DllStructGetData(CallFrame frame, Variant[] args)
        // {
        // 
        // }
        // 
        // internal static FunctionReturnValue DllStructGetPtr(CallFrame frame, Variant[] args)
        // {
        // 
        // }
        // 
        // internal static FunctionReturnValue DllStructGetSize(CallFrame frame, Variant[] args)
        // {
        // 
        // }
        // 
        // internal static FunctionReturnValue DllStructSetData(CallFrame frame, Variant[] args)
        // {
        // 
        // }

        #endregion
        #region DRIVE...

        internal static FunctionReturnValue DriveGetDrive(CallFrame frame, Variant[] args)
        {
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                DriveType? type = args[0].ToString().ToLowerInvariant() switch
                {
                    "cdrom" => DriveType.CDRom,
                    "removable" => DriveType.Removable,
                    "fixed" => DriveType.Fixed,
                    "network" => DriveType.Network,
                    "ramdisk" => DriveType.Ram,
                    "unknown" => DriveType.Unknown,
                    _ => null,
                };

                if (type is DriveType dt)
                    drives = drives.Where(d => d.DriveType == dt).ToArray();

                return Variant.FromArray(frame.Interpreter, drives.Select(d => d.Name.TrimEnd('/', '\\')).Prepend(drives.Length.ToString()).ToArray(Variant.FromString));
            }
            catch
            {
                return FunctionReturnValue.Error(Variant.EmptyString, 1, Variant.Zero);
            }
        }

        internal static FunctionReturnValue DriveGetFileSystem(CallFrame frame, Variant[] args)
        {
            if (GetDriveByPath(args[0].ToString())?.DriveFormat is string format)
                return Variant.FromString(format);
            else
                return FunctionReturnValue.Error(Variant.EmptyString, 1, Variant.Zero);
        }

        internal static FunctionReturnValue DriveGetLabel(CallFrame frame, Variant[] args)
        {
            if (GetDriveByPath(args[0].ToString())?.VolumeLabel is string label)
                return Variant.FromString(label);
            else
                return FunctionReturnValue.Error(Variant.EmptyString, 1, Variant.Zero);
        }

        internal static FunctionReturnValue DriveSetLabel(CallFrame frame, Variant[] args)
        {
            try
            {
                if (GetDriveByPath(args[0].ToString()) is DriveInfo info)
                    info.VolumeLabel = args[1].ToString();

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue DriveGetSerial(CallFrame frame, Variant[] args)
        {
            StringBuilder sb0 = new(256), sb1 = new(256);
            NativeInterop.GetVolumeInformation(args[0].ToString(), sb0, sb0.Capacity, out uint serial, out _, out _, sb1, sb1.Capacity);

            if (serial != 0)
                return (Variant)serial;
            else
                return FunctionReturnValue.Error(1);
        }

        internal static FunctionReturnValue DriveGetType(CallFrame frame, Variant[] args)
        {
            int mode = (int)args[1];

            if (mode == 1 && GetDriveByPath(args[0].ToString())?.DriveType is DriveType type)
                return Variant.FromString(type switch
                {
                    DriveType.Removable or DriveType.Fixed or DriveType.Network => type.ToString(),
                    DriveType.CDRom => "CDROM",
                    DriveType.Ram => "RAMDisk",
                    _ => "Unknown",
                });
            else
                return FunctionReturnValue.Error(Variant.EmptyString, 1, Variant.Zero);
        }

        internal static FunctionReturnValue DriveSpaceFree(CallFrame frame, Variant[] args)
        {
            if (GetDriveByPath(args[0].ToString()) is DriveInfo drive)
                return Variant.FromNumber(drive.TotalFreeSpace / 1048576d);
            else
                return FunctionReturnValue.Error(1);
        }

        internal static FunctionReturnValue DriveSpaceTotal(CallFrame frame, Variant[] args)
        {
            if (GetDriveByPath(args[0].ToString()) is DriveInfo drive)
                return Variant.FromNumber(drive.TotalSize / 1048576d);
            else
                return FunctionReturnValue.Error(1);
        }

        internal static FunctionReturnValue DriveStatus(CallFrame frame, Variant[] args)
        {
            if (GetDriveByPath(args[0].ToString()) is DriveInfo drive)
                return Variant.FromString(drive.IsReady ? "READY" : "NOTREADY");

            return Variant.FromString("INVALID");
        }

        internal static FunctionReturnValue DriveMapAdd(CallFrame frame, Variant[] args)
        {
            int flags = (int)args[2];
            string user = args[3].ToString();
            string pass = args[4].ToString();
            string cmd = $"net use \"{args[0]}\" \"{args[1]}\"";

            if (flags.HasFlag(1))
                cmd += " /persistent:Yes";

            if (flags.HasFlag(8))
                cmd += $" /user:{user} \"{pass}\"";

            (string output, int code) = NativeInterop.Exec(cmd);
            bool any = args[0].ToString() == "*";

            if (any && output.Match(REGEX_DRIVE_ADD, out Match match))
                return (Variant)match.Groups["letter"].Value;
            else if (any)
                return FunctionReturnValue.Error("", code, code);
            else
                return FunctionReturnValue.Error(code == 0, code, code);
        }

        internal static FunctionReturnValue DriveMapDel(CallFrame frame, Variant[] args) => (Variant)NativeInterop.Exec($"net use /D \"{args[0]}\"").code;

        internal static FunctionReturnValue DriveMapGet(CallFrame frame, Variant[] args)
        {
            if (NativeInterop.Exec($"net use \"{args[0]}\"").stdout.Match(REGEX_DRIVE_GET, out Match match))
                return (Variant)match.Groups["target"].Value;
            else
                return FunctionReturnValue.Error(Variant.EmptyString, 1, 0);
        }

        #endregion
        #region E...

        internal static FunctionReturnValue EnvGet(CallFrame frame, Variant[] args) => Variant.FromString(Environment.GetEnvironmentVariable(args[0].ToString()));

        internal static FunctionReturnValue EnvSet(CallFrame frame, Variant[] args)
        {
            Environment.SetEnvironmentVariable(args[0].ToString(), args[1].IsDefault || args[1].IsNull ? null : args[1].ToString(), EnvironmentVariableTarget.Process);

            return Variant.True;
        }

        internal static FunctionReturnValue EnvUpdate(CallFrame frame, Variant[] args) => Variant.False;

        internal static FunctionReturnValue Execute(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match(err => err, au3 => au3.ProcessAsVariant(args[0].ToString()));

        internal static FunctionReturnValue Eval(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 =>
            {
                if (au3.VariableResolver.TryGetVariable(args[0].ToString(), VariableSearchScope.Global, out Variable? variable))
                    return variable.Value;

                return InterpreterError.WellKnown(au3.CurrentLocation, "error.undeclared_variable", args[0]);
            });

        internal static FunctionReturnValue Exp(CallFrame frame, Variant[] args) => (Variant)Math.Exp((double)args[0].ToNumber());

        #endregion
        #region F...

        internal static FunctionReturnValue Floor(CallFrame frame, Variant[] args) => (Variant)Math.Floor(args[0].ToNumber());

        internal static FunctionReturnValue FuncName(CallFrame frame, Variant[] args)
        {
            if (args[0].IsFunction(out ScriptFunction? func))
                return (Variant)func.Name;
            else
                return FunctionReturnValue.Error("", 1, 0);
        }

        #endregion
        #region FILE...

        internal static FunctionReturnValue FileChangeDir(CallFrame frame, Variant[] args)
        {
            string curr = Directory.GetCurrentDirectory();

            Directory.SetCurrentDirectory(args[0].ToString());

            return Variant.FromBoolean(Directory.GetCurrentDirectory() != curr);
        }

        internal static FunctionReturnValue FileClose(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    handle.Dispose();
                    frame.Interpreter.GlobalObjectStorage.Delete(args[0]);

                    return Variant.True;
                }
            }
            catch
            {
            }

            return Variant.False;
        }

        internal static FunctionReturnValue FileCopy(CallFrame frame, Variant[] args)
        {
            try
            {
                FileInfo src = new(args[0].ToString());
                FileInfo dst = new(args[1].ToString());
                long flags = (long)args[2];

                if ((flags & 8) != 0 && dst.Directory is { Exists: false } dir)
                    dir.Create();

                src.CopyTo(dst.FullName, (flags & 1) != 0);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue FileCreateNTFSLink(CallFrame frame, Variant[] args)
        {
            try
            {
                FileSystemInfo target = FileSystemExtensions.GetSystemInfo(args[0].ToString());
                FileSystemInfo linkname = FileSystemExtensions.GetSystemInfo(args[1].ToString());
                bool overwrite = (bool)args[2];

                if (overwrite && linkname.Exists)
                    linkname.Delete();

                return NativeInterop.DoPlatformDependent<Variant>(
                    () => FileSystemExtensions.CreateNTFSHardLink(linkname.FullName, target.FullName),
                    () => NativeInterop.Exec($"ln -f \"{linkname.FullName}\" \"{target.FullName}\"").code == 0
                );
            }
            catch
            {
                return Variant.False;
            }
        }

        // internal static FunctionReturnValue FileCreateShortcut(CallFrame frame, Variant[] args)
        // {
        // }

        internal static FunctionReturnValue FileDelete(CallFrame frame, Variant[] args)
        {
            string path = args[0].ToString();

            try
            {
                foreach (FileSystemInfo entry in FileSystemExtensions.ResolveWildCards(path))
                    if (entry.Exists)
                        if (entry is DirectoryInfo dir)
                            dir.Delete(true);
                        else
                            entry.Delete();

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue FileExists(CallFrame frame, Variant[] args) =>
            Variant.FromBoolean(File.Exists(args[0].ToString()) || Directory.Exists(args[0].ToString()));

        internal static FunctionReturnValue FileFindFirstFile(CallFrame frame, Variant[] args)
        {
            try
            {
                return frame.Interpreter.GlobalObjectStorage.Store(new FileSearchHandle(FileSystemExtensions.ResolveWildCards(args[0].ToString())));
            }
            catch
            {
                return (Variant)(-1);
            }
        }

        internal static FunctionReturnValue FileFindNextFile(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileSearchHandle? handle))
                    if (handle.Enumerator.MoveNext())
                    {
                        FileSystemInfo fsi = handle.Enumerator.Current;
                        bool extended = (bool)args[1];

                        return FunctionReturnValue.Success(fsi.FullName, extended ? (Variant)GetAttributeString(fsi) : fsi.Attributes.HasFlag(FileAttributes.Directory));
                    }
                    else
                        frame.Interpreter.GlobalObjectStorage.Delete((int)args[0]);
            }
            catch
            {
            }

            return FunctionReturnValue.Error(1);
        }

        internal static FunctionReturnValue FileFlush(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    handle.StreamWriter?.Flush();
                    handle.FileStream.Flush();

                    return Variant.True;
                }
            }
            catch
            {
            }

            return Variant.False;
        }

        internal static FunctionReturnValue FileGetAttrib(CallFrame frame, Variant[] args)
        {
            try
            {
                return (Variant)GetAttributeString(FileSystemExtensions.GetSystemInfo(args[0].ToString()));
            }
            catch
            {
                return FunctionReturnValue.Error("", 1, 0);
            }
        }

        internal static FunctionReturnValue FileGetEncoding(CallFrame frame, Variant[] args)
        {
            try
            {
                Encoding enc;

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    if (handle.StreamReader.Peek() >= 0)
                    {
                        handle.StreamReader.Read();
                        handle.FileStream.Position--;
                    }

                    enc = handle.StreamReader.CurrentEncoding;
                }
                else
                {
                    using FileStream fs = File.OpenRead(args[0].ToString());
                    using StreamReader rd = new(fs, Encoding.Default);

                    if (rd.Peek() >= 0)
                        rd.Read();

                    enc = rd.CurrentEncoding;
                }

                if (new Dictionary<int, FileOpenFlags>
                {
                    [new UnicodeEncoding(false, false).CodePage] = FileOpenFlags.FO_UTF16_LE_NOBOM,
                    [new UnicodeEncoding(true, false).CodePage] = FileOpenFlags.FO_UTF16_BE_NOBOM,
                    [new UTF8Encoding(true).CodePage] = FileOpenFlags.FO_UTF8,
                    [new UTF8Encoding(false).CodePage] = FileOpenFlags.FO_UTF8_NOBOM,
                    [Encoding.UTF8.CodePage] = FileOpenFlags.FO_UTF8,
                    [Encoding.Unicode.CodePage] = FileOpenFlags.FO_UTF16_LE,
                    [Encoding.BigEndianUnicode.CodePage] = FileOpenFlags.FO_UTF16_BE,
                    [1252] = FileOpenFlags.FO_ANSI,
                }.TryGetValue(enc.CodePage, out FileOpenFlags flags))
                    return (Variant)(int)flags;
            }
            catch
            {
            }

            return Variant.FromNumber(-1);
        }

        internal static FunctionReturnValue FileGetShortName(CallFrame frame, Variant[] args) => NativeInterop.DoPlatformDependent(
            delegate
            {
                try
                {
                    return (Variant)FileSystemExtensions.GetShortPath(args[1] == 1 ? args[0].ToString() : Path.GetFullPath(args[0].ToString()));
                }
                catch
                {
                    return FunctionReturnValue.Error(1);
                }
            },
            () => args[0]
        );

        internal static FunctionReturnValue FileGetPos(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                    return (Variant)handle.FileStream.Position;
            }
            catch
            {
            }

            return FunctionReturnValue.Error(0m, 1, 0);
        }

        // internal static FunctionReturnValue FileGetShortcut(CallFrame frame, Variant[] args)
        // {
        // }

        internal static FunctionReturnValue FileGetLongName(CallFrame frame, Variant[] args) => NativeInterop.DoPlatformDependent(
            delegate
            {
                try
                {
                    return (Variant)Path.GetFullPath(args[0].ToString());
                }
                catch
                {
                    return FunctionReturnValue.Error(1);
                }
            },
            () => args[0]
        );

        internal static FunctionReturnValue FileGetSize(CallFrame frame, Variant[] args)
        {
            try
            {
                return (Variant)new FileInfo(args[0].ToString()).Length;
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        internal static FunctionReturnValue FileGetTime(CallFrame frame, Variant[] args)
        {
            try
            {
                FileSystemInfo file = new FileInfo(args[0].ToString());

                if (!file.Exists)
                    file = new DirectoryInfo(args[0].ToString());

                DateTime time = (int)args[1] switch
                {
                    1 => file.CreationTime,
                    2 => file.LastAccessTime,
                    _ => file.LastWriteTime,
                };

                return (int)args[2] switch
                {
                    1 => time.ToString("yyyyMMddHHmmss"),
                    _ => Variant.FromArray(
                        frame.Interpreter,
                        time.Year.ToString("D4"),
                        time.Month.ToString("D2"),
                        time.Day.ToString("D2"),
                        time.Hour.ToString("D2"),
                        time.Minute.ToString("D2"),
                        time.Second.ToString("D2")
                    ),
                };
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        // internal static FunctionReturnValue FileGetVersion(CallFrame frame, Variant[] args)
        // {
        // }

        // internal static FunctionReturnValue FileInstall(CallFrame frame, Variant[] args)
        // {
        // }

        internal static FunctionReturnValue FileMove(CallFrame frame, Variant[] args)
        {
            try
            {
                FileInfo src = new(args[0].ToString());
                FileInfo dst = new(args[1].ToString());
                long flags = (long)args[2];

                if ((flags & 8) != 0 && dst.Directory is { Exists: false } dir)
                    dir.Create();

                src.MoveTo(dst.FullName, (flags & 1) != 0);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue FileOpen(CallFrame frame, Variant[] args)
        {
            try
            {
                FileInfo file = new(args[0].ToString());
                FileOpenFlags flags = (FileOpenFlags)args[1].ToNumber();

                if (flags.HasFlag(FileOpenFlags.FO_CREATEPATH) && file.Directory is { Exists: false } dir)
                    dir.Create();

                FileStream fs = new(
                    file.FullName,
                    flags.HasFlag(FileOpenFlags.FO_OVERWRITE) ? FileMode.Create : flags.HasFlag(FileOpenFlags.FO_APPEND) ? FileMode.Append : FileMode.OpenOrCreate,
                    flags.HasFlag(FileOpenFlags.FO_OVERWRITE) || flags.HasFlag(FileOpenFlags.FO_APPEND) ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read
                );
                FileHandle handle = new(fs, flags);

                return frame.Interpreter.GlobalObjectStorage.Store(handle);
            }
            catch
            {
                return Variant.FromNumber(-1);
            }
        }

        // internal static FunctionReturnValue FileOpenDialog(CallFrame frame, Variant[] args)
        // {
        // }

        internal static FunctionReturnValue FileRead(CallFrame frame, Variant[] args)
        {
            try
            {
                int? count = args[1] < 0 || args[1].IsDefault ? null : (int?)(int)args[1];
                Variant output;

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    byte[] bytes = new byte[count ?? (handle.FileStream.Length - handle.FileStream.Position)];
                    int i = handle.FileStream.Read(bytes, 0, bytes.Length);

                    Array.Resize(ref bytes, i);

                    output = handle.Flags.HasFlag(FileOpenFlags.FO_BINARY) ? Variant.FromBinary(bytes) : Variant.FromString(From.Bytes(bytes).ToString(handle.Encoding));
                }
                else
                {
                    string s = File.ReadAllText(args[0].ToString());

                    if (count is int i && i < s.Length)
                        s = s[..i];

                    output = s;
                }

                frame.SetExtended(output.Length);

                if (count is int len && len > output.Length)
                    return FunctionReturnValue.Error(output, -1, output.Length); // eof

                return output;
            }
            catch
            {
                return FunctionReturnValue.Error(0m, 1, 0);
            }
        }

        internal static FunctionReturnValue FileReadLine(CallFrame frame, Variant[] args)
        {
            try
            {
                Index? line_index = (int)args[1] switch
                {
                    0 => null,
                    _ when args[1].IsDefault => null,
                    int i when i < 0 => ^i,
                    int i => i - 1,
                };

                Variant output;
                bool eof = false;

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    string s;

                    if (line_index is Index i)
                    {
                        List<string> lines = new();

                        handle.FileStream.Seek(0, SeekOrigin.Begin);

                        while (handle.StreamReader.ReadLine() is string line)
                            lines.Add(line);

                        s = lines[i];
                    }
                    else
                        s = handle.StreamReader.ReadLine() ?? "";

                    output = handle.Flags.HasFlag(FileOpenFlags.FO_BINARY) ? Variant.FromBinary(From.String(s, handle.Encoding).Data) : Variant.FromString(s);
                    eof = handle.FileStream.Position < handle.FileStream.Length - 1;
                }
                else
                    output = File.ReadAllLines(args[0].ToString())[line_index ?? 0];

                frame.SetExtended(output.Length);

                if (eof)
                    return FunctionReturnValue.Error(output, -1, output.Length); // eof

                return output;
            }
            catch
            {
                return FunctionReturnValue.Error(0m, 1, 0);
            }
        }

        internal static FunctionReturnValue FileReadToArray(CallFrame frame, Variant[] args)
        {
            string[] lines = Array.Empty<string>();
            bool error = false;

            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                    lines = From.Stream(handle.FileStream).ToLines();
                else
                    lines = File.ReadAllLines(args[0].ToString());
            }
            catch
            {
                error = true;
            }

            return FunctionReturnValue.Error(Variant.FromArray(frame.Interpreter, lines.Select(Variant.FromString)), error ? 1 : lines.Length == 0 ? 2 : 0, lines.Length);
        }

        internal static FunctionReturnValue FileRecycle(CallFrame frame, Variant[] args)
        {
            try
            {
                FileInfo file = new(args[0].ToString());

                return NativeInterop.DoPlatformDependent(delegate
                {
                    SHFILEOPSTRUCT opt = new()
                    {
                        hwnd = null,
                        wFunc = FileFuncFlags.FO_DELETE,
                        pFrom = $"{file.FullName}\0\0",
                        pTo = null,
                        fFlags = FILEOP_FLAGS.FOF_SILENT | FILEOP_FLAGS.FOF_NOCONFIRMATION | FILEOP_FLAGS.FOF_NOERRORUI | FILEOP_FLAGS.FOF_ALLOWUNDO
                    };

                    return Variant.FromBoolean(NativeInterop.SHFileOperation(ref opt) == 0);
                }, delegate
                {
                    foreach (DirectoryInfo trash in new[]
                    {
                        "~/.local/share/Trash/items/",
                        "~/.local/share/Trash/",
                        "~/.Trash/",
                        "~/Desktop/.Trash/items/",
                        "~/Desktop/.Trash/",
                        "/var/tmp",
                    }.Select(p => new DirectoryInfo(p)))
                    {
                        if (trash.Exists)
                        {
                            file.MoveTo(Path.Combine(trash.FullName, file.Name));

                            return Variant.True;
                        }
                    }

                    return Variant.False;
                });
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static unsafe FunctionReturnValue FileRecycleEmpty(CallFrame frame, Variant[] args)
        {
            try
            {
                return NativeInterop.DoPlatformDependent(
                    () => Variant.FromBoolean(NativeInterop.SHEmptyRecycleBin(null, args[0].IsDefault ? null : args[0].ToString(), 0) == 0),
                    delegate
                    {
                        foreach (DirectoryInfo trash in new[]
                        {
                            "~/.local/share/Trash/items/",
                            "~/.local/share/Trash/",
                            "~/.Trash/",
                            "~/Desktop/.Trash/items/",
                            "~/Desktop/.Trash/",
                            "/tmp",
                        }.Select(p => new DirectoryInfo(p)))
                        {
                            if (trash.Exists)
                            {
                                trash.EnumerateDirectories().Do(d => d.Delete(true));
                                trash.EnumerateFiles().Do(f => f.Delete());
                            }
                        }

                        return Variant.False;
                    }
                );
            }
            catch
            {
                return Variant.False;
            }
        }

        // internal static FunctionReturnValue FileSaveDialog(CallFrame frame, Variant[] args)
        // {
        // }

        // internal static FunctionReturnValue FileSelectFolder(CallFrame frame, Variant[] args)
        // {
        // }

        internal static FunctionReturnValue FileSetAttrib(CallFrame frame, Variant[] args)
        {
            try
            {
                string flags = args[1].ToString();
                FileAttributes pos = default;
                FileAttributes neg = default;
                bool mode_pos = true;

                foreach (char c in flags)
                {
                    if (char.IsWhiteSpace(c))
                        continue;
                    else if (c is '+')
                        mode_pos = true;
                    else if (c is '-')
                        mode_pos = false;
                    else
                        (mode_pos ? ref pos : ref neg) |= c switch
                        {
                            'R' => FileAttributes.ReadOnly,
                            'A' => FileAttributes.Archive,
                            'S' => FileAttributes.System,
                            'H' => FileAttributes.Hidden,
                            'N' => FileAttributes.Normal,
                            'O' => FileAttributes.Offline,
                            'T' => FileAttributes.Temporary,
                            _ => throw new ArgumentException(),
                        };
                }

                void traverse(FileSystemInfo fsi)
                {
                    if (fsi is DirectoryInfo dir && (int)args[3] == 1)
                        foreach (FileSystemInfo item in dir.EnumerateFileSystemInfos())
                            traverse(item);

                    fsi.Attributes = (fsi.Attributes & ~neg) | pos;
                };

                FileSystemExtensions.ResolveWildCards(args[0].ToString()).Do(traverse);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue FileSetEnd(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    handle.FileStream.Seek(0, SeekOrigin.End);

                    return Variant.True;
                }
            }
            catch
            {
            }

            return Variant.False;
        }

        internal static FunctionReturnValue FileSetPos(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    handle.FileStream.Seek((long)args[1], (SeekOrigin)(int)args[2]);

                    return Variant.True;
                }
            }
            catch
            {
            }

            return Variant.False;
        }

        internal static FunctionReturnValue FileSetTime(CallFrame frame, Variant[] args)
        {
            try
            {
                DateTime time;

                if (!DateTime.TryParseExact(args[1].ToString(), "yyyyMMddHHmmss", null, DateTimeStyles.AssumeLocal, out time))
                    time = DateTime.Now;

                void traverse(FileSystemInfo fsi)
                {
                    if (fsi is DirectoryInfo dir && (int)args[3] == 1)
                        foreach (FileSystemInfo item in dir.EnumerateFileSystemInfos())
                            traverse(item);

                    if (args[2] == 1m)
                        fsi.CreationTime = time;
                    else if (args[2] == 2m)
                        fsi.LastAccessTime = time;
                    else
                        fsi.LastWriteTime = time;
                }

                FileSystemExtensions.ResolveWildCards(args[0].ToString()).Do(traverse);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue FileWrite(CallFrame frame, Variant[] args)
        {
            try
            {
                byte[] data = args[1].IsBinary ? args[1].ToBinary() : From.String(args[1].ToString(), Encoding.UTF8).ToBytes();

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                    handle.FileStream.Write(data, 0, data.Length);
                else
                    File.WriteAllBytes(args[0].ToString(), data);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue FileWriteLine(CallFrame frame, Variant[] args)
        {
            try
            {
                string content = args[1].ToString();

                if (!content.EndsWith("\r") && !content.EndsWith("\n"))
                    content += "\r\n";

                if (args[0].TryResolveHandle(frame.Interpreter, out FileHandle? handle))
                {
                    if (handle.StreamWriter is null)
                        return Variant.False;
                    else
                        handle.StreamWriter?.Write(content);
                }
                else
                    File.WriteAllText(args[0].ToString(), content);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        #endregion
        #region G, H

        internal static FunctionReturnValue Hex(CallFrame frame, Variant[] args)
        {
            byte[] bytes = args[0].ToBinary();
            int length = (int)args[1];

            if (args[0].Type is VariantType.Binary)
                return (Variant)From.Bytes(bytes).ToHexString();
            else if (!args[1].IsDefault && length < 1)
                return Variant.EmptyString;

            length = Math.Max(4, Math.Min(length, 16));

            if (bytes.Length < length)
                bytes = new byte[length - bytes.Length].Concat(bytes).ToArray();

            return (Variant)From.Bytes(bytes).ToHexString();
        }

        internal static FunctionReturnValue Hwnd(CallFrame frame, Variant[] args) => Variant.FromHandle((nint)args[0]);

        #endregion
        #region INET...

        internal static FunctionReturnValue InetClose(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out InetHandle? inet))
                {
                    if (inet.Running)
                        inet.Cancel();

                    inet.Dispose();

                    return Variant.True;
                }
            }
            catch
            {
            }

            return Variant.False;
        }

        internal static FunctionReturnValue InetGet(CallFrame frame, Variant[] args)
        {
            string uri = args[0].ToString();
            string filename = args[1].ToString();
            int options = (int)args[2];
            bool background = (bool)args[3];

            if (!background)
                try
                {
                    From source = uri.StartsWith("ftp", StringComparison.InvariantCultureIgnoreCase) ? From.FTP(uri) : From.HTTP(uri);

                    source.ToFile(filename);

                    return (Variant)source.ByteCount;
                }
                catch
                {
                    return FunctionReturnValue.Error(1);
                }

            InetHandle inet = new(uri, filename, options);
            Variant handle = frame.Interpreter.GlobalObjectStorage.Store(inet);

            inet.Start();

            return handle;
        }

        internal static FunctionReturnValue InetGetInfo(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out InetHandle? inet))
                {
                    int index = (int)args[1];
                    Variant[] array =
                    {
                        0, // TODO : currently downloaded
                        0,
                        inet.Complete,
                        inet.Complete && !inet.Error,
                        inet.Error,
                        args[0]
                    };

                    return index >= 0 && index < array.Length ? array[index] : Variant.FromArray(frame.Interpreter, array);
                }
                else if (args[0].IsNull)
                    return (Variant)frame.Interpreter.GlobalObjectStorage.Objects.Count(o => o is InetHandle);
            }
            catch
            {
            }

            return FunctionReturnValue.Error(Variant.EmptyString, 1, Variant.Zero);
        }

        internal static FunctionReturnValue InetGetSize(CallFrame frame, Variant[] args)
        {
            string uri = args[0].ToString();

            try
            {
                From source = uri.StartsWith("ftp", StringComparison.InvariantCultureIgnoreCase) ? From.FTP(uri) : From.HTTP(uri);

                return (Variant)source.ByteCount;
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        internal static FunctionReturnValue InetRead(CallFrame frame, Variant[] args)
        {
            string uri = args[0].ToString();

            try
            {
                From source = uri.StartsWith("ftp", StringComparison.InvariantCultureIgnoreCase) ? From.FTP(uri) : From.HTTP(uri);

                return FunctionReturnValue.Success(Variant.FromBinary(source.Data), source.ByteCount);
            }
            catch
            {
                return FunctionReturnValue.Error(Variant.EmptyString, 1, Variant.Zero);
            }
        }

        #endregion
        #region INI...

        internal static FunctionReturnValue IniDelete(CallFrame frame, Variant[] args)
        {
            string path = args[0].ToString();
            string section = args[1].ToString();
            string key = args[2].ToString();

            try
            {
                INIFile ini = From.File(path).ToINI();

                if (ini.TryGetSection(section, out INISection? sec))
                    if (args[2].IsDefault)
                        ini.TryDeleteSection(section);
                    else
                        sec.Remove(key);

                From.INI(ini).ToFile(path);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue IniRead(CallFrame frame, Variant[] args)
        {
            string path = args[0].ToString();
            string section = args[1].ToString();
            string key = args[2].ToString();

            try
            {
                return (Variant)From.File(path).ToINI()[section][key];
            }
            catch
            {
                return args[3];
            }
        }

        internal static FunctionReturnValue IniReadSection(CallFrame frame, Variant[] args)
        {
            string path = args[0].ToString();
            string section = args[1].ToString();

            try
            {
                return Variant.FromArray(frame.Interpreter, From.File(path).ToINI()[section].Select(sec => Variant.FromArray(frame.Interpreter, sec.Key, sec.Value)));
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        internal static FunctionReturnValue IniReadSectionNames(CallFrame frame, Variant[] args)
        {
            string path = args[0].ToString();

            try
            {
                return Variant.FromArray(frame.Interpreter, From.File(path).ToINI().SectionKeys.Select(Variant.FromString));
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        internal static FunctionReturnValue IniRenameSection(CallFrame frame, Variant[] args)
        {
            string path = args[0].ToString();
            string old_sec = args[1].ToString();
            string new_sec = args[2].ToString();
            bool overwrite = args[3].ToBoolean();

            try
            {
                INIFile ini = From.File(path).ToINI();

                if (old_sec != new_sec)
                    if (!ini.TryGetSection(old_sec, out INISection? section))
                        return Variant.False;
                    else if (ini.HasSection(new_sec) && !overwrite)
                        return FunctionReturnValue.Error(Variant.False, 1, Variant.Zero);
                    else
                    {
                        ini[new_sec] = section ?? new INISection();
                        ini.TryDeleteSection(old_sec);

                        From.INI(ini).ToFile(path);
                    }

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue IniWrite(CallFrame frame, Variant[] args)
        {
            string section = args[1].ToString();
            string key = args[2].ToString();
            string value = args[3].ToString();

            try
            {
                FileInfo ini_file = new(args[0].ToString());
                INIFile ini = ini_file.Exists ? From.File(ini_file).ToINI() : new();

                if (!ini.HasSection(section))
                    ini[section] = new();

                ini[section][key] = value;

                From.INI(ini).ToFile(ini_file);

                return Variant.True;
            }
            catch
            {
                return Variant.False;
            }
        }

        internal static FunctionReturnValue IniWriteSection(CallFrame frame, Variant[] args)
        {
            string section = args[1].ToString();
            Variant data = args[2];
            int index = (int)args[3];

            try
            {
                FileInfo ini_file = new(args[0].ToString());
                INIFile ini = ini_file.Exists ? From.File(ini_file).ToINI() : new();
                INISection sec = ini.GetOrAddSection(section);

                if (args[2] is { Type: VariantType.Array } arr)
                    foreach (Variant element in arr.ToArray(frame.Interpreter))
                    {
                        Variant[] kvp = element.ToArray(frame.Interpreter);

                        sec[kvp[0].ToString()] = kvp[1].ToString();
                    }
                else if (args[2] is { Type: VariantType.Map } map)
                    foreach (var kvp in args[2].ToMap(frame.Interpreter))
                        sec[kvp.Key.ToString()] = kvp.Value.ToString();
                else
                    foreach (string line in args[2].ToString().SplitIntoLines())
                    {
                        int idx = line.IndexOf('=');

                        sec[line[..idx]] = line[(idx + 1)..];
                    }

                From.INI(ini).ToFile(ini_file);

                return Variant.True;
            }
            catch
            {
                return FunctionReturnValue.Error(Variant.False, 1, Variant.Zero);
            }
        }

        #endregion
        #region IS...

        internal static FunctionReturnValue IsDeclared(CallFrame frame, Variant[] args) =>
            GetAu3Caller(frame, nameof(Execute)).Match<FunctionReturnValue>(err => err, au3 =>
            {
                if (au3.VariableResolver.TryGetVariable(args[0].ToString(), VariableSearchScope.Global, out Variable? variable))
                    return (Variant)(variable.IsGlobal ? 1 : -1);
                else
                    return Variant.Zero;
            });

        internal static unsafe FunctionReturnValue IsAdmin(CallFrame frame, Variant[] args) => (Variant)NativeInterop.DoPlatformDependent(delegate
        {
            void* processHandle = NativeInterop.GetCurrentProcess();
            void* token;

            if (NativeInterop.OpenProcessToken(processHandle, NativeInterop.TOKEN_READ, &token))
                try
                {
                    int elevation;

                    if (NativeInterop.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation, &elevation, sizeof(int), out _))
                        return elevation != 0;
                }
                finally
                {
                    NativeInterop.LocalFree(token);
                }

            return false;
        }, () => NativeInterop.Linux__geteuid() == 0);

        internal static FunctionReturnValue IsArray(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Array);

        internal static FunctionReturnValue IsBinary(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Binary);

        internal static FunctionReturnValue IsBool(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Boolean);

        internal static FunctionReturnValue IsFloat(CallFrame frame, Variant[] args) => (Variant)(args[0].ToNumber() is double d && (long)d != d);

        internal static FunctionReturnValue IsFunc(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Function);

        internal static FunctionReturnValue IsInt(CallFrame frame, Variant[] args) => (Variant)(args[0].ToNumber() is double d && (long)d == d);

        internal static FunctionReturnValue IsKeyword(CallFrame frame, Variant[] args) => (Variant)(args[0].IsDefault ? 1m : args[0].IsNull ? 2m : 0m);

        internal static FunctionReturnValue IsNumber(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.Number);

        internal static FunctionReturnValue IsObj(CallFrame frame, Variant[] args) => (Variant)args[0].IsObject;

        internal static FunctionReturnValue IsPtr(CallFrame frame, Variant[] args) => (Variant)args[0].IsPtr;

        internal static FunctionReturnValue IsHwnd(CallFrame frame, Variant[] args) => (Variant)args[0].IsHandle;

        internal static FunctionReturnValue IsString(CallFrame frame, Variant[] args) => (Variant)(args[0].Type is VariantType.String);

        #endregion
        #region I, J, K

        internal static FunctionReturnValue Int(CallFrame frame, Variant[] args) => (Variant)(long)args[0];

        #endregion
        #region L, M, N

        internal static FunctionReturnValue Log(CallFrame frame, Variant[] args) => (Variant)Math.Log((double)args[0].ToNumber());

        internal static FunctionReturnValue Mod(CallFrame frame, Variant[] args) => args[0] % args[1];

        internal static FunctionReturnValue MsgBox(CallFrame frame, Variant[] args)
        {
            uint flags = (uint)args[0];
            string title = args[1].ToString();
            string text = args[2].ToString();
            double timeout = args[3].ToNumber();
            int hwnd = (int)args[4];

            // TODO : timeout
            // TODO : other platforms

            return NativeInterop.DoPlatformDependent(delegate
            {
                return (Variant)NativeInterop.MessageBox(hwnd, text, title, flags);
            }, () => throw new NotImplementedException());
        }

        internal static FunctionReturnValue Number(CallFrame frame, Variant[] args) => (Variant)(double)args[0];

        #endregion
        #region ONAUTOITEXIT...

        internal static FunctionReturnValue OnAutoItExitRegister(CallFrame frame, Variant[] args)
        {
            Union<InterpreterError, AU3CallFrame> caller = GetAu3Caller(frame, nameof(OnAutoItExitRegister));

            if (caller.Is(out AU3CallFrame? au3fame))
            {
                string func = args[0].IsFunction(out ScriptFunction? f) ? f.Name : args[0].ToString();

                return (Variant)au3fame.CurrentFunction.Script.AddExitFunction(func, au3fame.CurrentLocation);
            }
            else
                return Variant.False;
        }

        internal static FunctionReturnValue OnAutoItExitUnRegister(CallFrame frame, Variant[] args)
        {
            Union<InterpreterError, AU3CallFrame> caller = GetAu3Caller(frame, nameof(OnAutoItExitUnRegister));

            if (caller.Is(out AU3CallFrame? au3fame))
            {
                string func = args[0].IsFunction(out ScriptFunction? f) ? f.Name : args[0].ToString();

                return (Variant)au3fame.CurrentFunction.Script.RemoveExitFunction(func);
            }
            else
                return Variant.False;
        }

        #endregion
        #region OBJ...

        internal static FunctionReturnValue ObjCreate(CallFrame frame, Variant[] args)
        {
            if (!frame.Interpreter.IsCOMAvailable)
                frame.IssueWarning("warning.com_unavailable");
            else
                try
                {
                    string?[] confg = args[1..].ToArray(a => a.IsDefault ? null : a.ToString());

                    if (Variant.TryCreateCOM(frame.Interpreter, args[0].ToString(), confg[0], confg[1], confg[2], out Variant? com))
                        return com;
                }
                catch
                {
                }

            return FunctionReturnValue.Error(Variant.Null, 1, Variant.Null);
        }

        // internal static FunctionReturnValue ObjCreateInterface(CallFrame frame, Variant[] args)
        // {
        // 
        // }
        // 
        // internal static FunctionReturnValue ObjEvent(CallFrame frame, Variant[] args)
        // {
        // 
        // }

        internal static FunctionReturnValue ObjGet(CallFrame frame, Variant[] args)
        {
            string path = args[0].ToString();


            throw new NotImplementedException();
        }

        internal static FunctionReturnValue ObjName(CallFrame frame, Variant[] args)
        {
            if (args[0] is { Type: VariantType.COMObject } handle)
            {
                string? info = null;

                frame.Interpreter.COMConnector?.TryGetCOMObjectInfo((uint)handle, (COMObjectInfoMode)(int)args[1], out info);

                if (info is string s)
                    return (Variant)s;
            }

            return FunctionReturnValue.Error(Variant.EmptyString, 1, Variant.Null);
        }

        #endregion
        #region P...

        internal static unsafe FunctionReturnValue Ping(CallFrame frame, Variant[] args)
        {
            int err = 4;

            try
            {
                using Ping ping = new();

                PingReply reply = ping.Send(args[0].ToString(), (int)args[1]);

                if (reply.Status == IPStatus.Success)
                    return (Variant)Math.Min(1, reply.RoundtripTime);
                else if (reply.Status is IPStatus.DestinationUnreachable or IPStatus.DestinationNetworkUnreachable or IPStatus.DestinationHostUnreachable
                                      or IPStatus.DestinationProhibited or IPStatus.DestinationProtocolUnreachable or IPStatus.DestinationPortUnreachable)
                    err = 2;
                else if (reply.Status is IPStatus.BadDestination)
                    err = 3;
                else if (reply.Status is IPStatus.TimedOut or IPStatus.TtlExpired or IPStatus.TtlReassemblyTimeExceeded or IPStatus.TimeExceeded)
                    err = 1;
            }
            catch
            {
            }

            return FunctionReturnValue.Error(err);
        }

        #endregion
        #region PROCESS...

        internal static FunctionReturnValue ProcessClose(CallFrame frame, Variant[] args)
        {
            if (GetProcess(args[0]) is Process proc)
                try
                {
                    proc.Kill();

                    return Variant.True;
                }
                catch
                {
                    return FunctionReturnValue.Error(3);
                }
            else
                return FunctionReturnValue.Error(4);
        }

        internal static FunctionReturnValue ProcessExists(CallFrame frame, Variant[] args) => GetProcess(args[0]) is Process proc ? proc.Id : Variant.Zero;

        internal static FunctionReturnValue ProcessGetStats(CallFrame frame, Variant[] args)
        {
            if (GetProcess(args[0]) is Process proc)
                return (int)args[1] switch
                {
                    0 => Variant.FromArray(frame.Interpreter, proc.WorkingSet64, proc.PeakWorkingSet64),
                    // TODO : 1 => Variant.FromArray(frame.Interpreter, ),
                    _ => FunctionReturnValue.Error(1),
                };
            else
                return FunctionReturnValue.Error(1);
        }

        internal static FunctionReturnValue ProcessList(CallFrame frame, Variant[] args)
        {
            Variant[] items = Process.GetProcesses()
                                     .Where(proc => args[0].IsDefault ? true : proc.ProcessName == args[0].ToString())
                                     .ToArray(proc => Variant.FromArray(frame.Interpreter, proc.ProcessName, proc.Id));

            return Variant.FromArray(frame.Interpreter, items.Prepend(Variant.FromArray(frame.Interpreter, items.Length)));
        }

        internal static FunctionReturnValue ProcessSetPriority(CallFrame frame, Variant[] args)
        {
            if (GetProcess(args[0]) is Process proc)
                try
                {
                    proc.PriorityClass = (int)args[1] switch
                    {
                        0 => ProcessPriorityClass.Idle,
                        1 => ProcessPriorityClass.BelowNormal,
                        2 => ProcessPriorityClass.Normal,
                        3 => ProcessPriorityClass.AboveNormal,
                        4 => ProcessPriorityClass.High,
                        5 => ProcessPriorityClass.RealTime,
                        _ => (ProcessPriorityClass)(-1)
                    };

                    return Variant.True;
                }
                catch
                {
                    return FunctionReturnValue.Error(2);
                }
            else
                return FunctionReturnValue.Error(1);
        }

        internal static FunctionReturnValue ProcessWait(CallFrame frame, Variant[] args)
        {
            string procname = args[0].ToString();
            long timeout = (long)args[1] * 1000;
            Stopwatch sw = new();

            sw.Start();

            while (timeout <= 0 || sw.ElapsedMilliseconds < timeout)
            {
                try
                {
                    if (Process.GetProcessesByName(procname).FirstOrDefault()?.Id is int pid)
                        return (Variant)pid;
                }
                catch
                {
                }

                Thread.Sleep(250);
            }

            return Variant.Zero;
        }

        internal static FunctionReturnValue ProcessWaitClose(CallFrame frame, Variant[] args)
        {
            if (GetProcess(args[0]) is Process proc)
            {
                if ((int)args[1] <= 0)
                    proc.WaitForExit();
                else
                    proc.WaitForExit((int)args[0]);

                return (Variant)proc.HasExited;
            }
            else
                return Variant.False;
        }

        #endregion
        #region R...

        internal static unsafe FunctionReturnValue Random(CallFrame frame, Variant[] args)
        {
            double min = (double)args[0];
            double max = (double)args[1];

            if (frame.PassedArguments.Length == 1)
                (min, max) = (0, (double)args[0]);

            double val = frame.Interpreter.Random.NextDouble();

            val *= max - min;
            val += min;

            if (args[2].ToBoolean())
                val = Math.Round(val);

            return Variant.FromNumber(val);
        }

        internal static FunctionReturnValue Round(CallFrame frame, Variant[] args) => (Variant)Math.Round(args[0].ToNumber(), (int)args[1]);

        #endregion
        #region RUN + SHELL_EXEC

        internal static FunctionReturnValue Run(CallFrame frame, Variant[] args) =>
            Run(args[0].ToString(), args[1].ToString(), (int)args[2], (int)args[3], false);

        internal static FunctionReturnValue RunWait(CallFrame frame, Variant[] args) =>
            Run(args[0].ToString(), args[1].ToString(), (int)args[2], (int)args[3], true);

        internal static FunctionReturnValue RunAs(CallFrame frame, Variant[] args) =>
            RunAs(args[0].ToString(), args[1].ToString(), args[2].ToString(), (int)args[3], args[4].ToString(), args[5].ToString(), (int)args[6], (int)args[7], false);

        internal static FunctionReturnValue RunAsWait(CallFrame frame, Variant[] args) =>
            RunAs(args[0].ToString(), args[1].ToString(), args[2].ToString(), (int)args[3], args[4].ToString(), args[5].ToString(), (int)args[6], (int)args[7], true);

        internal static FunctionReturnValue ShellExecute(CallFrame frame, Variant[] args) =>
            Run(args[0].ToString(), args[1].ToString(), true, args[3].ToString(), args[2].ToString(), (int)args[3], 0, false);

        internal static FunctionReturnValue ShellExecuteWait(CallFrame frame, Variant[] args) =>
            Run(args[0].ToString(), args[1].ToString(), true, args[3].ToString(), args[2].ToString(), (int)args[3], 0, true);

        private static FunctionReturnValue Run(string program, string workingdir, int sw_state, int opt_flag, bool wait) =>
            RunAs(null, null, null, 0, program, workingdir, sw_state, opt_flag, wait);

        private static FunctionReturnValue Run(string file, string args, bool shell_exec, string verb, string workingdir, int sw_state, int opt_flag, bool wait) =>
            RunAs(null, null, null, 0, file, args, shell_exec, workingdir, verb, sw_state, opt_flag, wait);

        private static FunctionReturnValue RunAs(string? user, string? domain, string? passwd, int logon_flags, string program, string workingdir, int sw_state, int opt_flag, bool wait)
        {
            (string file, string args) = program.Match(REGEX_RUN, out ReadOnlyIndexer<string, string>? groups) ? (groups["file"], groups["args"]) : (program, "");

            return RunAs(user, domain, passwd, logon_flags, file, args, false, workingdir, null, sw_state, opt_flag, wait);
        }

        private static FunctionReturnValue RunAs(string? user, string? domain, string? passwd, int logon_flags, string file, string args, bool shell_exec, string workingdir, string? verb, int sw_state, int opt_flag, bool wait)
        {
            try
            {
                ProcessStartInfo psi = new()
                {
                    UserName = user!,
                    FileName = file,
                    Arguments = args,
                    WorkingDirectory = workingdir,
                    UseShellExecute = shell_exec,
                    WindowStyle = sw_state switch
                    {
                        0 => ProcessWindowStyle.Hidden,
                        3 => ProcessWindowStyle.Maximized,
                        2 or 6 => ProcessWindowStyle.Minimized,
                        _ => ProcessWindowStyle.Normal,
                    },
                    CreateNoWindow = (opt_flag & 0x10000) == 0,
                    RedirectStandardInput = (opt_flag & 0x01) != 0,
                    RedirectStandardOutput = (opt_flag & 0x0a) != 0,
                    RedirectStandardError = (opt_flag & 0x0c) != 0,
                };

                if (verb is string)
                    psi.Verb = verb;

                NativeInterop.DoPlatformDependent(delegate
                {
                    psi.Domain = domain!;
                    psi.PasswordInClearText = passwd!;
                    // TODO : use the logon_flags
                }, OS.Windows);

                if (Process.Start(psi) is Process process)
                    using (process)
                    {
                        if ((opt_flag & 0x10) != 0)
                            throw new NotImplementedException(); // TODO : redirect stdin/stdout/stderr to console

                        if ((opt_flag & 0x8) != 0)
                            throw new NotImplementedException(); // TODO : stderr = stdout

                        if (wait)
                            process.WaitForExit();
                    }
                }
                catch
                {
                }

            return FunctionReturnValue.Error(1);
        }

        #endregion
        #region REG...

        internal static unsafe FunctionReturnValue RegDelete(CallFrame frame, Variant[] args)
        {
            GetRegistryPath(args[0].ToString(), out void* hk_base, out string key);

            int res = args[1].IsDefault ? NativeInterop.RegDeleteKeyEx(hk_base, key, RegSAM.WOW64_64Key, null)
                                        : NativeInterop.RegDeleteKeyValue(hk_base, key, args[1].ToString());

            return Variant.FromBoolean(res is 0);
        }

        internal static unsafe FunctionReturnValue RegEnumKey(CallFrame frame, Variant[] args)
        {
            void* hkey = null;
            int err = 0;

            try
            {
                GetRegistryPath(args[0].ToString(), out void* hk_base, out string key);

                if ((err = NativeInterop.RegOpenKeyEx(hk_base, key, 0, RegSAM.Read, out hkey)) is 0)
                {
                    int max_size = 512;
                    StringBuilder sb = new(max_size);

                    if ((err = NativeInterop.RegEnumKeyEx(hkey, (int)args[1], sb, &max_size, null, null, null, out _)) is 0)
                        return (Variant)sb;
                }
            }
            finally
            {
                NativeInterop.RegCloseKey(hkey);
            }

            return FunctionReturnValue.Error(err);
        }

        internal static unsafe FunctionReturnValue RegEnumVal(CallFrame frame, Variant[] args)
        {
            void* hkey = null;
            int err = 0;

            try
            {
                GetRegistryPath(args[0].ToString(), out void* hk_base, out string key);

                if ((err = NativeInterop.RegOpenKeyEx(hk_base, key, 0, RegSAM.Read, out hkey)) is 0)
                {
                    int max_size = 512;
                    StringBuilder sb = new(max_size);

                    if ((err = NativeInterop.RegEnumValue(hkey, (int)args[1], sb, &max_size, null, out RegKeyType type, null, out _)) is 0)
                        return FunctionReturnValue.Success(sb, type.ToString());
                }
            }
            finally
            {
                NativeInterop.RegCloseKey(hkey);
            }

            return FunctionReturnValue.Error(err);
        }

        internal static unsafe FunctionReturnValue RegRead(CallFrame frame, Variant[] args)
        {
            GetRegistryPath(args[0].ToString(), out void* hk_base, out string key);

            void* hkey = null;
            int err;

            try
            {
                if ((err = NativeInterop.RegOpenKeyEx(hk_base, key, 0, RegSAM.Write, out hkey)) != 0)
                    return FunctionReturnValue.Error(err);
                else if ((err = NativeInterop.RegGetValue(hk_base, key, args[1].ToString(), 0xffff, out RegKeyType type, null, out int size)) != 0)
                    return FunctionReturnValue.Error(err);
                else
                {
                    void* data = (void*)Marshal.AllocHGlobal(size);

                    err = NativeInterop.RegGetValue(hk_base, key, args[1].ToString(), type switch
                    {
                        RegKeyType.REG_SZ => 0x0002,
                        RegKeyType.REG_MULTI_SZ => 0x0020,
                        RegKeyType.REG_EXPAND_SZ => 0x0004,
                        RegKeyType.REG_DWORD => 0x0018,
                        RegKeyType.REG_QWORD => 0x0048,
                        RegKeyType.REG_BINARY => 0x0008,
                        RegKeyType.REG_UNKNOWN or _ => 0xffff,
                    }, out type, data, out size);

                    if (err is 0)
                        switch (type)
                        {
                            case RegKeyType.REG_SZ or RegKeyType.REG_EXPAND_SZ or RegKeyType.REG_MULTI_SZ:
                                string s = From.Pointer(data, size - 1).ToString();

                                if (type is RegKeyType.REG_MULTI_SZ)
                                    s = s.Replace('\0', '\n');

                                return FunctionReturnValue.Success(s, type.ToString());
                            case RegKeyType.REG_DWORD or RegKeyType.REG_DWORD_LITTLE_ENDIAN:
                                return FunctionReturnValue.Success(From.Pointer(data, size).ToUnmanaged<int>(), "REG_DWORD");
                            case RegKeyType.REG_DWORD_BIG_ENDIAN:
                                return FunctionReturnValue.Success(From.Pointer(data, size).Reverse().ToUnmanaged<int>(), "REG_DWORD");
                            case RegKeyType.REG_QWORD:
                                return FunctionReturnValue.Success(From.Pointer(data, size).ToUnmanaged<long>(), type.ToString());
                            case RegKeyType.REG_LINK:
                                return FunctionReturnValue.Error(-2);
                            case RegKeyType.REG_NONE or RegKeyType.REG_UNKNOWN or RegKeyType.REG_BINARY:
                            default:
                                return FunctionReturnValue.Success(From.Pointer(data, size).Data, type.ToString());
                        }
                }
            }
            finally
            {
                NativeInterop.RegCloseKey(hkey);
            }

            return FunctionReturnValue.Error(err);
        }

        internal static unsafe FunctionReturnValue RegWrite(CallFrame frame, Variant[] args)
        {
            string name = args[1].ToString();
            Variant value = args[3];

            RegKeyType type = args[2].ToString().ToUpperInvariant() switch
            {
                "REG_SZ" => RegKeyType.REG_SZ,
                "REG_MULTI_SZ" => RegKeyType.REG_MULTI_SZ,
                "REG_EXPAND_SZ" => RegKeyType.REG_EXPAND_SZ,
                "REG_DWORD" => RegKeyType.REG_DWORD,
                "REG_QWORD" => RegKeyType.REG_QWORD,
                "REG_BINARY" => RegKeyType.REG_BINARY,
                _ => RegKeyType.REG_UNKNOWN,
            };

            GetRegistryPath(args[0].ToString(), out void* hk_base, out string key);

            if (args[1].IsDefault)
            {
                int res = NativeInterop.RegCreateKeyEx(hk_base, key, null, null, RegOption.NonVolatile, RegSAM.Write, null, out void* hkey, out _);

                NativeInterop.RegCloseKey(hkey);

                return Variant.FromBoolean(res is 0);
            }
            else
            {
                int retVal = NativeInterop.RegOpenKeyEx(hk_base, key, 0, RegSAM.Write, out void* hkey);

                if (retVal is 0)
                {
                    nint pData;
                    int size;

                    switch (type)
                    {
                        case RegKeyType.REG_SZ or RegKeyType.REG_MULTI_SZ or RegKeyType.REG_EXPAND_SZ:
                            if (type is RegKeyType.REG_MULTI_SZ)
                                value = value.ToString().Replace('\n', '\0') + '\0';

                            size = value.Length + 1;
                            pData = Marshal.StringToHGlobalUni(value.ToString());

                            break;
                        case RegKeyType.REG_DWORD:
                            pData = Marshal.AllocHGlobal(size = sizeof(int));
                            *(int*)pData = (int)value;

                            break;
                        case RegKeyType.REG_QWORD:
                            pData = Marshal.AllocHGlobal(size = sizeof(long));
                            *(long*)pData = (long)value;

                            break;
                        case RegKeyType.REG_BINARY:
                        case RegKeyType.REG_UNKNOWN:
                        default:
                            pData = Marshal.AllocHGlobal(size = value.Length);

                            Marshal.Copy(value.ToBinary(), 0, pData, size);

                            break;
                    }

                    retVal = NativeInterop.RegSetValueEx(hkey, name, null, type, (void*)pData, size);
                }

                NativeInterop.RegCloseKey(hkey);

                if (retVal is 0)
                    return Variant.True;
                else
                    return FunctionReturnValue.Error(retVal);
            }
        }

        #endregion
        #region S...

        internal static unsafe FunctionReturnValue Shutdown(CallFrame frame, Variant[] args)
        {
            try
            {
                ShutdownMode mode = (ShutdownMode)(int)args[0];
                bool success = NativeInterop.DoPlatformDependent(delegate
                {
                    uint flags = 0;

                    if (mode.HasFlag(ShutdownMode.SD_REBOOT))
                        flags = 0x00000004;
                    else if (mode.HasFlag(ShutdownMode.SD_POWERDOWN))
                        flags = 0x00000008;
                    else if (mode.HasFlag(ShutdownMode.SD_SHUTDOWN))
                        flags = 0x00000010;
                    else if (mode is ShutdownMode.SD_HIBERNATE)
                        return NativeInterop.SetSuspendState(true, true, true);
                    else if (mode is ShutdownMode.SD_STANDBY)
                        return NativeInterop.SetSuspendState(false, true, true);

                    if (mode.HasFlag(ShutdownMode.SD_FORCE))
                        flags |= 0x00000023;

                    if (flags != 0)
                    {
                        void* token;
                        void* processHandle = NativeInterop.GetCurrentProcess();
                        NativeInterop.OpenProcessToken(processHandle, NativeInterop.TOKEN_ADJUST_PRIVILEGES | NativeInterop.TOKEN_QUERY, &token);

                        TOKEN_PRIVILEGES tk = new()
                        {
                            PrivilegeCount = 1,
                            Privileges = new[]
                            {
                            new LUID_AND_ATTRIBUTES { Attributes = 0x00000002 }
                        }
                        };
                        NativeInterop.LookupPrivilegeValue(null, "SeShutdownPrivilege", ref tk.Privileges[0].Luid);
                        NativeInterop.AdjustTokenPrivileges(token, false, ref tk, 0, null, null);

                        return NativeInterop.InitiateShutdown(null, null, 0, flags, 0x80000000) == 0;
                    }

                    return false;
                }, delegate
                {
                    uint? code = null;

                    if (mode.HasFlag(ShutdownMode.SD_REBOOT))
                        code = 0x01234567;
                    else if (mode.HasFlag(ShutdownMode.SD_POWERDOWN))
                        code = 0x4321FEDC;
                    else if (mode.HasFlag(ShutdownMode.SD_SHUTDOWN))
                        code = 0xCDEF0123;
                    else if (mode is ShutdownMode.SD_STANDBY or ShutdownMode.SD_HIBERNATE)
                        code = 0xD000FCE2;

                    return code is uint msg && NativeInterop.Linux__reboot(0xfee1dead, 0x28121969, msg, null) == 0;
                });

                return Variant.FromBoolean(success);
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(ex.HResult);
            }
        }

        internal static FunctionReturnValue Sin(CallFrame frame, Variant[] args) => (Variant)Math.Sin((double)args[0].ToNumber());

        internal static FunctionReturnValue String(CallFrame frame, Variant[] args) => (Variant)args[0].ToString();

        internal static FunctionReturnValue StringToBinary(CallFrame frame, Variant[] args) => (Variant)From.String(args[0].ToString(), (int)args[1] switch
        {
            1 => Encoding.GetEncoding(1252),
            2 => Encoding.Unicode,
            3 => Encoding.BigEndianUnicode,
            4 => Encoding.UTF8,
            _ => BytewiseEncoding.Instance,
        }).Data;

        internal static FunctionReturnValue Sqrt(CallFrame frame, Variant[] args) => (Variant)Math.Sqrt((double)args[0].ToNumber());

        internal static unsafe FunctionReturnValue SRandom(CallFrame frame, Variant[] args)
        {
            frame.Interpreter.ResetRandom((int)args[0]);

            return Variant.Null;
        }

        internal static FunctionReturnValue SetExtended(CallFrame frame, Variant[] args) => frame.SetExtended((int)args[0], args[1]);

        internal static FunctionReturnValue SetError(CallFrame frame, Variant[] args) => frame.SetError((int)args[0], (int)args[1], args[2]);

        internal static FunctionReturnValue Sleep(CallFrame frame, Variant[] args)
        {
            Thread.Sleep((int)args[0]);

            return Variant.Null;
        }


        private Process? _vlc_process = null;

        internal FunctionReturnValue SoundPlay(CallFrame frame, Variant[] args)
        {
            string path = args[0].ToString();

            if (_vlc_process is Process process)
            {
                process.Kill();
                process.WaitForExit();
                process.Dispose();
                _vlc_process = null;
            }

            if (!string.IsNullOrEmpty(path))
                try
                {
                    bool wait = args[1].ToBoolean();
                    ProcessStartInfo pfi = new(path)
                    {
                        Arguments = $"-I null --play-and-exit --qt-start-minimized \"{path}\"",
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(path)!,
                        FileName = NativeInterop.DoPlatformDependent("vlc.exe", "cvlc"),
                        Verb = "OPEN",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    process = new() { StartInfo = pfi };
                    process.Start();

                    if (wait)
                    {
                        process.WaitForExit();
                        process.Dispose();
                    }
                    else
                        _vlc_process = process;
                }
                catch
                {
                    return InterpreterError.WellKnown(frame.CurrentLocation, "error.vlc_required");
                }

            return Variant.True;
        }

        #endregion
        #region STRING...

        internal static FunctionReturnValue StringAddCR(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().Replace("\n", "\r\n", StringComparison.InvariantCultureIgnoreCase);

        internal static FunctionReturnValue StringCompare(CallFrame frame, Variant[] args) => (Variant)string.Compare(args[0].ToString(), args[1].ToString(), ((int)args[2]) switch
        {
            1 => StringComparison.CurrentCulture,
            2 => StringComparison.InvariantCultureIgnoreCase,
            _ => StringComparison.CurrentCultureIgnoreCase,
        });

        internal static FunctionReturnValue StringFormat(CallFrame frame, Variant[] args) => (Variant)StringFormatter.FormatString(args[0].ToString(), args[1..]);

        internal static FunctionReturnValue StringFromASCIIArray(CallFrame frame, Variant[] args)
        {
            if (args[0].Type is not VariantType.Array)
                return FunctionReturnValue.Error(1, 0, Variant.EmptyString);

            try
            {
                Variant[] data = args[0].ToArray(frame.Interpreter);
                int start = (int)args[1];
                int end = (int)args[2];
                int enc = (int)args[3];

                if (end < 0)
                    end = int.MaxValue;

                IEnumerable<char>? chars = data[start..Math.Min(end, data.Length)].Select(v => unchecked((char)(int)v));

                return Variant.FromString(enc switch
                {
                    2 => Encoding.UTF8.GetString(chars.ToArray(c => (byte)c)),
                    1 => new string(chars.ToArray(c => (char)(byte)c)),
                    _ => new string(chars.ToArray()),
                });
            }
            catch
            {
                return FunctionReturnValue.Error(2, 0, Variant.EmptyString);
            }
        }

        internal static FunctionReturnValue StringInStr(CallFrame frame, Variant[] args)
        {
            string input = args[0].ToString();
            string substr = args[1].ToString();
            StringComparison mode = ((int)args[2]) switch
            {
                1 => StringComparison.InvariantCulture,
                2 => StringComparison.InvariantCultureIgnoreCase,
                _ => StringComparison.CurrentCultureIgnoreCase,
            };
            int occurence = (int)args[3];
            int start = (int)args[4] - 1;
            int count = (int)args[5];

            if (count < 0)
                count = int.MaxValue;

            if (start >= 0 && start < input.Length)
                try
                {
                    count = Math.Min(count, input.Length - start);

                    int sindex = start + (occurence < 0 ? count : 0);

                    while (occurence != 0)
                    {
                        int match = occurence < 0 ? input.LastIndexOf(substr, sindex, count - sindex + start, mode) : input.IndexOf(substr, sindex, count - sindex + start, mode);

                        if (match < 0)
                            break;
                        else if ((occurence += occurence < 0 ? 1 : -1) == 0)
                            return Variant.FromNumber(match + 1);
                        else
                            sindex = match;
                    }

                    return Variant.Zero;
                }
                catch
                {
                }

            return FunctionReturnValue.Error(1);
        }

        internal static FunctionReturnValue StringLeft(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();
            int c = Math.Max(0, (int)args[1]);

            return c < s.Length ? s[..c] : Variant.EmptyString;
        }

        internal static FunctionReturnValue StringLen(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().Length;

        internal static FunctionReturnValue StringLower(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().ToLowerInvariant();

        internal static FunctionReturnValue StringMid(CallFrame frame, Variant[] args)
        {
            string str = args[0].ToString();
            int start = (int)args[1] - 1;
            int len = (int)args[2];

            if (start < 0 || start >= str.Length)
                return Variant.EmptyString;

            len = str.Length - start;

            return (Variant)(len < 0 ? "" : str.Substring(start, len));
        }

        internal static FunctionReturnValue StringRegExp(CallFrame frame, Variant[] args)
        {
            Regex regex;
            string input = args[0].ToString();
            int flag = (int)args[2];
            int offset = (int)args[3];

            try
            {
                regex = new Regex(args[1].ToString());
            }
            catch
            {
                return FunctionReturnValue.Error(2, 0);
            }

            Variant to_array(Match match)
            {
                Variant[] groups = match.Groups.Cast<Group>().ToArray(g => (Variant)g.Value);

                if (flag is 1 or 3 && groups.Length > 1)
                    groups = groups[1..];

                return Variant.FromArray(frame.Interpreter, groups);
            }

            if (flag is 1 or 2)
            {
                if (input.Match(regex, out Match m))
                    return to_array(m);
                else
                    return FunctionReturnValue.Error(1);
            }
            else if (flag is 3 or 4)
            {
                Variant result = Variant.FromArray(frame.Interpreter, regex.Matches(input).Select(to_array));

                if (result.Length > 0)
                    return result;
                else
                    return FunctionReturnValue.Error(1);
            }
            else
                return (Variant)regex.IsMatch(input);
        }

        internal static FunctionReturnValue StringRegExpReplace(CallFrame frame, Variant[] args)
        {
            Regex regex;
            string input = args[0].ToString();
            string replacement = args[2].ToString();
            int count = (int)args[3];
            bool all = count == 0;
            int i = 0;

            try
            {
                regex = new Regex(args[1].ToString());
            }
            catch
            {
                return FunctionReturnValue.Error(2, 0);
            }

            while (i < count || all)
                if (input.Match(regex, out Match m))
                {
                    input = input[..m.Index] + replacement + input[(m.Index + m.Length)..];
                    ++i;
                }
                else
                    break;

            if (i == 0)
                return FunctionReturnValue.Error(input, 1, i);
            else
                return FunctionReturnValue.Success(input, i);
        }

        internal static FunctionReturnValue StringReplace(CallFrame frame, Variant[] args)
        {
            string input = args[0].ToString();
            string search = args[1].ToString();
            string replace = args[2].ToString();
            int occurence = (int)args[3];
            StringComparison mode = ((int)args[4]) switch
            {
                1 => StringComparison.InvariantCulture,
                2 => StringComparison.InvariantCultureIgnoreCase,
                _ => StringComparison.CurrentCultureIgnoreCase,
            };
            int count = 0;
            int sindex = 0;

            if (occurence == 0)
                occurence = int.MaxValue;
            else if (occurence < 0)
                sindex = input.Length;

            while (occurence != 0)
            {
                int index = occurence > 0 ? input.IndexOf(search, sindex, mode) : input.LastIndexOf(search, sindex, mode);

                if (index == -1)
                    break;

                if (occurence < 0)
                {
                    ++occurence;
                    sindex = index;
                }
                else
                {
                    --occurence;
                    sindex = index + replace.Length;
                }

                input = input[..index] + replace + input[(index + search.Length)..];
                ++count;
            }

            return FunctionReturnValue.Success(input, count);
        }

        internal static FunctionReturnValue StringReverse(CallFrame frame, Variant[] args) => (Variant)new string(args[0].ToString().Reverse().ToArray());

        internal static FunctionReturnValue StringRight(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            return Variant.FromString(((int)args[1]) switch
            {
                < 0 => "",
                int i when i >= s.Length => s,
                int i => s[^i..],
            });
        }

        internal static FunctionReturnValue StringSplit(CallFrame frame, Variant[] args)
        {
            string input = args[0].ToString();
            string delim = args[1].ToString();
            int flags = (int)args[2];

            string[] fragments = (flags & 1) != 0 ? input.Split(delim) : input.Split(delim.ToCharArray());

            if (string.IsNullOrEmpty(delim))
                fragments = input.ToArray(c => c.ToString());

            IEnumerable<Variant> array = fragments.Select(Variant.FromString);

            if ((flags & 2) != 0)
                array = array.Prepend(fragments.Length);

            return Variant.FromArray(frame.Interpreter, array);
        }

        internal static FunctionReturnValue StringStripCR(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().Replace("\r", "", StringComparison.InvariantCulture);

        internal static FunctionReturnValue StringStripWS(CallFrame frame, Variant[] args)
        {
            string str = args[0].ToString();
            StringStripFlags flags = (StringStripFlags)(int)args[1];

            if (flags.HasFlag(StringStripFlags.STR_STRIPALL))
                flags = StringStripFlags.STR_STRIPLEADING | StringStripFlags.STR_STRIPSPACES | StringStripFlags.STR_STRIPTRAILING;

            if (flags.HasFlag(StringStripFlags.STR_STRIPLEADING))
                str = str.TrimStart();

            if (flags.HasFlag(StringStripFlags.STR_STRIPTRAILING))
                str = str.TrimEnd();

            if (flags.HasFlag(StringStripFlags.STR_STRIPSPACES))
                str = REGEX_WS.Replace(str, "");

            return (Variant)str;
        }

        internal static FunctionReturnValue StringToASCIIArray(CallFrame frame, Variant[] args)
        {
            string str = args[0].ToString();
            int start = (int)args[1];

            if (start >= 0 && start < str.Length)
            {
                int len = (int)args[2];

                if (len < 0 || len - start >= str.Length)
                    len = str.Length - start;

                int enc = (int)args[3];
                IEnumerable<Variant> arr;

                if (enc == 2)
                    arr = Encoding.UTF8.GetBytes(str).Select(b => Variant.FromNumber(b));
                else
                    arr = str.Select(c => Variant.FromNumber(enc == 1 ? (byte)c : (int)c));

                return Variant.FromArray(frame.Interpreter, arr);
            }

            return Variant.EmptyString;
        }

        internal static FunctionReturnValue StringTrimLeft(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();
            int c = Math.Max(0, (int)args[1]);

            return c < s.Length ? s[c..] : Variant.EmptyString;
        }

        internal static FunctionReturnValue StringTrimRight(CallFrame frame, Variant[] args)
        {
            string s = args[0].ToString();

            return Variant.FromString(((int)args[1]) switch
            {
                < 0 => "",
                int i when i >= s.Length => s,
                int i => s[..^i],
            });
        }

        internal static FunctionReturnValue StringUpper(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().ToUpperInvariant();

        internal static FunctionReturnValue StringIsDigit(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsDigit);

        internal static FunctionReturnValue StringIsAlNum(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsLetterOrDigit);

        internal static FunctionReturnValue StringIsAlpha(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsLetter);

        internal static FunctionReturnValue StringIsASCII(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(c => c < 0x80);

        internal static FunctionReturnValue StringIsLower(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsLower);

        internal static FunctionReturnValue StringIsSpace(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsWhiteSpace);

        internal static FunctionReturnValue StringIsUpper(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All(char.IsUpper);

        internal static FunctionReturnValue StringIsXDigit(CallFrame frame, Variant[] args) => (Variant)args[0].ToString().All("0123456789abcdefABCDEF".Contains);

        #endregion
        #region TCP...

        internal static FunctionReturnValue TCPAccept(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out TCPHandle.Case0? handle))
                {
                    TcpListener listener = handle.Item;

                    while (!listener.Pending())
                        Thread.Sleep(0);

                    TcpClient client = listener.AcceptTcpClient();

                    return frame.Interpreter.GlobalObjectStorage.Store<TCPHandle>(client);
                }
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(-2, ex.HResult, Variant.Zero);
            }

            return FunctionReturnValue.Error(-1, 1, Variant.Zero);
        }

        internal static FunctionReturnValue TCPCloseSocket(CallFrame frame, Variant[] args)
        {

            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out TCPHandle? handle))
                {
                    if (handle.Is(out TcpClient? client))
                        client.Close();
                    else if (handle.Is(out TcpListener? listener))
                        listener.Stop();

                    frame.Interpreter.GlobalObjectStorage.Delete(args[0]);

                    return Variant.True;
                }
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(Variant.False, ex.HResult, Variant.Zero);
            }

            return FunctionReturnValue.Error(1);
        }

        internal static FunctionReturnValue TCPConnect(CallFrame frame, Variant[] args)
        {
            try
            {
                TcpClient listener = new(args[0].ToString(), (int)args[1]);

                return frame.Interpreter.GlobalObjectStorage.Store<TCPHandle>(listener);
            }
            catch (Exception e)
            {
                int err = -2;

                if (e is ArgumentOutOfRangeException)
                    err = 2;
                else if (e is SocketException se)
                    err = (int)se.SocketErrorCode;

                return FunctionReturnValue.Error(Variant.Zero, err, Variant.Zero);
            }
        }

        internal static FunctionReturnValue TCPListen(CallFrame frame, Variant[] args)
        {
            try
            {
                TcpListener listener = new(IPAddress.Parse(args[0].ToString()), (int)args[1]);
                int max_pending = (int)args[2];

                if (max_pending <= 0)
                    max_pending = short.MaxValue;

                listener.Start();

                return frame.Interpreter.GlobalObjectStorage.Store<TCPHandle>(listener);
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(Variant.Zero, ex is ArgumentOutOfRangeException ? 2 : 1, Variant.Zero);
            }
        }

        internal static FunctionReturnValue TCPNameToIP(CallFrame frame, Variant[] args)
        {
            string addr = args[0].ToString();

            addr = Dns.GetHostEntry(addr).AddressList.FirstOrDefault()?.ToString() ?? addr;

            return (Variant)addr;
        }

        internal static FunctionReturnValue TCPRecv(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out TCPHandle.Case1? handle))
                {
                    TcpClient client = handle.Item;
                    List<byte> resp = new();

                    int max_length = (int)args[1];
                    bool binary = args[2].ToBoolean();
                    byte[] bytes;
                    int count = 0;

                    using (NetworkStream ns = client.GetStream())
                        do
                        {
                            bytes = new byte[client.ReceiveBufferSize];
                            count = ns.Read(bytes, 0, client.ReceiveBufferSize);

                            resp.AddRange(bytes.Take(count));
                        }
                        while ((count >= bytes.Length) && (resp.Count <= max_length));

                    bytes = resp.Take(max_length).ToArray();
                    binary |= bytes.Contains(default);

                    return FunctionReturnValue.Success(binary ? Variant.FromBinary(bytes) : From.Bytes(bytes).ToString(), bytes.Length == 0);
                }
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(Variant.EmptyString, ex.HResult, 1);
            }

            return FunctionReturnValue.Error(Variant.EmptyString, -1, 1);
        }

        internal static FunctionReturnValue TCPSend(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0].TryResolveHandle(frame.Interpreter, out TCPHandle.Case1? handle))
                {
                    TcpClient client = handle.Item;
                    byte[] bytes = args[1].ToBinary();

                    using (NetworkStream ns = client.GetStream())
                        ns.Write(bytes, 0, bytes.Length);

                    return FunctionReturnValue.Success(bytes.Length);
                }
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(Variant.Zero, ex.HResult, Variant.Zero);
            }

            return FunctionReturnValue.Error(Variant.Zero, 1, Variant.Zero);
        }

        internal static FunctionReturnValue TCPShutdown(CallFrame frame, Variant[] args) => Variant.True;

        internal static FunctionReturnValue TCPStartup(CallFrame frame, Variant[] args) => Variant.True;

        #endregion
        #region T...

        internal static FunctionReturnValue Tan(CallFrame frame, Variant[] args) => (Variant)Math.Tan((double)args[0].ToNumber());

        internal static FunctionReturnValue TimerInit(CallFrame frame, Variant[] args)
        {
            Stopwatch sw = new();

            sw.Start();

            return frame.Interpreter.GlobalObjectStorage.Store(sw);
        }

        internal static FunctionReturnValue TimerDiff(CallFrame frame, Variant[] args) =>
            (Variant)(args[0].TryResolveHandle(frame.Interpreter, out Stopwatch? sw) ? sw.ElapsedMilliseconds : -1);

        #endregion
        #region U...

        internal static FunctionReturnValue UBound(CallFrame frame, Variant[] args)
        {
            Variant obj = args[0];
            int count = (int)args[1] - 1;

            while (count > 0 && obj.Type is VariantType.Array)
            {
                obj = obj.ToArray(frame.Interpreter).FirstOrDefault();
                --count;
            }

            return (Variant)obj.Length;
        }

        #endregion
        #region UDP...

        internal static FunctionReturnValue UDPListen(CallFrame frame, Variant[] args)
        {
            try
            {
                IPAddress addr = IPAddress.Parse(args[0].ToString());
                int port = (int)args[1];
                IPEndPoint iep = new(addr, port);
                UDPServer server = new(iep);
                Variant handle = frame.Interpreter.GlobalObjectStorage.Store(server);

                return Variant.FromArray(
                    frame.Interpreter,
                    Variant.True,
                    handle,
                    addr.ToString(),
                    port
                );
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(Variant.Zero, ex switch
                {
                    ArgumentOutOfRangeException => 2,
                    FormatException => 1,
                    _ => ex.HResult
                }, Variant.Zero);
            }
        }

        internal static FunctionReturnValue UDPCloseSocket(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0] is { Type: VariantType.Array } &&
                    args[0].ToArray(frame.Interpreter) is Variant[] arr &&
                    arr[0].TryResolveHandle(frame.Interpreter, out UDPBase? udp))
                {
                    udp.Close();

                    frame.Interpreter.GlobalObjectStorage.Delete(arr[0]);

                    return Variant.True;
                }

                return FunctionReturnValue.Error(-3);
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(ex.HResult);
            }
        }

        internal static FunctionReturnValue UDPOpen(CallFrame frame, Variant[] args)
        {
            string addr = args[0].ToString();
            int port = (int)args[1];

            try
            {
                UDPClient client = UDPClient.ConnectTo(args[0].ToString(), (int)args[1], (bool)args[2]);
                Variant handle = frame.Interpreter.GlobalObjectStorage.Store(client);

                return Variant.FromArray(
                    frame.Interpreter,
                    Variant.True,
                    handle,
                    addr,
                    port
                );
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(Variant.FromArray(
                    frame.Interpreter,
                    Variant.False,
                    Variant.Null,
                    addr,
                    port
                ), ex.HResult, Variant.Zero);
            }
        }

        internal static FunctionReturnValue UDPRecv(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0] is { Type: VariantType.Array } &&
                    args[0].ToArray(frame.Interpreter) is Variant[] arr &&
                    arr[1].TryResolveHandle(frame.Interpreter, out UDPBase? udp))
                {
                    bool binary = ((int)args[2] & 1) != 0;
                    bool array = ((int)args[2] & 2) != 0;
                    int max_length = (int)args[1];

                    (IPEndPoint sender, byte[] bytes) = udp.Receive();

                    if (max_length < 0 || max_length > bytes.Length)
                        max_length = bytes.Length;

                    bytes = bytes[..max_length];
                    binary |= bytes.Contains(default);

                    Variant response = binary ? Variant.FromBinary(bytes) : From.Bytes(bytes).ToString();

                    if (array)
                        return Variant.FromArray(
                            frame.Interpreter,
                            response,
                            sender.Address.ToString(),
                            sender.Port
                        );
                    else
                        return response;
                }

                return FunctionReturnValue.Error(Variant.EmptyString, -3, Variant.Zero);
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(Variant.EmptyString, ex.HResult, 1);
            }
        }

        internal static FunctionReturnValue UDPSend(CallFrame frame, Variant[] args)
        {
            try
            {
                if (args[0] is { Type: VariantType.Array } &&
                    args[0].ToArray(frame.Interpreter) is Variant[] arr &&
                    arr[1].TryResolveHandle(frame.Interpreter, out UDPBase? udp))
                {
                    byte[] data = args[1].ToBinary();
                    int count = 0;

                    if (udp is UDPClient client)
                        count = client.Send(data);
                    else if (udp is UDPServer server)
                        count = server.Reply(data, new IPEndPoint(IPAddress.Parse(arr[2].ToString()), (int)arr[3]));

                    return (Variant)count;
                }

                return FunctionReturnValue.Error(-3);
            }
            catch (Exception ex)
            {
                return FunctionReturnValue.Error(ex.HResult);
            }
        }

        internal static FunctionReturnValue UDPShutdown(CallFrame frame, Variant[] args) => Variant.True;

        internal static FunctionReturnValue UDPStartup(CallFrame frame, Variant[] args) => Variant.True;

        #endregion
        #region V, W, X, Y, Z

        internal static FunctionReturnValue VarGetType(CallFrame frame, Variant[] args)
        {
            string gettype(Variant var) => var.Type switch
            {
                VariantType.Boolean => "Bool",
                VariantType.Null or VariantType.Default => "Keyword",
                VariantType.String or VariantType.Binary or VariantType.Array or VariantType.Map or VariantType.Function => var.Type.ToString(),
                VariantType.Reference => gettype(var.ReferencedVariable!.Value),
                VariantType.COMObject => "Object", // NETObject
                VariantType.Number when (int)var == (double)var => "Int32",
                VariantType.Number => "Double",
                VariantType.Handle => "Ptr",
                // TODO : ??
            };

            return (Variant)gettype(args[0]);
        }

        #endregion
        #endregion
        #region HELPER FUNCTIONS

        private static Union<InterpreterError, AU3CallFrame> GetAu3Caller(CallFrame? frame, string funcname)
        {
            AU3CallFrame? caller = null;

            while (frame is { })
                if (frame is AU3CallFrame au3)
                    caller = au3;
                else
                    frame = frame.CallerFrame;

            if (caller is { })
                return caller;
            else
                return InterpreterError.WellKnown(null, "error.au3_caller_only", funcname);
        }

        private static DriveInfo? GetDriveByPath(string path)
        {
            try
            {
                string dir = Path.GetFullPath(path);

                return DriveInfo.GetDrives().FirstOrDefault(d => Path.GetFullPath(d.RootDirectory.FullName).Equals(dir));
            }
            catch
            {
                return null;
            }
        }

        private static Process? GetProcess(Variant variant)
        {
            try
            {
                return Process.GetProcessById((int)variant);
            }
            catch
            {
                return Process.GetProcessesByName(variant.ToString()).OrderByDescending(p => p.Id)?.FirstOrDefault();
            }
        }

        private static unsafe void GetRegistryPath(string key, out void* hk_base, out string path)
        {
            string[] tokens = key.Split('\\');
            hk_base = (void*)(int)(tokens[0].ToUpperInvariant() switch
            {
                "HKEY_LOCAL_MACHINE" or "HKLM" => RegPredefinedkeys.HKEY_LOCAL_MACHINE,
                "HKEY_USERS" or "HKU" => RegPredefinedkeys.HKEY_USERS,
                "HKEY_CURRENT_USER" or "HKCU" => RegPredefinedkeys.HKEY_CURRENT_USER,
                "HKEY_CLASSES_ROOT" or "HKCR" => RegPredefinedkeys.HKEY_CLASSES_ROOT,
                "HKEY_CURRENT_CONFIG" or "HKCC" => RegPredefinedkeys.HKEY_CURRENT_CONFIG,
                "HKEY_DYN_DATA" or "HKDD" => RegPredefinedkeys.HKEY_DYN_DATA,
                "HKEY_PERFORMANCE_DATA" or "HKPD" => RegPredefinedkeys.HKEY_PERFORMANCE_DATA,
                _ => default,
            });
            path = tokens.Skip(1).StringJoin("\\");
        }

        private static string GetAttributeString(FileSystemInfo info)
        {
            StringBuilder sb = new();
            FileAttributes attr = info.Attributes;
            Dictionary<FileAttributes, char> dic = new()
            {
                [FileAttributes.ReadOnly] = 'R',
                [FileAttributes.Hidden] = 'H',
                [FileAttributes.System] = 'S',
                [FileAttributes.Directory] = 'D',
                [FileAttributes.Archive] = 'A',
                [FileAttributes.Normal] = 'N',
                [FileAttributes.Temporary] = 'T',
                [FileAttributes.Compressed] = 'C',
                [FileAttributes.Offline] = 'O',
                [FileAttributes.Encrypted] = 'X',
                // the following are optional
                [FileAttributes.Device] = 'V',
                [FileAttributes.IntegrityStream] = 'I',
                [FileAttributes.ReparsePoint] = 'P',
                [FileAttributes.NotContentIndexed] = 'Z',
            };

            foreach (FileAttributes key in dic.Keys)
                if (attr.HasFlag(key))
                    sb.Append(dic[key]);

            return sb.ToString();
        }

        #endregion
        #region HELPER CLASSES

        private sealed class FileSearchHandle
        {
            public IEnumerator<FileSystemInfo> Enumerator {get; }


            public FileSearchHandle(FileSystemInfo[] files) => Enumerator = (IEnumerator<FileSystemInfo>)files.GetEnumerator();

            public override string ToString() => Enumerator.Current?.ToString();
        }

        private sealed class FileHandle
            : IDisposable
        {
            public FileOpenFlags Flags { get; }
            public FileStream FileStream { get; }
            public StreamReader StreamReader { get; }
            public StreamWriter? StreamWriter { get; }
            public Encoding Encoding { get; }


            public FileHandle(FileStream fs, FileOpenFlags flags)
            {
                Flags = flags;
                FileStream = fs;
                Encoding = flags.HasFlag(FileOpenFlags.FO_UNICODE) || flags.HasFlag(FileOpenFlags.FO_UTF16_LE) ? Encoding.Unicode :
                           flags.HasFlag(FileOpenFlags.FO_UTF16_BE) ? Encoding.BigEndianUnicode :
                           flags.HasFlag(FileOpenFlags.FO_UTF8) ? new UTF8Encoding(true) :
                           flags.HasFlag(FileOpenFlags.FO_UTF8_NOBOM) ? new UTF8Encoding(false) :
                           flags.HasFlag(FileOpenFlags.FO_ANSI) ? Encoding.GetEncoding(1252) :
                           flags.HasFlag(FileOpenFlags.FO_UTF16_LE_NOBOM) ? new UnicodeEncoding(false, false) :
                           flags.HasFlag(FileOpenFlags.FO_UTF16_BE_NOBOM) ? new UnicodeEncoding(true, false) : Encoding.Default;

                StreamReader = new(fs, Encoding);

                if (fs.CanWrite)
                    StreamWriter = new(fs, Encoding);
            }

            public void Dispose()
            {
                StreamWriter?.Close();
                StreamWriter?.Dispose();
                StreamReader.Close();
                StreamReader.Dispose();
                FileStream.Close();
                FileStream.Dispose();
            }

            public override string ToString() => $"{FileStream} ({Flags}, {Encoding})";
        }

        private sealed unsafe class LibraryHandle
            : IDisposable
        {
            public bool IsDisposed { get; private set; }
            public nint Handle { get; private set; }
            public string Path { get; }
            public bool IsLoaded => Handle != 0;


            public LibraryHandle(string path) => Path = path;

            ~LibraryHandle() => Dispose(false);

            private void Dispose(bool _)
            {
                if (!IsDisposed)
                {
                    if (IsLoaded)
                        NativeInterop.DoPlatformDependent(
                            () => NativeInterop.FreeLibrary(Handle),
                            () => NativeInterop.Linux__dlclose(Handle),
                            () => NativeInterop.MacOS__dlclose(Handle)
                        );

                    Handle = default;
                    IsDisposed = true;
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            public void LoadLibrary() => Handle = NativeInterop.DoPlatformDependent(
                () => NativeInterop.LoadLibrary(Path),
                () => NativeInterop.Linux__dlopen(Path),
                () => NativeInterop.MacOS__dlopen(Path)
            );

            public override string ToString() => $"0x{(long)Handle:x16}: {Path}";
        }

        private sealed class InetHandle
            : IDisposable
        {
            private readonly CancellationTokenSource _cts;
            private readonly WebClient _wc;
            private readonly string _file;
            private readonly string _uri;
            private volatile bool _complete = false;
            private volatile bool _running = false;
            private volatile bool _error = false;
            private Task<byte[]>? _task;


            public bool Complete => _complete;

            public bool Running => _running;

            public bool Error => _error;


            // TODO : options, download monitor


            public InetHandle(string uri, string filename, int options)
            {
                _cts = new CancellationTokenSource();
                _wc = new WebClient();
                _file = filename;
                _uri = uri;

                // todo : options
            }

            public void Dispose()
            {
                Cancel();

                _wc.Dispose();
                _cts.Dispose();
                _task?.Dispose();
            }

            public void Cancel()
            {
                if (_running)
                {
                    _running = false;
                    _complete = false;
                    _error = true;
                    _cts.Cancel();
                }
            }

            public void Start() => _task = Task.Factory.StartNew(delegate
            {
                byte[] data = Array.Empty<byte>();

                try
                {
                    _error = false;
                    _complete = false;
                    _running = true;
                    data = _wc.DownloadData(_uri);
                    _complete = true;

                    From.Bytes(data).ToFile(_file);
                }
                catch
                {
                    _error = true;
                }
                finally
                {
                    _running = false;
                }

                return data;
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            public override string ToString() => $"uri={_uri}, file={_file}, running={_running}, error={_error}";
        }

        private abstract class UDPBase
            : IDisposable
        {
            protected UdpClient _client = new();


            protected internal UDPBase()
            {
            }

            public (IPEndPoint sender, byte[] data) Receive()
            {
                IPEndPoint? sender = null;
                byte[] data = _client.Receive(ref sender);

                return (sender, data);
            }

            public void Close() => _client.Close();

            public void Dispose() => _client.Dispose();
        }

        private sealed class UDPServer
            : UDPBase
        {
            private readonly IPEndPoint _listenon; // TODO : ??


            public UDPServer(IPEndPoint endpoint)
            {
                _listenon = endpoint;
                _client = new UdpClient(endpoint) { EnableBroadcast = true };
            }

            public int Reply(byte[] bytes, IPEndPoint endpoint) => _client.Send(bytes, bytes.Length, endpoint);
        }

        private sealed class UDPClient
            : UDPBase
        {
            private UDPClient()
            {
            }

            public int Send(byte[] data) => _client.Send(data, data.Length);

            public static UDPClient ConnectTo(string hostname, int port, bool enable_broadcast)
            {
                UDPClient connection = new();

                connection._client.EnableBroadcast = enable_broadcast;
                connection._client.Connect(hostname, port);

                return connection;
            }
        }

        #endregion
        #region HELPER ENUMS

        [Flags]
        public enum ShutdownMode
        {
            SD_LOGOFF = 0,
            SD_SHUTDOWN = 1,
            SD_REBOOT = 2,
            SD_FORCE = 4,
            SD_POWERDOWN = 8,
            SD_FORCEHUNG = 16,
            SD_STANDBY = 32,
            SD_HIBERNATE = 64,
        }

        [Flags]
        private enum AssignFlags
            : int
        {
            Create = 0,
            ForceLocal = 1,
            ForceGlobal = 2,
            ExistFail = 4
        }

        [Flags]
        private enum FileOpenFlags
        {
            FO_READ = 0,
            FO_APPEND = 1,
            FO_OVERWRITE = 2,
            FO_CREATEPATH = 8,
            FO_BINARY = 16,
            FO_UNICODE = 32,
            FO_UTF16_LE = FO_UNICODE,
            FO_UTF16_BE = 64,
            FO_UTF8 = 128,
            FO_UTF8_NOBOM = 256,
            FO_ANSI = 512,
            FO_UTF16_LE_NOBOM = 1024,
            FO_UTF16_BE_NOBOM = 2048,
            FO_FULLFILE_DETECT = 16384,
        }

        [Flags]
        private enum StringStripFlags
            : int
        {
            STR_STRIPLEADING = 1,
            STR_STRIPTRAILING = 2,
            STR_STRIPSPACES = 4,
            STR_STRIPALL = 8,
        }

        #endregion
    }
}
