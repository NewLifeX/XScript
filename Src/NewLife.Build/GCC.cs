using System;
using System.IO;
using Microsoft.Win32;

namespace NewLife.Build
{
    /// <summary>MDK环境</summary>
    public class GCC : Builder
    {
        #region 属性
        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>工具目录</summary>
        public String ToolPath { get; set; }
        #endregion

        #region 初始化
        /// <summary>初始化</summary>
        public GCC()
        {
            Name = "GCC";

            #region 从注册表获取目录和版本
            if (String.IsNullOrEmpty(ToolPath))
            {
                var reg = Registry.LocalMachine.OpenSubKey(@"Software\ARM\GNU Tools for ARM Embedded Processors");
                if (reg == null) reg = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\ARM\GNU Tools for ARM Embedded Processors");
                if (reg != null)
                {
                    ToolPath = reg.GetValue("InstallFolder") + "";
                    if (ToolPath.Contains(".")) Version = ToolPath.AsDirectory().Name;
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
                        ToolPath = p.CombinePath(@"..\").GetFullPath();
                        break;
                    }
                }
            }
            /*if (Version < new Version(5, 17))
            {
                var url = "http://www.newlifex.com/showtopic-1456.aspx";
                var client = new WebClientX(true, true);
                client.Log = XTrace.Log;
                var dir = Environment.SystemDirectory.CombinePath("..\\..\\Keil").GetFullPath();
                var file = client.DownloadLinkAndExtract(url, "GCC", dir);
                var p = dir.CombinePath("ARM");
                if (Directory.Exists(p))
                {
                    var ver = GetVer(p);
                    if (ver > Version)
                    {
                        ToolPath = p;
                        Version = ver;
                    }
                }
            }*/
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取GCC安装目录！");
            #endregion
        }
        #endregion
    }
}