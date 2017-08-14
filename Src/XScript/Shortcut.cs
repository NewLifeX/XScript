using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NewLife.Reflection;

namespace NewLife.XScript
{
    static internal class NativeClasses
    {
        [Flags]
        internal enum SLR_MODE : UInt32
        {
            SLR_INVOKE_MSI = 0x80,
            SLR_NOLINKINFO = 0x40,
            SLR_NO_UI = 0x1,
            SLR_NOUPDATE = 0x8,
            SLR_NOSEARCH = 0x10,
            SLR_NOTRACK = 0x20,
            SLR_UPDATE = 0x4,
            SLR_NO_UI_WITH_MSG_PUMP = 0x101
        }

        [Flags]
        internal enum STGM_ACCESS : UInt32
        {
            STGM_READ = 0x00000000,
            STGM_WRITE = 0x00000001,
            STGM_READWRITE = 0x00000002,
            STGM_SHARE_DENY_NONE = 0x00000040,
            STGM_SHARE_DENY_READ = 0x00000030,
            STGM_SHARE_DENY_WRITE = 0x00000020,
            STGM_SHARE_EXCLUSIVE = 0x00000010,
            STGM_PRIORITY = 0x00040000,
            STGM_CREATE = 0x00001000,
            STGM_CONVERT = 0x00020000,
            STGM_FAILIFTHERE = 0x00000000,
            STGM_DIRECT = 0x00000000,
            STGM_TRANSACTED = 0x00010000,
            STGM_NOSCRATCH = 0x00100000,
            STGM_NOSNAPSHOT = 0x00200000,
            STGM_SIMPLE = 0x08000000,
            STGM_DIRECT_SWMR = 0x00400000,
            STGM_DELETEONRELEASE = 0x04000000
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0)]
        internal struct _FILETIME
        {
            public UInt32 dwLowDateTime;
            public UInt32 dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0, CharSet = CharSet.Unicode)]
        internal struct _WIN32_FIND_DATAW
        {
            public UInt32 dwFileAttributes;
            public _FILETIME ftCreationTime;
            public _FILETIME ftLastAccessTime;
            public _FILETIME ftLastWriteTime;
            public UInt32 nFileSizeHigh;
            public UInt32 nFileSizeLow;
            public UInt32 dwReserved0;
            public UInt32 dwReserved1;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public String cFileName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public String cAlternateFileName;
        }

        internal const UInt32 SLGP_SHORTPATH = 0x01;
        internal const UInt32 SLGP_UNCPRIORITY = 0x02;
        internal const UInt32 SLGP_RAWPATH = 0x04;

        [ComImport()]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellLinkW
        {
            [PreserveSig()]
            Int32 GetPath([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, Int32 cchMaxPath, ref _WIN32_FIND_DATAW pfd, UInt32 fFlags);

            [PreserveSig()]
            Int32 GetIDList(out IntPtr ppidl);

            [PreserveSig()]
            Int32 SetIDList(IntPtr pidl);

            [PreserveSig()]
            Int32 GetDescription([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, Int32 cchMaxName);

            [PreserveSig()]
            Int32 SetDescription([MarshalAs(UnmanagedType.LPWStr)] String pszName);

            [PreserveSig()]
            Int32 GetWorkingDirectory([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, Int32 cchMaxPath);

            [PreserveSig()]
            Int32 SetWorkingDirectory(
               [MarshalAs(UnmanagedType.LPWStr)] String pszDir);

            [PreserveSig()]
            Int32 GetArguments([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, Int32 cchMaxPath);

            [PreserveSig()]
            Int32 SetArguments([MarshalAs(UnmanagedType.LPWStr)] String pszArgs);

            [PreserveSig()]
            Int32 GetHotkey(out UInt16 pwHotkey);

            [PreserveSig()]
            Int32 SetHotkey(UInt16 pwHotkey);

            [PreserveSig()]
            Int32 GetShowCmd(out UInt32 piShowCmd);

            [PreserveSig()]
            Int32 SetShowCmd(UInt32 piShowCmd);

            [PreserveSig()]
            Int32 GetIconLocation([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, Int32 cchIconPath, out Int32 piIcon);

            [PreserveSig()]
            Int32 SetIconLocation(
               [MarshalAs(UnmanagedType.LPWStr)] String pszIconPath, Int32 iIcon);

            [PreserveSig()]
            Int32 SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] String pszPathRel, UInt32 dwReserved);

            [PreserveSig()]
            Int32 Resolve(IntPtr hWnd, UInt32 fFlags);

            [PreserveSig()]
            Int32 SetPath([MarshalAs(UnmanagedType.LPWStr)] String pszFile);
        }

        [ComImport()]
        [Guid("0000010B-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPersistFile
        {
            [PreserveSig()]
            Int32 GetClassID(out Guid pClassID);

            [PreserveSig()]
            Int32 IsDirty();

            [PreserveSig()]
            Int32 Load([MarshalAs(UnmanagedType.LPWStr)] String pszFileName, UInt32 dwMode);

            [PreserveSig()]
            Int32 Save([MarshalAs(UnmanagedType.LPWStr)] String pszFileName, [MarshalAs(UnmanagedType.Bool)] Boolean fRemember);

            [PreserveSig()]
            Int32 SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] String pszFileName);

            [PreserveSig()]
            Int32 GetCurFile([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath);
        }

        [Guid("00021401-0000-0000-C000-000000000046")]
        [ClassInterface(ClassInterfaceType.None)]
        [ComImport()]
        private class CShellLink { }

        internal static NativeClasses.IShellLinkW CreateShellLink()
        {
            return (NativeClasses.IShellLinkW)new NativeClasses.CShellLink();
        }
    }

    public class Shortcut
    {
        private const Int32 MAX_DESCRIPTION_LENGTH = 512;
        private const Int32 MAX_PATH = 512;

        private NativeClasses.IShellLinkW _link;

        public Shortcut()
        {
            _link = NativeClasses.CreateShellLink();
        }

        public Shortcut(String path)
            : this()
        {
            Marshal.ThrowExceptionForHR(_link.SetPath(path));
        }

        public String Path
        {
            get
            {
                var fdata = new NativeClasses._WIN32_FIND_DATAW();
                var path = new StringBuilder(MAX_PATH, MAX_PATH);
                Marshal.ThrowExceptionForHR(_link.GetPath(path, path.MaxCapacity, ref fdata, NativeClasses.SLGP_UNCPRIORITY));
                return path.ToString();
            }
            set
            {
                Marshal.ThrowExceptionForHR(_link.SetPath(value));
            }
        }

        public String Description
        {
            get
            {
                var desc = new StringBuilder(MAX_DESCRIPTION_LENGTH, MAX_DESCRIPTION_LENGTH);
                Marshal.ThrowExceptionForHR(_link.GetDescription(desc, desc.MaxCapacity));
                return desc.ToString();
            }
            set
            {
                Marshal.ThrowExceptionForHR(_link.SetDescription(value));
            }
        }

        public String RelativePath
        {
            set
            {
                Marshal.ThrowExceptionForHR(_link.SetRelativePath(value, 0));
            }
        }

        public String WorkingDirectory
        {
            get
            {
                var dir = new StringBuilder(MAX_PATH, MAX_PATH);
                Marshal.ThrowExceptionForHR(_link.GetWorkingDirectory(dir, dir.MaxCapacity));
                return dir.ToString();
            }
            set
            {
                Marshal.ThrowExceptionForHR(_link.SetWorkingDirectory(value));
            }
        }

        public String Arguments
        {
            get
            {
                var args = new StringBuilder(MAX_PATH, MAX_PATH);
                Marshal.ThrowExceptionForHR(_link.GetArguments(args, args.MaxCapacity));
                return args.ToString();
            }
            set
            {
                Marshal.ThrowExceptionForHR(_link.SetArguments(value));
            }
        }

        public UInt16 HotKey
        {
            get
            {
                Marshal.ThrowExceptionForHR(_link.GetHotkey(out var key));
                return key;
            }
            set
            {
                Marshal.ThrowExceptionForHR(_link.SetHotkey(value));
            }
        }

        public void Resolve(IntPtr hwnd, UInt32 flags)
        {
            Marshal.ThrowExceptionForHR(_link.Resolve(hwnd, flags));
        }

        public void Resolve()
        {
            Resolve(IntPtr.Zero, (UInt32)NativeClasses.SLR_MODE.SLR_NO_UI);
        }

        private NativeClasses.IPersistFile AsPersist
        {
            get { return ((NativeClasses.IPersistFile)_link); }
        }

        public void Save(String fileName)
        {
            var hres = AsPersist.Save(fileName, true);
            Marshal.ThrowExceptionForHR(hres);
        }

        public void Load(String fileName)
        {
            var hres = AsPersist.Load(fileName, (UInt32)NativeClasses.STGM_ACCESS.STGM_READ);
            Marshal.ThrowExceptionForHR(hres);
        }

        public static Shortcut Create(String name, String arg)
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            var asmx = AssemblyX.Create(Assembly.GetExecutingAssembly());
            if (!String.IsNullOrEmpty(name)) name = "（" + name + "）";
            var file = dir.CombinePath(asmx.Title + name + ".lnk");

            var sc = new Shortcut()
            {
                Path = Assembly.GetEntryAssembly().Location,
                Arguments = arg,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                Description = asmx.Description
            };
            sc.Save(file);

            return sc;
        }
    }
}