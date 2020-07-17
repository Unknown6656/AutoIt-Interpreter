using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Reflection;
using System;

namespace AutoItCoreLibrary
{
    public sealed class COM
    {
        public TypeInformation Information { get; }
        public object Instance { get; }
        public Type Type { get; }


        public COM(object com)
        {
            Instance = com;
            Type = com.GetType();
            Information = TypeInformation.GetTypeInformation(com);
        }

        public COM(Type t, params object[] args)
        {
            Type = t;
            Instance = Activator.CreateInstance(t, args ?? new object[0]);
            Information = TypeInformation.GetTypeInformation(Instance);
        }

        public override string ToString() => $"[{Type.FullName}{(Information?.Name is string s ? "::" + s : "")}] {Instance}";

        public override int GetHashCode() => Instance?.GetHashCode() ?? Type?.GetHashCode() ?? 0;

        public override bool Equals(object obj) => obj is COM other && (Instance?.Equals(other.Instance) ?? false);

        public object Invoke(string name, params object[] args) => Type.InvokeMember(name, BindingFlags.InvokeMethod, null, Instance, args ?? new object[0]);
    }

    public sealed class TypeInformation
    {
        public string Name { get; }
        public string Description { get; }
        public int HelpContext { get; }
        public string HelpPath { get; }


        private TypeInformation(ITypeInfo nfo)
        {
            nfo.GetDocumentation(-1, out string name, out string desc, out int ctx, out string path);

            Name = name.StartsWith("_") ? name.Substring(1) : name;
            Description = desc ?? "";
            HelpPath = path ?? "";
            HelpContext = ctx;
        }

        public static TypeInformation GetTypeInformation(object com) => com is IDispatch dispatch ? new TypeInformation(dispatch.GetTypeInfo(0, 1033)) : null;
    }

    [ComImport, Guid("00020400-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDispatch
    {
        int GetTypeInfoCount();

        [return: MarshalAs(UnmanagedType.Interface)]
        ITypeInfo GetTypeInfo(uint iTInfo, uint lcid);

        void GetIDsOfNames(ref Guid riid, [MarshalAs(UnmanagedType.LPArray)] string[] rgszNames, uint cNames, uint lcid, [Out, MarshalAs(UnmanagedType.LPArray)] int[] rgDispId);
    }

    [ComImport, Guid("0000000b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public unsafe interface IStorage
    {
        void CreateStream(string pwcsName, uint grfMode, uint reserved1, uint reserved2, out IStream ppstm);
        void OpenStream(string pwcsName, void* reserved1, uint grfMode, uint reserved2, out IStream ppstm);
        void CreateStorage(string pwcsName, uint grfMode, uint reserved1, uint reserved2, out IStorage ppstg);
        void OpenStorage(string pwcsName, IStorage pstgPriority, uint grfMode, void* snbExclude, uint reserved, out IStorage ppstg);
        void CopyTo(uint ciidExclude, Guid rgiidExclude, void* snbExclude, IStorage pstgDest);
        void MoveElementTo(string pwcsName, IStorage pstgDest, string pwcsNewName, uint grfFlags);
        void Commit(uint grfCommitFlags);
        void Revert();
        void EnumElements(uint reserved1, void* reserved2, uint reserved3, out IEnumSTATSTG ppenum);
        void DestroyElement(string pwcsName);
        void RenameElement(string pwcsOldName, string pwcsNewName);
        void SetElementTimes(string pwcsName, FILETIME pctime, FILETIME patime, FILETIME pmtime);
        void SetClass(Guid clsid);
        void SetStateBits(uint grfStateBits, uint grfMask);
        void Stat(out STATSTG pstatstg, uint grfStatFlag);
    }

    [ComImport, Guid("0000000d-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumSTATSTG
    {
        [PreserveSig]
        uint Next(uint celt, [MarshalAs(UnmanagedType.LPArray), Out] STATSTG[] rgelt, out uint pceltFetched);
        void Skip(uint celt);
        void Reset();

        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumSTATSTG Clone();
    }

    [ComImport, Guid("00000112-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleObject
    {
        void SetClientSite(IOleClientSite pClientSite);
        void GetClientSite(ref IOleClientSite ppClientSite);
        void SetHostNames(object szContainerApp, object szContainerObj);
        void Close(uint dwSaveOption);
        void SetMoniker(uint dwWhichMoniker, object pmk);
        void GetMoniker(uint dwAssign, uint dwWhichMoniker, object ppmk);
        void InitFromData(IDataObject pDataObject, bool fCreation, uint dwReserved);
        void GetClipboardData(uint dwReserved, ref IDataObject ppDataObject);
        void DoVerb(uint iVerb, uint lpmsg, object pActiveSite, uint lindex, uint hwndParent, uint lprcPosRect);
        void EnumVerbs(ref object ppEnumOleVerb);
        void Update();
        void IsUpToDate();
        void GetUserClassID(uint pClsid);
        void GetUserType(uint dwFormOfType, uint pszUserType);
        void SetExtent(uint dwDrawAspect, uint psizel);
        void GetExtent(uint dwDrawAspect, uint psizel);
        void Advise(object pAdvSink, uint pdwConnection);
        void Unadvise(uint dwConnection);
        void EnumAdvise(ref object ppenumAdvise);
        void GetMiscStatus(uint dwAspect, uint pdwStatus);
        void SetColorScheme(object pLogpal);
    }

    [ComImport, Guid("00000118-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleClientSite
    {
        void SaveObject();
        void GetMoniker(uint dwAssign, uint dwWhichMoniker, ref object ppmk);
        void GetContainer(ref object ppContainer);
        void ShowObject();
        void OnShowWindow(bool fShow);
        void RequestNewObjectLayout();
    }
}
