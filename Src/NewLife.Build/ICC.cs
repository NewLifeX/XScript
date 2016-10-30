using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using NewLife.Log;

namespace NewLife.Build
{
    /// <summary>ICC环境</summary>
    public class ICC : Builder
    {
        #region 属性
        #endregion

        #region 初始化
        private static ICCLocation location = new ICCLocation();

        /// <summary>初始化</summary>
        public ICC()
        {
            Name = "ICC";

            Version = location.Version;
            ToolPath = location.ToolPath;
        }
        #endregion
    }

    class ICCLocation
    {
        #region 属性
        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>工具目录</summary>
        public String ToolPath { get; set; }
        #endregion

        public ICCLocation()
        {
            #region 从注册表获取目录和版本
            if (String.IsNullOrEmpty(ToolPath))
            {
                var reg = Registry.LocalMachine.OpenSubKey(@"Software\IAR Systems\Embedded Workbench\5.0");
                if (reg == null) reg = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\IAR Systems\Embedded Workbench\5.0");
                if (reg != null)
                {
                    ToolPath = reg.GetValue("LastInstallPath") + "";
                    if (ToolPath.Contains(".")) Version = ToolPath.AsDirectory().Name.Split(" ").Last();

                    if (!String.IsNullOrEmpty(ToolPath)) XTrace.WriteLine("注册表 {0} {1}", ToolPath, Version);
                }
            }
            #endregion

            #region 扫描所有根目录，获取MDK安装目录
            foreach (var item in DriveInfo.GetDrives())
            {
                if (!item.IsReady) continue;

                //var p = Path.Combine(item.RootDirectory.FullName, @"Program Files (x86)\IAR Systems\Embedded Workbench 7.0");
                var p = Path.Combine(item.RootDirectory.FullName, @"Program Files\IAR Systems");
                if (!Directory.Exists(p)) p = Path.Combine(item.RootDirectory.FullName, @"Program Files (x86)\IAR Systems");
                if (Directory.Exists(p))
                {
                    var f = p.AsDirectory().GetAllFiles("iccarm.exe", true).LastOrDefault();
                    if (f != null)
                    {
                        p = f.Directory.FullName.CombinePath(@"..\..\").GetFullPath();
                        var ver = GetVer(p);
                        if (ver.CompareTo(Version) > 0)
                        {
                            ToolPath = p;
                            Version = ver;

                            XTrace.WriteLine("本地 {0} {1}", ToolPath, Version);
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取ICC安装目录！");
            #endregion
        }

        String GetVer(String path)
        {
            if (!path.Contains(".")) return "";

            return path.AsDirectory().Name.Split(" ").LastOrDefault();
        }
    }
}
