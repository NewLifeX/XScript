using System;
using System.Collections.Generic;
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

            return base.Init();
        }

        #region 主要编译方法
        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        public override String GetCompileCommand(Boolean cpp)
        {
            // -ggdb -ffunction-sections -fno-exceptions -fno-rtti -O0   -mcpu=cortex-m3 -mthumb
            // -I. -IstLib/inc -IstCM3 -DDEBUG=1 -DARM_MATH_CM3 -DSTM32F103VE -Dstm32_flash_layout -DSTM32F10X_HD
            // -c LEDBlink.cpp -o Debug/LEDBlink.o -MD -MF Debug/LEDBlink.dep

            // arm-none-eabi-gcc -DM3 -DCONFIG_PLATFORM_8195A -DGCC_ARMCM3 -DARDUINO_SDK -mcpu=cortex-m3 -mthumb -g2 -w -O2 
            // -Wno-pointer-sign -fno-common -fmessage-length=0  -ffunction-sections -fdata-sections -fomit-frame-pointer -fno-short-enums 
            // -mcpu=cortex-m3 -DF_CPU=166000000L -std=gnu99 -fsigned-char

            var sb = new StringBuilder();
            if (cpp)
                sb.Append("-std=c++17");
            else
                sb.Append("-std=gnu99");
            sb.AppendFormat(" -mcpu={0} -mthumb -O{1}", CPU.ToLower(), Debug ? 0 : 3);
            sb.AppendFormat(" -ffunction-sections -fdata-sections -fomit-frame-pointer");
            sb.AppendFormat(" -fno-exceptions -MD -fno-common -fmessage-length=0");
            if (Linux) sb.Append(" -fno-short-enums -fsigned-char");
            if (!cpp) sb.Append(" -Wno-pointer-sign");
            // 调试版打开所有警告
            if (Debug)
                sb.Append(" -W -Wall -g2");
            else
                sb.Append(" -w");
            if (Debug) sb.Append(" -DDEBUG -DUSE_FULL_ASSERT");
            if (Tiny) sb.Append(" -DTINY");
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

        /// <summary>汇编程序</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected override String OnAssemble(String file)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("-mthumb -mcpu={0} -mthumb", CPU.ToLower());
            // 汇编的警告意义不大
            //if (Debug) sb.Append(" -W -Wall -g");
            sb.AppendFormat(" -I.");
            foreach (var item in Includes)
            {
                sb.AppendFormat(" -I{0}", item);
            }

            return sb.ToString();
        }

        /// <summary>链接静态库</summary>
        /// <returns></returns>
        protected override String OnBuildLib(String lib)
        {
            var sb = new StringBuilder();
            sb.AppendFormat(" -r \"{0}\"", lib);

            return sb.ToString();
        }

        /// <summary>链接静态库</summary>
        /// <returns></returns>
        protected override String OnBuild(String name)
        {
            /*
             * -mcpu=cortex-m3 -mthumb -g --specs=nano.specs -nostartfiles 
             * -Wl,-Map=$(BIN_DIR)/application.map -Os -Wl,--gc-sections -Wl,--cref -Wl,--entry=Reset_Handler -Wl,--no-enum-size-warning -Wl,--no-wchar-size-warning
             * $(LFLAGS) -o $(BIN_DIR)/$(TARGET).axf  $(OBJ_LIST) $(OBJ_DIR)/ram_1.r.o $(LIBFLAGS) -T./rlx8195A-symbol-v02-img2.ld
             */

            var lstName = GetListPath(name);
            var objName = GetObjPath(name);

            var sb = new StringBuilder();
            //sb.AppendFormat("-mcpu={0} -mthumb --specs=nano.specs", CPU.ToLower());
            sb.AppendFormat("-mcpu={0} -mthumb", CPU.ToLower());
            sb.AppendFormat(" -Os -Wl,--gc-sections -Wl,--cref -Wl,--entry=Reset_Handler");
            if (Debug) sb.Append(" -g");
            if (Linux) sb.Append(" -Wl,--no-enum-size-warning -Wl,--no-wchar-size-warning");
            var icf = Scatter;
            if (icf.IsNullOrEmpty()) icf = ".".AsDirectory().GetAllFiles("*.ld", false).FirstOrDefault()?.Name;
            if (!icf.IsNullOrEmpty() && File.Exists(icf.GetFullPath()))
                sb.AppendFormat(" -T\"{0}\"", icf);

            foreach (var item in ExtBuilds)
            {
                sb.AppendFormat(" {0}", item.Trim());
            }

            var axf = objName.EnsureEnd(".axf");
            sb.AppendFormat(" -Wl,-Map=\"{0}.map\" -o \"{1}\"", lstName, axf);
            //sb.AppendFormat(" -o \"{0}\"", axf);

            return sb.ToString();
        }

        /// <summary>加载库文件</summary>
        /// <param name="sb"></param>
        protected override void LoadLib(StringBuilder sb)
        {
            var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Libs)
            {
                var lib = new LibFile(item);
                // 调试版/发行版 优先选用最佳匹配版本
                var old = "";
                // 不包含，直接增加
                if (!dic.TryGetValue(lib.Name, out old))
                {
                    dic.Add(lib.Name, lib.FullName);
                }
                // 已包含，并且新版本更合适，替换
                else
                {
                    //Console.WriteLine("{0} Debug={1} Tiny={2}", lib.FullName, lib.Debug, lib.Tiny);
                    var lib2 = new LibFile(old);
                    if (!(lib2.Debug == Debug && lib2.Tiny == Tiny) &&
                    (lib.Debug == Debug && lib.Tiny == Tiny))
                    {
                        dic[lib.Name] = lib.FullName;
                    }
                }
            }

            var hs = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            Console.WriteLine("使用静态库：");
            foreach (var item in dic)
            {
                if (!item.Value.EndsWithIgnoreCase(".a")) continue;

                Console.WriteLine("\t{0}\t{1}", item.Key, item.Value);

                var fi = Path.GetFileName(item.Value);
                if (!fi.StartsWith("lib") || !fi.EndsWith(".a")) continue;

                var dir = Path.GetDirectoryName(item.Value);
                if (!hs.Contains(dir))
                {
                    hs.Add(dir);

                    sb.AppendFormat(" -L{0}", dir);
                }

                sb.AppendFormat(" -l{0}", fi.TrimStart("lib").TrimEnd(".a"));
            }
        }

        /// <summary>导出目标文件</summary>
        /// <param name="axf"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        protected override Boolean Dump(String axf, String target)
        {
            var cmd = "";
            if (target.EndsWithIgnoreCase(".bin"))
                cmd = "--bin -o \"{0}\" \"{1}\"".F(axf, target);
            else
                cmd = "--i32 -o \"{0}\" \"{1}\"".F(axf, target);

            var rs = ObjCopy.Run(cmd, 3000, WriteLog);

            return rs != 0;
        }
        #endregion

        /// <summary>初始化关键字</summary>
        protected override void InitWord()
        {
            base.InitWord();

            var ss = Words;
            ss["implicit declaration of function"] = "隐式声明函数";
            ss["In function"] = "在函数";
            ss["passing argument"] = "传递参数";
            ss["from incompatible pointer type"] = "从不兼容指针类型";
            ss["expected "] = "要求 ";
            ss["but argument is of type"] = "但参数类型是";
            //ss[" of "] = " 于 ";
            ss[" discards "] = " 抛弃 ";
            ss[" qualifier "] = " 修饰 ";
            ss["from pointer target type"] = "从指针";
            ss[" redefined"] = " 重复定义";
            ss["this is the location of the previous definition"] = "这是前一个定义";
            ss["makes integer from pointer without a cast"] = "整数未强转为指针";
            ss["conflicting types for"] = "类型冲突";
            ss["previous declaration of"] = "前一个声明";
            ss["was here"] = "在";
            ss["cannot find entry symbol"] = "找不到符号";
            ss["undefined reference to"] = "未定义引用";
            ss["defaulting to"] = "默认";
        }
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
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取GCC安装目录！");
            #endregion
        }

        public String GetVer(String path)
        {
            // bin\arm-none-eabi-gcc-5.4.1.exe
            var di = path.CombinePath("bin").AsDirectory();
            if (!di.Exists) return "";

            var fi = di.GetAllFiles("arm-none-eabi-gcc-*.exe").FirstOrDefault();
            if (fi == null || !fi.Exists) return "";

            return fi.Name.Substring(fi.Name.LastIndexOf("-") + 1).TrimEnd(".exe");
        }

        void WriteLog(String format, params Object[] args)
        {
            if (XTrace.Debug) XTrace.WriteLine(format, args);
        }
    }
}