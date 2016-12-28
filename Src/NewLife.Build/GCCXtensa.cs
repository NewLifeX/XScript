using System;
using System.IO;
using System.Linq;
using NewLife.Log;

namespace NewLife.Build
{
    /// <summary>Xtensa 工具链</summary>
    public class GCCXtensa : GCC
    {
        #region 初始化
        private static GCCXtensaLocation location = new GCCXtensaLocation();

        /// <summary>初始化</summary>
        public GCCXtensa()
        {
            Name = "GCCXtensa";

            Version = location.Version;
            ToolPath = location.ToolPath;
        }
        #endregion

        /// <summary>初始化</summary>
        /// <param name="addlib"></param>
        /// <returns></returns>
        public override Boolean Init(Boolean addlib)
        {
            if (ToolPath != location.ToolPath) Version = location.GetVer(ToolPath);

            var basePath = ToolPath.CombinePath("bin").GetFullPath();

            Complier = basePath.CombinePath("xtensa-lx106-elf-gcc.exe").GetFullPath();
            //Asm = basePath.CombinePath(@"..\xtensa-lx106-elf\bin\as.exe");
            Asm = basePath.CombinePath("xtensa-lx106-elf-as.exe");
            //Link = basePath.CombinePath("xtensa-lx106-elf-ld.exe");
            Link = basePath.CombinePath("xtensa-lx106-elf-gcc.exe");
            Ar = basePath.CombinePath("xtensa-lx106-elf-ar.exe");
            ObjCopy = basePath.CombinePath("xtensa-lx106-elf-objcopy.exe");

            IncPath = basePath.CombinePath(@"..\xtensa-lx106-elf\include").GetFullPath();
            LibPath = basePath.CombinePath(@"..\xtensa-lx106-elf\lib").GetFullPath();

            return base.Init(addlib);
        }
    }

    class GCCXtensaLocation
    {
        #region 属性
        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>工具目录</summary>
        public String ToolPath { get; set; }
        #endregion

        public GCCXtensaLocation()
        {
            #region 从注册表获取目录和版本
            //if (String.IsNullOrEmpty(ToolPath))
            //{
            //    var reg = Registry.LocalMachine.OpenSubKey(@"Software\ARM\GNU Tools for ARM Embedded Processors");
            //    if (reg == null) reg = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\ARM\GNU Tools for ARM Embedded Processors");
            //    if (reg != null)
            //    {
            //        ToolPath = reg.GetValue("InstallFolder") + "";
            //        if (ToolPath.Contains(".")) Version = ToolPath.AsDirectory().Name;

            //        WriteLog("注册表 {0} {1}", ToolPath, Version);
            //    }
            //}
            #endregion

            #region 扫描所有根目录，获取MDK安装目录
            //if (String.IsNullOrEmpty(ToolPath))
            {
                foreach (var item in DriveInfo.GetDrives())
                {
                    if (!item.IsReady) continue;

                    var p = Path.Combine(item.RootDirectory.FullName, "GCCXtensa");
                    if (Directory.Exists(p))
                    {
                        p = p.GetFullPath();
                        var ver = GetVer(p);
                        if (ver.CompareTo(Version) > 0)
                        {
                            ToolPath = p;
                            Version = ver;

                            WriteLog("本地 {0} {1}", p, ver);
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取GCC Xtensa安装目录！");
            #endregion
        }

        public String GetVer(String path)
        {
            // bin\xtensa-lx106-elf-gcc-5.4.1.exe
            var di = path.CombinePath("bin").AsDirectory();
            if (!di.Exists) return "";

            var fi = di.GetAllFiles("*-gcc-*.exe").FirstOrDefault();
            if (fi == null || !fi.Exists) return "";

            return fi.Name.Substring(fi.Name.LastIndexOf("-") + 1).TrimEnd(".exe");
        }

        void WriteLog(String format, params Object[] args)
        {
            if (XTrace.Debug) XTrace.WriteLine(format, args);
        }
    }
}