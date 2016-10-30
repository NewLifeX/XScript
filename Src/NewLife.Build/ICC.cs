using System;
using System.IO;
using System.Linq;
using System.Text;
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

        #region 主要编译方法
        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        public override String GetCompileCommand(Boolean cpp)
        {
            // --debug --endian=little --cpu=Cortex-M3 --enum_is_int -e --char_is_signed --fpu=None 
            // -Ohz --use_c++_inline 
            // --dlib_config C:\Program Files (x86)\IAR Systems\Embedded Workbench 7.0\arm\INC\c\DLib_Config_Normal.h 
            var sb = new StringBuilder();
            if (cpp)
                //sb.Append("--c++ --no_exceptions");
                sb.Append("--eec++");
            else
                sb.Append("--use_c++_inline");
            // -e打开C++扩展
            sb.AppendFormat(" --endian=little --cpu={0} -e --silent", CPU);
            if (Cortex >= 4) sb.Append(" --fpu=None");
            //sb.Append(" --enable_multibytes");
            if (Debug) sb.Append(" --debug");
            // 默认低级优化，发行版-Ohz为代码大小优化，-Ohs为高速度优化
            if (!Debug) sb.Append(" -Ohz");
            foreach (var item in Defines)
            {
                sb.AppendFormat(" -D {0}", item);
            }
            if (Tiny) sb.Append(" -D TINY");
            //var basePath = Complier.CombinePath(@"..\..\..\").GetFullPath();
            //sb.AppendFormat(" --dlib_config \"{0}\\arm\\INC\\c\\DLib_Config_Normal.h\"", basePath);

            foreach (var item in Defines)
            {
                sb.AppendFormat(" -D{0}", item);
            }

            foreach (var item in ExtCompiles)
            {
                sb.AppendFormat(" {0}", item.Trim());
            }

            return sb.ToString();
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

                    WriteLog("注册表 {0} {1}", ToolPath, Version);
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

                            WriteLog("本地 {0} {1}", ToolPath, Version);
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

        void WriteLog(String format, params Object[] args)
        {
            if (XTrace.Debug) XTrace.WriteLine(format, args);
        }
    }
}
