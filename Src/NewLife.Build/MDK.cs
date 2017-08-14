using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        private static MDKLocation location = new MDKLocation();

        /// <summary>初始化</summary>
        public MDK()
        {
            Name = "MDK";

            Version = location.Version;
            ToolPath = location.ToolPath;

            RebuildTime = 7 * 24 * 60;
        }
        #endregion

        /// <summary>初始化</summary>
        /// <param name="addlib"></param>
        /// <returns></returns>
        public override Boolean Init(Boolean addlib)
        {
            var basePath = ToolPath;
            if (CLang)
            {
                if (!location.Version2.IsNullOrEmpty()) Version = location.Version2;

                basePath = ToolPath.CombinePath("ARMCLANG\\bin").GetFullPath();
                if (!Directory.Exists(basePath)) basePath = ToolPath.CombinePath("ARMCC\\bin").GetFullPath();
            }
            else
            {
                // CLang编译器用来检查语法非常棒，但是对代码要求很高，我们有很多代码需要改进，暂时不用
                basePath = ToolPath.CombinePath("ARMCC\\bin").GetFullPath();
            }

            Complier = basePath.CombinePath("armcc.exe");
            if (!File.Exists(Complier)) Complier = basePath.CombinePath("armclang.exe");
            Asm = basePath.CombinePath("armasm.exe");
            Link = basePath.CombinePath("armlink.exe");
            Ar = basePath.CombinePath("armar.exe");
            ObjCopy = basePath.CombinePath("fromelf.exe");

            IncPath = basePath.CombinePath("..\\include").GetFullPath();
            LibPath = basePath.CombinePath("..\\lib").GetFullPath();

            return base.Init(addlib);
        }

        #region 主要编译方法
        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        protected override String OnGetCompileCommand(Boolean cpp)
        {
            var sb = new StringBuilder();
            /*
             * -c --cpu Cortex-M0 -D__MICROLIB -g -O3 --apcs=interwork --split_sections -I..\Lib\inc -I..\Lib\CMSIS -I..\SmartOS
             * -DSTM32F030 -DUSE_STDPERIPH_DRIVER -DSTM32F0XX -DGD32 -o ".\Obj\*.o" --omf_browse ".\Obj\*.crf" --depend ".\Obj\*.d"
             *
             * -c --cpu Cortex-M3 -D__MICROLIB -g -O0 --apcs=interwork --split_sections -I..\STM32F1Lib\inc -I..\STM32F1Lib\CMSIS -I..\SmartOS
             * -DSTM32F10X_HD -DDEBUG -DUSE_FULL_ASSERT -o ".\Obj\*.o" --omf_browse ".\Obj\*.crf" --depend ".\Obj\*.d"
             */

            sb.Append("-c");
            if (cpp) sb.Append(" --cpp11");
            //sb.AppendFormat(" --cpu {0} -D__MICROLIB -g -O{1} --exceptions --apcs=interwork --split_sections", CPU, Debug ? 0 : 3);
            sb.AppendFormat(" --cpu {0} -D__MICROLIB -g -O{1} --apcs=interwork --split_sections", CPU, Debug ? 0 : 3);
            sb.Append(" --multibyte_chars --locale \"chinese\"");
            // arm_linux 需要编译器授权支持
            //if(Linux) sb.Append(" --arm_linux");
            // --signed_chars
            if (Linux) sb.Append(" --enum_is_int --wchar32");

            return sb.ToString();
        }

        /// <summary>编译输出</summary>
        /// <param name="file"></param>
        protected override String OnCompile(String file)
        {
            var sb = new StringBuilder();
            var objName = GetObjPath(file);
            if (Preprocess)
            {
                sb.AppendFormat(" -E");
                sb.AppendFormat(" -o \"{0}.{1}\"", objName, Path.GetExtension(file).TrimStart("."));
            }
            else
                sb.AppendFormat(" -o \"{0}.o\"", objName);
            sb.AppendFormat(" -c \"{0}\"", file);
            if (!CLang) sb.AppendFormat(" --depend \"{0}.d\"", objName);

            return sb.ToString();
        }

        /// <summary>汇编程序</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected override String OnAssemble(String file)
        {
            /*
             * --cpu Cortex-M3 -g --apcs=interwork --pd "__MICROLIB SETA 1"
             * --pd "__UVISION_VERSION SETA 515" --pd "STM32F10X_HD SETA 1" --list ".\Lis\*.lst" --xref -o "*.o" --depend "*.d"
             */

            var lstName = GetListPath(file);
            var objName = GetObjPath(file);

            var sb = new StringBuilder();
            sb.AppendFormat("--cpu {0} -g --apcs=interwork --pd \"__MICROLIB SETA 1\"", CPU);
            //sb.AppendFormat(" --pd \"{0} SETA 1\"", Flash);

            //if (GD32) sb.Append(" --pd \"GD32 SETA 1\"");
            foreach (var item in Defines)
            {
                if (!item.IsNullOrWhiteSpace()) sb.AppendFormat(" --pd \"{0} SETA 1\"", item);
            }
            if (Debug) sb.Append(" --pd \"DEBUG SETA 1\"");
            if (Tiny) sb.Append(" --pd \"TINY SETA 1\"");

            sb.AppendFormat(" --list \"{0}.lst\" --xref --depend \"{1}.d\"", lstName, objName);

            return sb.ToString();
        }

        /// <summary>链接静态库</summary>
        /// <returns></returns>
        protected override String OnBuildLib(String lib)
        {
            var sb = new StringBuilder();
            sb.Append("--create -c");
            sb.AppendFormat(" -r \"{0}\"", lib);

            //if (Objs.Count < 6) Console.Write("使用对象文件：");
            //foreach (var item in Objs)
            //{
            //    sb.Append(" ");
            //    sb.Append(item);
            //    if (Objs.Count < 6) Console.Write(" {0}", item);
            //}
            //if (Objs.Count < 6) Console.WriteLine();

            return sb.ToString();
        }

        /// <summary>链接静态库</summary>
        /// <returns></returns>
        protected override String OnBuild(String name)
        {
            /*
             * --cpu Cortex-M3 *.o --library_type=microlib --strict --scatter ".\Obj\SmartOSF1_Debug.sct"
             * --summary_stderr --info summarysizes --map --xref --callgraph --symbols
             * --info sizes --info totals --info unused --info veneers
             * --list ".\Lis\SmartOSF1_Debug.map"
             * -o .\Obj\SmartOSF1_Debug.axf
             *
             * --cpu Cortex-M0 *.o --library_type=microlib --diag_suppress 6803 --strict --scatter ".\Obj\Smart130.sct"
             * --summary_stderr --info summarysizes --map --xref --callgraph --symbols
             * --info sizes --info totals --info unused --info veneers
             * --list ".\Lis\Smart130.map"
             * -o .\Obj\Smart130.axf
             */

            var lstName = GetListPath(name);
            var objName = GetObjPath(name);

            var sb = new StringBuilder();
            sb.AppendFormat("--cpu {0} --library_type=microlib --strict", CPU);
            if (!Scatter.IsNullOrEmpty() && File.Exists(Scatter.GetFullPath()))
            {
                sb.AppendFormat(" --scatter \"{0}\"", Scatter);
                //Console.WriteLine("使用分散加载文件");
            }
            else
            {
                sb.AppendFormat(" --ro-base 0x08000000 --rw-base 0x20000000 --first __Vectors");
                //Console.WriteLine("未使用分散加载文件");
                //Console.WriteLine("--ro-base 0x08000000 --rw-base 0x20000000 --first __Vectors");
            }
            //sb.Append(" --summary_stderr --info summarysizes --map --xref --callgraph --symbols");
            //sb.Append(" --info sizes --info totals --info unused --info veneers");
            sb.Append(" --summary_stderr --info summarysizes --map --xref --callgraph --symbols");
            sb.Append(" --info sizes --info totals --info veneers --diag_suppress L6803 --diag_suppress L6314");

            var axf = objName.EnsureEnd(".axf");
            sb.AppendFormat(" --list \"{0}.map\" -o \"{1}\"", lstName, axf);

            return sb.ToString();
        }

        /// <summary>导出目标文件</summary>
        /// <param name="axf"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        protected override Boolean Dump(String axf, String target)
        {
            var cmd = "";
            if (target.EndsWithIgnoreCase(".bin"))
                cmd = "--bin  -o \"{0}\" \"{1}\"".F(target, axf);
            else if (target.EndsWithIgnoreCase(".hex"))
                cmd = "--i32  -o \"{0}\" \"{1}\"".F(target, axf);
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

                    var header = item.Substring(".o: ");
                    if (header.IsNullOrEmpty()) continue;

                    //header = header.Replace("/", "\\");
                    header = header.Trim().GetFullPath();
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

            return false;
        }
        #endregion

        /// <summary>初始化关键字</summary>
        protected override void InitWord()
        {
            var ss = Words;
            ss["Fatal error"] = "致命错误";
            ss["Could not open file"] = "无法打开文件";
            ss["No such file or directory"] = "文件或目录不存在";
            ss["Undefined symbol"] = "未定义标记";
            ss["referred from"] = "引用自";
            ss["Program Size"] = "程序大小";
            ss["Finished"] = "程序大小";
            ss["declared at"] = "声明于";
            ss["required for copy that was eliminated"] = "已淘汰";
            ss["it is a deleted function"] = "函数已标记为删除";
            ss["be referenced"] = "被引用";
            ss["the format string ends before this argument"] = "格式字符串参数不足";
            ss["has already been declared in the current scope"] = "已在当前区域中定义";
            ss["more than one operator"] = "多于一个运算符";
            ss["matches these operands"] = "匹配该操作";
            ss["operand types are"] = "操作类型";
            ss["no instance of overloaded function"] = "没有函数";
            ss["matches the argument list"] = "匹配参数列表";
            ss["argument types are"] = "参数类型是";
            ss["object type is"] = "对象类型是";
            ss["initial value of reference to non-const must be an lvalue"] = "非常量引用初值必须是左值";
            ss["too many arguments in function call"] = "函数调用参数过多";
            ss["cannot be initialized with a value of type"] = "不能初始化为类型";
            ss["a reference of type"] = "引用类型";
            ss["connot be assigned to an entity of type"] = "不能赋值给类型";
            ss["detected during instantiation of"] = "在检测实例化";
            ss["not const-qualified"] = "非常量约束";
            ss["no instance of constructor"] = "没有构造函数";
            ss["is undefined"] = "未定义";
            ss["declaration is incompatible with"] = "声明不兼容";
            ss["is inaccessible"] = "不可访问";
            ss["expression must have class type"] = "表达式必须是类";
            ss["argument is incompatible with corresponding format string conversion"] = "格式化字符串不兼容参数";
            ss["no suitable constructor exists to convert from"] = "没有合适的构造函数去转换";
            ss["nonstandard form for taking the address of a member function"] = "获取成员函数地址不标准（&Class::Method）";
            ss["argument of type"] = "实参类型";
            ss["is incompatible with parameter of type"] = "不兼容形参类型";
            ss["last line of file ends without a newline"] = "文件结尾需要一行空行";
            ss["declared implicitly"] = "隐式声明";
            ss["Deprecated declaration"] = "拒绝声明";
            ss["give arg types"] = "指定参数类型";
            ss["assignment in condition"] = "在条件语句中赋值";
            ss["declaration may not appear after executable statement in block"] = "声明不应该出现在语句块之后";
            ss["incomplete type is not allowed"] = "不允许使用不完整的类型";
            ss["object of abstract class type"] = "实例化抽象类";
            ss["has no overrider"] = "没有重写";
            ss["pointer to incomplete class type is not allowed"] = "不允许使用不完整类型的指针";

            ss["is not allowed"] = "不允许";

            base.InitWord();
        }
    }

    /// <summary>MDK 6.0，采用LLVM技术的CLang编译器</summary>
    public class MDK6 : MDK
    {
        /// <summary>实例化</summary>
        public MDK6()
        {
            Name = "MDK6";
            CLang = true;
        }

        #region 主要编译方法
        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        protected override String OnGetCompileCommand(Boolean cpp)
        {
            var sb = new StringBuilder();
            /*
             * -xc --target=arm-arm-none-eabi -mcpu=cortex-m3 -c
             * -funsigned-char
             * -D__MICROLIB -gdwarf-3 -O0 -ffunction-sections
             * -I ..\Lib\inc -I ..\Lib\CMSIS -I ..\SmartOS -I ..\SmartOS\Core -I ..\SmartOS\Device
             * -I ..\SmartOS\Kernel
             * -D__UVISION_VERSION="520" -DSTM32F10X_HD -DSTM32F1 -DDEBUG -DUSE_FULL_ASSERT -DR24
             * -o .\Obj\*.o -MD
             */

            sb.Append("-xc++");
            //if (file.EndsWithIgnoreCase(".cpp")) sb.Append(" -std=gnu++11");
            if (cpp) sb.Append(" -std=c++14");
            sb.Append(" --target=arm-arm-none-eabi -funsigned-char -MD");
            sb.AppendFormat(" -mcpu={0} -D__MICROLIB -gdwarf-3 -O{1} -ffunction-sections", CPU.ToLower(), Debug ? 0 : 3);
            sb.Append(" -Warmcc-pragma-arm");

            return sb.ToString();
        }
        #endregion
    }

    class MDKLocation
    {
        #region 属性
        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>版本2</summary>
        public String Version2 { get; set; }

        /// <summary>工具目录</summary>
        public String ToolPath { get; set; }
        #endregion

        public MDKLocation()
        {
            #region 从注册表获取目录和版本
            if (String.IsNullOrEmpty(ToolPath))
            {
                var reg = Registry.LocalMachine.OpenSubKey("Software\\Keil\\Products\\MDK");
                if (reg == null) reg = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Keil\\Products\\MDK");
                if (reg != null)
                {
                    var p = reg.GetValue("Path") + "";
                    var v = reg.GetValue("Version") + "";

                    var v2 = GetVer(p, false);

                    WriteLog("注册表 {0} {1} {2}", p, v, v2);

                    if (Directory.Exists(p))
                    {
                        ToolPath = p;
                        Version = v;
                        Version2 = v2;
                    }
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
                        var ver = GetVer(p, false);
                        if (ver.CompareTo(Version + "") > 0)
                        {
                            ToolPath = p;
                            Version = ver;
                            Version2 = GetVer(p, true);

                            WriteLog("本地 {0} {1} ｛2｝｝", p, ver, Version2);
                        }
                    }
                }
            }
            #endregion

            #region 版本更新
            if (Version.ToLower().CompareTo("v5.17") < 0)
            {
                XTrace.WriteLine("版本 {0} 太旧，准备更新", Version);

                var url = "http://x.newlifex.com/";
                var client = new WebClientX(true, true)
                {
                    Log = XTrace.Log
                };
                var dir = Environment.SystemDirectory.CombinePath("..\\..\\Keil").GetFullPath();
                var file = client.DownloadLinkAndExtract(url, "MDK", dir);
                var p = dir.CombinePath("ARM");
                if (Directory.Exists(p))
                {
                    var ver = GetVer(p, false);
                    if (ver.CompareTo(Version) > 0)
                    {
                        ToolPath = p;
                        Version = ver;
                        Version2 = GetVer(p, true);
                    }
                }
            }
            if (String.IsNullOrEmpty(ToolPath)) throw new Exception("无法获取MDK安装目录！");
            #endregion
        }

        String GetVer(String path, Boolean clang)
        {
            if (path.IsNullOrEmpty()) return null;

            var p = Path.Combine(path, "..\\Tools.ini");
            if (File.Exists(p))
            {
                var dic = File.ReadAllText(p).SplitAsDictionary("=", Environment.NewLine);
                if (dic.TryGetValue("VERSION", out var v)) return v.Trim('\"').EnsureStart("v");
                if (clang && dic.TryGetValue("DEFAULT_ARMCC_VERSION_OTHER", out v)) return v.Trim('\"');
                if (!clang && dic.TryGetValue("DEFAULT_ARMCC_VERSION_CM0", out v)) return v.Trim('\"');
                if (dic.TryGetValue("VERSION", out v)) return v;
            }

            return "";
        }

        void WriteLog(String format, params Object[] args)
        {
            if (XTrace.Debug) XTrace.WriteLine(format, args);
        }
    }
}