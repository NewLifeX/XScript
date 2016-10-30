using System;
using System.IO;
using Microsoft.Win32;
using NewLife.Log;
using NewLife.Web;

namespace NewLife.Build
{
    /// <summary>MDK环境</summary>
    public class MDK : Builder
    {
        /// <summary>是否使用最新的MDK 6.4</summary>
        public Boolean CLang { get; set; }

        #region 初始化
        /// <summary>初始化</summary>
        public MDK()
        {
            Name = "MDK";

            #region 从注册表获取目录和版本
            if (String.IsNullOrEmpty(ToolPath))
            {
                var reg = Registry.LocalMachine.OpenSubKey("Software\\Keil\\Products\\MDK");
                if (reg == null) reg = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Keil\\Products\\MDK");
                if (reg != null)
                {
                    ToolPath = reg.GetValue("Path") + "";
                    var s = (reg.GetValue("Version") + "").Trim('V', 'v', 'a', 'b', 'c');
                    var ss = s.SplitAsInt(".");
                    Version = new Version(ss[0], ss[1]);

                    if (!String.IsNullOrEmpty(ToolPath)) XTrace.WriteLine("从注册表得到路径{0} {1}！", ToolPath, Version);
                }
            }
            #endregion

            #region 扫描所有根目录，获取MDK安装目录
            //if (String.IsNullOrEmpty(ToolPath))
            {
                foreach (var item in DriveInfo.GetDrives())
                {
                    if (!item.IsReady) continue;

                    var p = Path.Combine(item.RootDirectory.FullName, "Keil\\ARM");
                    if (Directory.Exists(p))
                    {
                        var ver = GetVer(p);
                        if (ver > Version)
                        {
                            ToolPath = p;
                            Version = ver;

                            if (!String.IsNullOrEmpty(ToolPath)) XTrace.WriteLine("从本地磁盘得到路径{0} {1}！", ToolPath, Version);

                            break;
                        }
                    }
                }
            }
            if (Version < new Version(5, 17))
            {
                XTrace.WriteLine("版本 {0} 太旧，准备更新", Version);

                var url = "http://www.newlifex.com/showtopic-1456.aspx";
                var client = new WebClientX(true, true);
                client.Log = XTrace.Log;
                var dir = Environment.SystemDirectory.CombinePath("..\\..\\Keil").GetFullPath();
                var file = client.DownloadLinkAndExtract(url, "MDK", dir);
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
            }
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取MDK安装目录！");
            #endregion
        }

        Version GetVer(String path)
        {
            var p = Path.Combine(path, "..\\Tools.ini");
            if (File.Exists(p))
            {
                foreach (var item in File.ReadAllLines(p))
                {
                    if (String.IsNullOrEmpty(item)) continue;
                    if (item.StartsWith("VERSION=", StringComparison.OrdinalIgnoreCase))
                    {
                        var s = item.Substring("VERSION=".Length).Trim().Trim('V', 'v', 'a', 'b', 'c');
                        var ss = s.SplitAsInt(".");
                        return new Version(ss[0], ss[1]);
                        //break;
                    }
                }
            }

            return new Version();
        }
        #endregion

        public override Boolean Init(Boolean addlib)
        {
            var root = ToolPath;
            if (CLang)
            {
                root = ToolPath.CombinePath("ARMCLANG\\bin").GetFullPath();
                if (!Directory.Exists(root)) root = ToolPath.CombinePath("ARMCC\\bin").GetFullPath();
            }
            else
            {
                // CLang编译器用来检查语法非常棒，但是对代码要求很高，我们有很多代码需要改进，暂时不用
                root = ToolPath.CombinePath("ARMCC\\bin").GetFullPath();
            }

            Complier = root.CombinePath("armcc.exe");
            if (!File.Exists(Complier)) Complier = root.CombinePath("armclang.exe");
            Asm = root.CombinePath("armasm.exe");
            Link = root.CombinePath("armlink.exe");
            Ar = root.CombinePath("armar.exe");
            FromELF = root.CombinePath("fromelf.exe");
            IncPath = root.CombinePath("..\\include").GetFullPath();
            LibPath = root.CombinePath("..\\lib").GetFullPath();

            return base.Init();
        }
    }

    /// <summary>MDK 6.0</summary>
    public class MDK6 : MDK
    {
        public MDK6()
        {
            Name = "MDK6";
            CLang = true;
        }
    }
}