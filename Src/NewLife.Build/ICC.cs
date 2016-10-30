using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace NewLife.Build
{
    /// <summary>ICC环境</summary>
    public class ICC : Builder
    {
        #region 属性
        #endregion

        #region 初始化
        /// <summary>初始化</summary>
        public ICC()
        {
            Name = "ICC";

            #region 从注册表获取目录和版本
            if (String.IsNullOrEmpty(ToolPath))
            {
                var reg = Registry.LocalMachine.OpenSubKey(@"Software\IAR Systems\Embedded Workbench\5.0");
                if (reg == null) reg = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\IAR Systems\Embedded Workbench\5.0");
                if (reg != null)
                {
                    ToolPath = reg.GetValue("LastInstallPath") + "";
                    if (ToolPath.Contains(".")) Version = ToolPath.AsDirectory().Name.Split(" ").Last();
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
                        ToolPath = f.Directory.FullName.CombinePath(@"..\..\").GetFullPath();
                        if (ToolPath.Contains(".")) Version = ToolPath.AsDirectory().Name.Split(" ").Last();
                        break;
                    }
                }
            }
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取ICC安装目录！");
            #endregion
        }
        #endregion
    }
}
