using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using NewLife.Log;

namespace NewLife.Build
{
    /// <summary>arm-none-eabi-gcc 工具链</summary>
    public class GCCArm : GCC
    {
        #region 初始化
        private static GCCArmLocation location = new GCCArmLocation();

        /// <summary>初始化</summary>
        public GCCArm()
        {
            Name = "GCCArm";

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

            Complier = basePath.CombinePath("arm-none-eabi-gcc.exe").GetFullPath();
            //Asm = basePath.CombinePath(@"..\arm-none-eabi\bin\as.exe");
            Asm = basePath.CombinePath("arm-none-eabi-as.exe");
            //Link = basePath.CombinePath("arm-none-eabi-ld.exe");
            Link = basePath.CombinePath("arm-none-eabi-gcc.exe");
            Ar = basePath.CombinePath("arm-none-eabi-ar.exe");
            ObjCopy = basePath.CombinePath("arm-none-eabi-objcopy.exe");

            IncPath = basePath.CombinePath(@"..\arm-none-eabi\include").GetFullPath();
            LibPath = basePath.CombinePath(@"..\arm-none-eabi\lib").GetFullPath();

            return base.Init(addlib);
        }

        #region 主要编译方法
        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        public override String GetCompileCommand(Boolean cpp)
        {
            return base.GetCompileCommand(cpp) + " -mthumb";
        }

        /// <summary>汇编程序</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected override String OnAssemble(String file)
        {
            return base.OnAssemble(file) + " -mthumb";
        }

        /// <summary>链接静态库</summary>
        /// <returns></returns>
        protected override String OnBuild(String name)
        {
            return base.OnBuild(name) + " -mthumb";
        }
        #endregion
    }

        class GCCArmLocation
    {
        #region 属性
        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>工具目录</summary>
        public String ToolPath { get; set; }
        #endregion

        public GCCArmLocation()
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

                    WriteLog("注册表 {0} {1}", ToolPath, Version);
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
                    if (!Directory.Exists(p)) p = Path.Combine(item.RootDirectory.FullName, @"GCCArm\arm-none-eabi");
                    if (Directory.Exists(p))
                    {
                        p = p.CombinePath(@"..\").GetFullPath();
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
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取GCC Arm安装目录！");
            #endregion
        }

        public String GetVer(String path)
        {
            // bin\arm-none-eabi-gcc-5.4.1.exe
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