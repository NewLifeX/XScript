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
        /// <summary>nano.specs/nosys.specs</summary>
        public String Specs { get; set; } = "nano.specs";

        /// <summary>入口函数。链接目标文件时使用</summary>
        public String Entry { get; set; }

        /// <summary>链接时输出详细过程</summary>
        public Boolean LinkVerbose { get; set; }
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

            return base.Init(addlib);
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
            // 指定编译语言
            if (cpp)
                sb.Append("-std=c++17");
            else
                sb.Append("-std=gnu99");
            // 指定CPU和指令集
            sb.AppendFormat(" -mcpu={0} -mthumb", CPU.ToLower());
            // 指定优化等级
            /*
             * gcc默认提供了5级优 化选项的集合: 
             * -O0:无优化(默认) 
             * -O和-O1:使用能减少目标文 件 大小以及执行时间并且不会使编译时间明显增加的优化.在编译大型程序的时候会显著增加编译时内存的使用. 
             * -O2: 包含-O1的优化并增加了不需要在目标文件大小和执行速度上进行折衷的优化.编译器不执行循环展开以及函数内联.此选项将增加编译时间和目标文件的执行性能. 
             * -Os:专门优化目标文件大小,执行所有的不增加目标文件大小的-O2优化选项.并且执行专门减小目标文件大小的优化选项. 
             * -O3: 打开所有-O2的优化选项并且增加 -finline-functions, -funswitch-loops,-fpredictive-commoning, -fgcse-after-reload and -ftree-vectorize优化选项. 
             */
            sb.AppendFormat(" -O{0}", Debug ? 0 : 3);
            // 为每个函数和数据项分配独立的段
            sb.Append(" -ffunction-sections -fdata-sections");
            // omit- frame-pointer:可能的情况下不产生栈帧
            sb.Append(" -fomit-frame-pointer");
            // 不使用异常
            sb.Append(" -fno-exceptions");
            // 禁止将未初始化的全局变量放入到common段，而是放入bss段，初始为0
            sb.Append(" -fno-common");
            // Linux风格，4字节枚举、有符号字符
            if (Linux) sb.Append(" -fno-short-enums -fsigned-char");
            // C语言指针无符号
            if (!cpp) sb.Append(" -Wno-pointer-sign");
            // 调试版打开所有警告
            if (Debug)
                sb.Append(" -W -Wall -g2");
            else
                sb.Append(" -w");
            // 输出依赖文件
            sb.Append(" -MD");
            if (Debug) sb.Append(" -DDEBUG -DUSE_FULL_ASSERT");
            if (Tiny) sb.Append(" -DTINY");
            foreach (var item in Defines)
            {
                sb.AppendFormat(" -D{0}", item);
            }

            if (!ExtCompiles.IsNullOrEmpty()) sb.AppendFormat(" {0}", ExtCompiles.Trim());

            return sb.ToString();
        }

        /// <summary>汇编程序</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected override String OnAssemble(String file)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("-mthumb -mcpu={0}", CPU.ToLower());
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
            // 指定CPU和指令集
            sb.AppendFormat("-mcpu={0} -mthumb", CPU.ToLower());
            // 指定优化等级
            sb.AppendFormat(" -O{0}", Debug ? 0 : 3);
            if (!Specs.IsNullOrEmpty()) sb.AppendFormat(" --specs={0}", Specs);
            // 只链接静态库，不找动态库
            sb.Append(" -static");
            if (!Entry.IsNullOrEmpty()) sb.AppendFormat(" -Wl,--entry={0}", Entry);
            sb.Append(" -Wl,--cref");
            // 链接时输出详细过程
            if (LinkVerbose) sb.Append(" -Wl,--verbose");
            // 为每个函数和数据项分配独立的段
            //sb.Append(" -ffunction-sections -fdata-sections");
            // 删除未使用段
            sb.AppendFormat(" -Wl,--gc-sections");
            // Linux风格，4字节枚举、有符号字符
            if (Linux) sb.Append(" -Wl,--no-enum-size-warning -Wl,--no-wchar-size-warning");
            // 调试版打开所有警告
            if (Debug)
                sb.Append(" -W -Wall -g2");
            else
                sb.Append(" -w");

            var icf = Scatter;
            if (icf.IsNullOrEmpty()) icf = ".".AsDirectory().GetAllFiles("*.ld", false).FirstOrDefault()?.Name;
            if (!icf.IsNullOrEmpty() && File.Exists(icf.GetFullPath()))
                sb.AppendFormat(" -T\"{0}\"", icf);

            var axf = objName.EnsureEnd(".axf");
            sb.AppendFormat(" -Wl,-Map=\"{0}.map\" -o \"{1}\"", lstName, axf);
            //sb.AppendFormat(" -o \"{0}\"", axf);

            sb.Append(" -Xlinker \"-(\"");

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

                var fi = Path.GetFileName(item.Value);
                if (!fi.StartsWith("lib") || !fi.EndsWith(".a")) continue;

                var dir = Path.GetDirectoryName(item.Value);
                if (!hs.Contains(dir))
                {
                    hs.Add(dir);

                    sb.AppendFormat(" -L{0}", dir);
                }
            }
            var n = 0;
            foreach (var item in dic)
            {
                if (!item.Value.EndsWithIgnoreCase(".a")) continue;

                var fi = Path.GetFileName(item.Value);
                if (!fi.StartsWith("lib") || !fi.EndsWith(".a")) continue;

                Console.WriteLine("\t{0}\t{1}", item.Key, item.Value);

                //if (n++ == 0) sb.Append(" -Xlinker \"-(\"");

                sb.AppendFormat(" -l{0}", fi.TrimStart("lib").TrimEnd(".a"));
            }
            if (n > 0) sb.Append(" -Xlinker \"-)\"");
        }

        /// <summary>导出目标文件</summary>
        /// <param name="axf"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        protected override Boolean Dump(String axf, String target)
        {
            var cmd = "";
            if (target.EndsWithIgnoreCase(".bin"))
                cmd = "-O binary \"{0}\" \"{1}\"".F(axf, target);
            else if (target.EndsWithIgnoreCase(".hex"))
                cmd = "-O ihex \"{0}\" \"{1}\"".F(axf, target);
            else
                return false;

            var rs = ObjCopy.Run(cmd, 3000, WriteLog);

            return rs != 0;
        }
        #endregion

        #region 检查是否需要重新编译
        /// <summary>检查源码文件是否需要编译</summary>
        /// <param name="src"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        protected override Boolean Check(String src, FileInfo obj)
        {
            if (base.Check(src, obj)) return true;

            // 检查依赖文件
            var dp = Path.ChangeExtension(obj.FullName, ".d");
            if (File.Exists(dp))
            {
                var depends = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                // 分析所有被依赖文件的最后更新时间
                foreach (var item in File.ReadAllLines(dp))
                {
                    if (item.IsNullOrEmpty()) continue;

                    var header = item.Substring(" ").TrimEnd("\\").Trim();
                    foreach (var elm in header.Split(" "))
                    {
                        header = elm;
                        if (!header.IsNullOrEmpty() && !depends.Contains(header))
                        {
                            // 如果头文件修改过，需要重新编译
                            if (obj.LastWriteTime < header.AsFile().LastWriteTime)
                            {
                                // 输出被修改了的头文件
                                if (header.StartsWithIgnoreCase(_Root)) header = header.Substring(_Root.Length);
                                Console.Write(header + " ");
                                return true;
                            }

                            depends.Add(header);
                        }
                    }
                }
            }

            return false;
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
            ss["unused parameter"] = "未使用参数";
            ss["unused variable"] = "未使用变量";
            ss["deleting object of polymorphic class type"] = "删除没有虚析构的多态类对象";
            ss["which has non-virtual destructor might cause undefined behaviour"] = "可能导致未知情况";
            ss["comparison between signed and unsigned integer expressions"] = "比较整数与无符号整数";
            ss["In copy constructor"] = "在拷贝构造函数";
            ss["base class"] = "基类";
            ss["should be explicitly initialized in the copy constructor"] = "应该在拷贝构造函数中被明确初始化";
            ss["In member function"] = "在成员函数";
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