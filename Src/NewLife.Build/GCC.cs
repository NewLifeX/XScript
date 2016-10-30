using System;
using System.IO;
using System.Linq;
using System.Text;
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

        #region 主要编译方法
        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        public override String GetCompileCommand(Boolean cpp)
        {
            // -ggdb -ffunction-sections -fno-exceptions -fno-rtti -O0   -mcpu=cortex-m3 -mthumb
            // -I. -IstLib/inc -IstCM3 -DDEBUG=1 -DARM_MATH_CM3 -DSTM32F103VE -Dstm32_flash_layout -DSTM32F10X_HD
            // -c LEDBlink.cpp -o Debug/LEDBlink.o -MD -MF Debug/LEDBlink.dep
            var sb = new StringBuilder();
            sb.Append("-ggdb");
            if (cpp) sb.Append(" -std=c++17");
            sb.AppendFormat(" -mlittle-endian -mthumb -mcpu={0} -mthumb-interwork -O{1}", CPU, Debug ? 0 : 3);
            sb.AppendFormat(" -ffunction-sections -fdata-sections");
            sb.AppendFormat(" -fno-exceptions -MD");
            //sb.AppendFormat(" -fno-exceptions --specs=nano.specs --specs=rdimon.specs -o");
            //sb.AppendFormat(" -L. -L./ldscripts -T gcc.ld");
            //sb.AppendFormat(" -Wl,--gc-sections");
            //sb.AppendFormat(" -fwide-exec-charset=UTF-8");
            //sb.AppendFormat("  -D__NO_SYSTEM_INIT -D{0}", Flash);
            //sb.AppendFormat(" -D{0}", Flash);
            //if (GD32) sb.Append(" -DGD32");
            foreach (var item in Defines)
            {
                sb.AppendFormat(" -D{0}", item);
            }
            if (Debug) sb.Append(" -DDEBUG -DUSE_FULL_ASSERT");
            if (Tiny) sb.Append(" -DTINY");

            foreach (var item in ExtCompiles)
            {
                sb.AppendFormat(" {0}", item.Trim());
            }

            return sb.ToString();
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