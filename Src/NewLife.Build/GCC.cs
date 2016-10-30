using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using NewLife.Log;

namespace NewLife.Build
{
    /// <summary>MDK环境</summary>
    public class GCC : Builder
    {
        #region 属性
        #endregion

        #region 初始化
        private static GCCLocation location = new GCCLocation();

        /// <summary>初始化</summary>
        public GCC()
        {
            Name = "GCC";

            Version = location.Version;
            ToolPath = location.ToolPath;
        }
        #endregion
    }

    class GCCLocation
    {
        #region 属性
        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>工具目录</summary>
        public String ToolPath { get; set; }
        #endregion

        public GCCLocation()
        {
            #region 从注册表获取目录和版本
            if (String.IsNullOrEmpty(ToolPath))
            {
                var reg = Registry.LocalMachine.OpenSubKey(@"Software\ARM\GNU Tools for ARM Embedded Processors");
                if (reg == null) reg = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\ARM\GNU Tools for ARM Embedded Processors");
                if (reg != null)
                {
                    ToolPath = reg.GetValue("InstallFolder") + "";
                    if (ToolPath.Contains(".")) Version = ToolPath.AsDirectory().Name;

                    if (!String.IsNullOrEmpty(ToolPath)) XTrace.WriteLine("注册表 {0} {1}", ToolPath, Version);
                }
            }
            #endregion

            #region 扫描所有根目录，获取MDK安装目录
            //if (String.IsNullOrEmpty(ToolPath))
            {
                foreach (var item in DriveInfo.GetDrives())
                {
                    if (!item.IsReady) continue;

                    var p = Path.Combine(item.RootDirectory.FullName, @"GCC\arm-none-eabi");
                    if (Directory.Exists(p))
                    {
                        p = p.CombinePath(@"..\").GetFullPath();
                        var ver = GetVer(p);
                        if (ver.CompareTo(Version) > 0)
                        {
                            ToolPath = p;
                            Version = ver;

                            XTrace.WriteLine("本地 {0} {1}", p, ver);
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取GCC安装目录！");
            #endregion
        }

        String GetVer(String path)
        {
            // bin\arm-none-eabi-gcc-5.4.1.exe
            var di = path.CombinePath("bin").AsDirectory();
            if (!di.Exists) return "";

            var fi = di.GetAllFiles("arm-none-eabi-gcc-*.exe").FirstOrDefault();
            if (fi == null || !fi.Exists) return "";

            return fi.Name.Substring(fi.Name.LastIndexOf("-") + 1).TrimEnd(".exe");
        }
    }
}