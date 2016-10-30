using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;

namespace NewLife.Build
{
    /// <summary>编译器基类</summary>
    public abstract class Builder
    {
        #region 属性
        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>工具目录</summary>
        public String ToolPath { get; set; }
        #endregion

        #region 工厂构造
        protected Builder() { }

        static Builder()
        {
            var oc = ObjectContainer.Current;
            foreach (var item in typeof(Builder).GetAllSubclasses(true))
            {
                var obj = item.CreateInstance() as Builder;
                oc.Register<Builder>(obj, obj.Name);

                //oc.Register(typeof(Builder), item, null, item.Name);
            }
        }

        /// <summary>根据指定的编译器名称来创建编译器</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Builder Create(String name)
        {
            return ObjectContainer.Current.Resolve<Builder>(name);
        }

        /// <summary>所有编译器</summary>
        public static String All
        {
            get
            {
                return ObjectContainer.Current.ResolveAll(typeof(Builder)).Select(e => e.Identity + "").Join(",");
            }
        }
        #endregion

        #region 编译器工具
        public String Complier { get; set; }
        public String Asm { get; set; }
        public String Link { get; set; }
        public String Ar { get; set; }
        public String FromELF { get; set; }
        public String IncPath { get; set; }
        public String LibPath { get; set; }

        public virtual Boolean Init(Boolean addlib = true)
        {
            var tp = ToolPath;
            if (tp.IsNullOrEmpty() || !Directory.Exists(tp))
            {
                XTrace.WriteLine("未找到编译器！");
                return false;
            }

            XTrace.WriteLine("发现 {0} {1} {2}", Name, Version, tp);

            Libs.Clear();
            Objs.Clear();

            // 扫描当前所有目录，作为头文件引用目录
            //var ss = new String[] { ".", "..\\SmartOS" };
            var ss = new String[] { ".", "inc", "include", "lib" };
            foreach (var src in ss)
            {
                var p = src.GetFullPath();
                if (!Directory.Exists(p)) p = ("..\\" + src).GetFullPath();
                if (!Directory.Exists(p)) continue;

                AddIncludes(p, false);
                if (addlib) AddLibs(p);
            }

            return true;
        }
        #endregion

        #region 编译选项
        /// <summary>是否编译调试版。默认true</summary>
        public Boolean Debug { get; set; }

        /// <summary>是否精简版。默认false</summary>
        public Boolean Tiny { get; set; }

        /// <summary>是否仅预处理文件，不编译。默认false</summary>
        public Boolean Preprocess { get; set; }

        /// <summary>处理器。默认M0</summary>
        public String CPU { get; set; } = "Cortex-M0";

        /// <summary>分散加载文件</summary>
        public String Scatter { get; set; }

        private Int32 _Cortex;
        /// <summary>Cortex版本。默认0</summary>
        public Int32 Cortex
        {
            get { return _Cortex; }
            set
            {
                _Cortex = value;
                CPU = "Cortex-M{0}".F(value);
                if (value == 4) CPU += ".fp";
            }
        }

        /// <summary>重新编译时间，默认60分钟</summary>
        public Int32 RebuildTime { get; set; } = 60;

        /// <summary>是否使用Linux标准</summary>
        public Boolean Linux { get; set; }

        /// <summary>定义集合</summary>
        public ICollection<String> Defines { get; private set; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>引用头文件路径集合</summary>
        public ICollection<String> Includes { get; private set; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>源文件集合</summary>
        public ICollection<String> Files { get; private set; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>对象文件集合</summary>
        public ICollection<String> Objs { get; private set; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>库文件集合</summary>
        public ICollection<String> Libs { get; private set; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>扩展编译集合</summary>
        public ICollection<String> ExtCompiles { get; private set; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        /// <summary>扩展编译集合</summary>
        public ICollection<String> ExtBuilds { get; private set; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region 主要编译方法
        private String _Root;

        /// <summary>获取编译用的命令行</summary>
        /// <param name="clang"></param>
        /// <returns></returns>
        public String GetCompileCommand(Boolean clang, Boolean cpp)
        {
            var sb = new StringBuilder();
            if (!clang)
            {
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
            }
            else
            {
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
            }

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

            foreach (var item in Includes)
            {
                sb.AppendFormat(" -I{0}", item);
            }
            //if(Directory.Exists(IncPath)) sb.AppendFormat(" -I{0}", IncPath);

            return sb.ToString();
        }

        public Int32 Compile(String cmd, String file)
        {
            var objName = GetObjPath(file);

            // 如果文件太新，则不参与编译
            var obj = (objName + ".o").AsFile();
            if (obj.Exists)
            {
                if (RebuildTime > 0 && obj.LastWriteTime > file.AsFile().LastWriteTime)
                {
                    // 单独验证源码文件的修改时间不够，每小时无论如何都编译一次新的
                    if (obj.LastWriteTime.AddMinutes(RebuildTime) > DateTime.Now) return -2;
                }
            }

            obj.DirectoryName.EnsureDirectory(false);

            var sb = new StringBuilder();
            sb.Append(cmd);
            if (Preprocess)
            {
                sb.AppendFormat(" -E");
                sb.AppendFormat(" -o \"{0}.{1}\" --omf_browse \"{0}.crf\" --depend \"{0}.d\"", objName, Path.GetExtension(file).TrimStart("."));
            }
            else
                sb.AppendFormat(" -o \"{0}.o\" --omf_browse \"{0}.crf\" --depend \"{0}.d\"", objName);
            sb.AppendFormat(" -c \"{0}\"", file);

            // 先删除目标文件
            if (obj.Exists) obj.Delete();

            return Complier.Run(sb.ToString(), 100, WriteLog);
        }

        public Int32 CompileCLang(String cmd, String file)
        {
            var objName = GetObjPath(file);

            // 如果文件太新，则不参与编译
            var obj = (objName + ".o").AsFile();
            if (obj.Exists)
            {
                if (RebuildTime > 0 && obj.LastWriteTime > file.AsFile().LastWriteTime)
                {
                    // 单独验证源码文件的修改时间不够，每小时无论如何都编译一次新的
                    if (obj.LastWriteTime.AddMinutes(RebuildTime) > DateTime.Now) return -2;
                }
            }

            obj.DirectoryName.EnsureDirectory(false);

            var sb = new StringBuilder();
            sb.Append(cmd);
            if (Preprocess)
            {
                sb.AppendFormat(" -E");
                sb.AppendFormat(" -o \"{0}.{1}\"", objName, Path.GetExtension(file).TrimStart("."));
            }
            else
                sb.AppendFormat(" -o \"{0}.o\"", objName);
            sb.AppendFormat(" -c \"{0}\"", file);

            // 先删除目标文件
            if (obj.Exists) obj.Delete();

            return Complier.Run(sb.ToString(), 100, WriteLog);
        }

        public Int32 Assemble(String file)
        {
            /*
             * --cpu Cortex-M3 -g --apcs=interwork --pd "__MICROLIB SETA 1"
             * --pd "__UVISION_VERSION SETA 515" --pd "STM32F10X_HD SETA 1" --list ".\Lis\*.lst" --xref -o "*.o" --depend "*.d"
             */

            var lstName = GetListPath(file);
            var objName = GetObjPath(file);

            // 如果文件太新，则不参与编译
            var obj = (objName + ".o").AsFile();
            if (obj.Exists)
            {
                if (obj.LastWriteTime > file.AsFile().LastWriteTime)
                {
                    // 单独验证源码文件的修改时间不够，每小时无论如何都编译一次新的
                    if (obj.LastWriteTime.AddHours(1) > DateTime.Now) return -2;
                }
            }

            obj.DirectoryName.EnsureDirectory(false);

            var sb = new StringBuilder();
            sb.AppendFormat("--cpu {0} -g --apcs=interwork --pd \"__MICROLIB SETA 1\"", CPU);
            //sb.AppendFormat(" --pd \"{0} SETA 1\"", Flash);

            //if (GD32) sb.Append(" --pd \"GD32 SETA 1\"");
            foreach (var item in Defines)
            {
                sb.AppendFormat(" --pd \"{0} SETA 1\"", item);
            }
            if (Debug) sb.Append(" --pd \"DEBUG SETA 1\"");
            if (Tiny) sb.Append(" --pd \"TINY SETA 1\"");

            sb.AppendFormat(" --list \"{0}.lst\" --xref -o \"{1}.o\" --depend \"{1}.d\"", lstName, objName);
            sb.AppendFormat(" \"{0}\"", file);

            // 先删除目标文件
            if (obj.Exists) obj.Delete();

            return Asm.Run(sb.ToString(), 100, WriteLog);
        }

        public Int32 CompileAll()
        {
            Objs.Clear();
            var count = 0;

            // 计算根路径，输出的对象文件以根路径下子路径的方式存放
            var di = Files.First().AsFile().Directory;
            _Root = di.FullName;
            foreach (var item in Files)
            {
                while (!item.StartsWithIgnoreCase(_Root))
                {
                    di = di.Parent;
                    if (di == null) break;

                    _Root = di.FullName;
                }
                if (di == null) break;
            }
            _Root = _Root.EnsureEnd("\\");
            Console.WriteLine("根目录：{0}", _Root);

            // 提前创建临时目录
            var obj = GetObjPath(null);
            var list = new List<String>();

            var clang = Complier.Contains("ARMCLANG");
            var cmd = GetCompileCommand(clang, true);
            var cmd2 = GetCompileCommand(clang, false);

            Console.Write("命令参数：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(cmd);
            Console.ResetColor();

            foreach (var item in Files)
            {
                var rs = 0;
                var sw = new Stopwatch();
                sw.Start();
                switch (Path.GetExtension(item).ToLower())
                {
                    case ".c":
                        if (clang)
                            rs = CompileCLang(cmd2, item);
                        else
                            rs = Compile(cmd2, item);
                        break;
                    case ".cpp":
                        if (clang)
                            rs = CompileCLang(cmd, item);
                        else
                            rs = Compile(cmd, item);
                        break;
                    case ".s":
                        rs = Assemble(item);
                        break;
                    default:
                        break;
                }

                sw.Stop();

                if (rs == 0 || rs == -1)
                {
                    var fi = item;
                    if (fi.StartsWith(_Root)) fi = fi.Substring(_Root.Length);
                    Console.Write("编译：{0}\t", fi);
                    var old = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\t {0:n0}毫秒", sw.ElapsedMilliseconds);
                    Console.ForegroundColor = old;
                }

                if (rs <= 0)
                {
                    if (!Preprocess)
                    {
                        //var fi = obj.CombinePath(Path.GetFileNameWithoutExtension(item) + ".o");
                        var fi = GetObjPath(item) + ".o";
                        list.Add(fi);
                    }
                }
            }

            Console.WriteLine("等待编译完成：");
            //var left = Console.CursorLeft;
            var list2 = new List<String>(list);
            var end = DateTime.Now.AddSeconds(10);
            var fs = 0;
            while (fs < Files.Count)
            {
                for (int i = list2.Count - 1; i >= 0; i--)
                {
                    if (File.Exists(list[i]))
                    {
                        fs++;
                        list2.RemoveAt(i);
                    }
                }
                //Console.CursorLeft = left;
                Console.WriteLine();
                Console.Write("\t {0}/{1} = {2:p}", fs, Files.Count, (Double)fs / Files.Count);

                if (DateTime.Now > end)
                {
                    Console.Write(" 等待超时！");
                    break;
                }
                Thread.Sleep(500);
            }
            Console.WriteLine();

            for (int i = 0; i < list.Count; i++)
            {
                if (File.Exists(list[i]))
                {
                    count++;
                    Objs.Add(list[i]);
                }
            }

            return count;
        }

        /// <summary>编译静态库</summary>
        /// <param name="name"></param>
        public void BuildLib(String name = null)
        {
            name = GetOutputName(name);
            XTrace.WriteLine("链接：{0}", name);

            var lib = name.EnsureEnd(".lib");
            var sb = new StringBuilder();
            sb.Append("--create -c");
            sb.AppendFormat(" -r \"{0}\"", lib);

            if (Objs.Count < 6) Console.Write("使用对象文件：");
            foreach (var item in Objs)
            {
                sb.Append(" ");
                sb.Append(item);
                if (Objs.Count < 6) Console.Write(" {0}", item);
            }
            if (Objs.Count < 6) Console.WriteLine();

            var rs = Ar.Run(sb.ToString(), 3000, WriteLog);
            XTrace.WriteLine("链接完成 {0} {1}", rs, lib);

            if (name.Contains("\\")) name = name.Substring("\\");
            var p = name.LastIndexOf("\\");
            if (p >= 0) name = name.Substring(p + 1);
            name = name.Replace("_", " ");
            if (rs == 0)
                "链接静态库{0}完成".F(name).SpeakAsync();
            else
                "链接静态库{0}失败".F(name).SpeakAsync();
        }

        /// <summary>编译目标文件</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Int32 Build(String name = null)
        {
            name = GetOutputName(name);
            Console.WriteLine();
            XTrace.WriteLine("生成：{0}", name);

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
                Console.WriteLine("使用分散加载文件");
            }
            else
            {
                sb.AppendFormat(" --ro-base 0x08000000 --rw-base 0x20000000 --first __Vectors");
                Console.WriteLine("未使用分散加载文件");
                Console.WriteLine("--ro-base 0x08000000 --rw-base 0x20000000 --first __Vectors");
            }
            //sb.Append(" --summary_stderr --info summarysizes --map --xref --callgraph --symbols");
            //sb.Append(" --info sizes --info totals --info unused --info veneers");
            sb.Append(" --summary_stderr --info summarysizes --map --xref --callgraph --symbols");
            sb.Append(" --info sizes --info totals --info veneers --diag_suppress L6803 --diag_suppress L6314");

            foreach (var item in ExtBuilds)
            {
                sb.AppendFormat(" {0}", item.Trim());
            }

            var axf = objName.EnsureEnd(".axf");
            sb.AppendFormat(" --list \"{0}.map\" -o \"{1}\"", lstName, axf);

            if (Objs.Count < 6) Console.Write("使用对象文件：");
            foreach (var item in Objs)
            {
                sb.Append(" ");
                sb.Append(item);
                if (Objs.Count < 6) Console.Write(" {0}", item);
            }
            if (Objs.Count < 6) Console.WriteLine();

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

            Console.WriteLine("使用静态库：");
            foreach (var item in dic)
            {
                sb.Append(" ");
                sb.Append(item.Value);
                Console.WriteLine("\t{0}\t{1}", item.Key, item.Value);
            }

            XTrace.WriteLine("链接：{0}", axf);

            //var rs = Link.Run(sb.ToString(), 3000, WriteLog);
            var rs = Link.Run(sb.ToString(), 3000, msg =>
            {
                if (msg.IsNullOrEmpty()) return;

                // 引用错误可以删除obj文件，下次编译将会得到解决
                /*var p = msg.IndexOf("Undefined symbol");
                if(p >= 0)
                {
                    foreach(var obj in Objs)
                    {
                        if(File.Exists(obj)) File.Delete(obj);
                        var crf = Path.ChangeExtension(obj, ".crf");
                        if(File.Exists(crf)) File.Delete(crf);
                        var dep = Path.ChangeExtension(obj, ".d");
                        if(File.Exists(dep)) File.Delete(dep);
                    }
                }*/

                WriteLog(msg);
            });
            if (rs != 0) return rs;

            // 预处理axf。修改编译信息
            //Helper.WriteBuildInfo(axf);

            var bin = name.EnsureEnd(".bin");
            XTrace.WriteLine("生成：{0}", bin);
            Console.WriteLine("");
            sb.Clear();
            sb.AppendFormat("--bin  -o \"{0}\" \"{1}\"", bin, axf);
            rs = FromELF.Run(sb.ToString(), 3000, WriteLog);

            /*var hex = name.EnsureEnd(".hex");
            XTrace.WriteLine("生成：{0}", hex);
            sb.Clear();
            sb.AppendFormat("--i32  -o \"{0}\" \"{1}\"", hex, axf);
            rs = FromELF.Run(sb.ToString(), 3000, WriteLog);*/

            if (name.Contains("\\")) name = name.Substring("\\", "_");
            if (rs == 0)
                "编译目标{0}完成".F(name).SpeakAsync();
            else
                "编译目标{0}失败".F(name).SpeakAsync();

            return rs;
        }
        #endregion

        #region 辅助方法
        /// <summary>添加指定目录所有文件</summary>
        /// <param name="path">要编译的目录</param>
        /// <param name="exts">后缀过滤</param>
        /// <param name="excludes">要排除的文件</param>
        /// <returns></returns>
        public Int32 AddFiles(String path, String exts = "*.c;*.cpp", Boolean allSub = true, String excludes = null)
        {
            // 目标目录也加入头文件搜索
            //AddIncludes(path);

            var count = 0;

            var excs = new HashSet<String>((excludes + "").Split(",", ";"), StringComparer.OrdinalIgnoreCase);

            path = path.GetFullPath().EnsureEnd("\\");
            if (String.IsNullOrEmpty(exts)) exts = "*.c;*.cpp";
            foreach (var item in path.AsDirectory().GetAllFiles(exts, allSub))
            {
                if (!item.Extension.EqualIgnoreCase(".c", ".cpp", ".s")) continue;

                //Console.Write("添加：{0}\t", item.FullName);

                var flag = true;
                var ex = "";
                if (excs.Contains(item.Name)) { flag = false; ex = item.Name; }
                if (flag)
                {
                    foreach (var elm in excs)
                    {
                        if (item.Name.Contains(elm)) { flag = false; ex = elm; break; }
                    }
                }
                if (!flag)
                {
                    var old2 = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\t 跳过 {0}", ex);
                    Console.ForegroundColor = old2;

                    continue;
                }
                //Console.WriteLine();

                if (!Files.Contains(item.FullName))
                {
                    count++;
                    Files.Add(item.FullName);
                }
            }

            return count;
        }

        public void AddIncludes(String path, Boolean sub = true, Boolean allSub = true)
        {
            path = path.GetFullPath();
            if (!Directory.Exists(path)) return;

            // 有头文件才要，没有头文件不要
            //var fs = path.AsDirectory().GetAllFiles("*.h;*.hpp");
            //if (!fs.Any()) return;

            if (!Includes.Contains(path) && HasHeaderFile(path))
            {
                WriteLog("引用目录：{0}".F(path));
                Includes.Add(path);
            }

            if (sub)
            {
                var opt = allSub ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var item in path.AsDirectory().GetDirectories("*", opt))
                {
                    if (item.FullName.Contains(".svn")) continue;
                    if (item.Name.EqualIgnoreCase("List", "Obj", "ObjRelease", "Log")) continue;

                    if (!Includes.Contains(item.FullName) && HasHeaderFile(item.FullName))
                    {
                        WriteLog("引用目录：{0}".F(item.FullName));
                        Includes.Add(item.FullName);
                    }
                }
            }
        }

        Boolean HasHeaderFile(String path)
        {
            return path.AsDirectory().GetFiles("*.h", SearchOption.AllDirectories).Length > 0;
        }

        public void AddLibs(String path, String filter = null, Boolean allSub = true)
        {
            path = path.GetFullPath();
            if (!Directory.Exists(path)) return;

            if (filter.IsNullOrEmpty()) filter = "*.lib";
            //var opt = allSub ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var item in path.AsDirectory().GetAllFiles(filter, allSub))
            {
                // 不包含，直接增加
                if (!Libs.Contains(item.FullName))
                {
                    var lib = new LibFile(item.FullName);
                    WriteLog("发现静态库：{0, -12} {1}".F(lib.Name, item.FullName));
                    Libs.Add(item.FullName);
                }
            }
        }

        class LibFile
        {
            private String _Name;
            /// <summary>名称</summary>
            public String Name { get { return _Name; } set { _Name = value; } }

            private String _FullName;
            /// <summary>全名</summary>
            public String FullName { get { return _FullName; } set { _FullName = value; } }

            private Boolean _Debug;
            /// <summary>是否调试版文件</summary>
            public Boolean Debug { get { return _Debug; } set { _Debug = value; } }

            private Boolean _Tiny;
            /// <summary>是否精简版文件</summary>
            public Boolean Tiny { get { return _Tiny; } set { _Tiny = value; } }

            public LibFile(String file)
            {
                FullName = file;
                Name = Path.GetFileNameWithoutExtension(file);
                Debug = Name.EndsWithIgnoreCase("D");
                Tiny = Name.EndsWithIgnoreCase("T");
                Name = Name.TrimEnd("D", "T");
            }
        }

        private String GetOutputName(String name)
        {
            if (name.IsNullOrEmpty())
            {
                var file = Environment.GetEnvironmentVariable("XScriptFile");
                if (!file.IsNullOrEmpty())
                {
                    file = Path.GetFileNameWithoutExtension(file);
                    name = file.TrimStart("Build_", "编译_", "Build", "编译").TrimEnd(".cs");
                }
            }
            if (name.IsNullOrEmpty())
                name = ".".GetFullPath().AsDirectory().Name;
            else if (name.StartsWith("_"))
                name = ".".GetFullPath().AsDirectory().Name + name.TrimStart("_");
            else if (name.EndsWith("\\"))
                name += ".".GetFullPath().AsDirectory().Name;
            if (Tiny)
                name = name.EnsureEnd("T");
            else if (Debug)
                name = name.EnsureEnd("D");

            return name;
        }

        // 输出目录。obj/list等位于该目录下，默认当前目录
        public String Output = "";

        private String GetObjPath(String file)
        {
            var objName = "Obj";
            if (Tiny)
                objName += "T";
            else if (Debug)
                objName += "D";
            objName = Output.CombinePath(objName);
            objName.GetFullPath().EnsureDirectory(false);
            if (!file.IsNullOrEmpty())
            {
                //objName += "\\" + Path.GetFileNameWithoutExtension(file);
                var p = file.IndexOf(_Root, StringComparison.OrdinalIgnoreCase);
                if (p == 0) file = file.Substring(_Root.Length);

                objName = objName.CombinePath(file);
                p = objName.LastIndexOf('.');
                if (p > 0) objName = objName.Substring(0, p);
            }

            return objName;
        }

        private String GetListPath(String file)
        {
            var lstName = "List";
            lstName = Output.CombinePath(lstName);
            lstName.GetFullPath().EnsureDirectory(false);
            if (!file.IsNullOrEmpty())
                lstName += "\\" + Path.GetFileNameWithoutExtension(file);

            return lstName;
        }
        #endregion

        #region 日志
        void WriteLog(String msg)
        {
            if (msg.IsNullOrEmpty()) return;

            msg = FixWord(msg);
            if (msg.StartsWithIgnoreCase("错误", "Error", "致命错误", "Fatal error") || msg.Contains("Error:"))
                XTrace.Log.Error(msg);
            else
                XTrace.WriteLine(msg);
        }

        private Dictionary<String, String> _Sections = new Dictionary<String, String>();
        /// <summary>片段字典集合</summary>
        public Dictionary<String, String> Sections { get { return _Sections; } set { _Sections = value; } }

        private Dictionary<String, String> _Words = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        /// <summary>字典集合</summary>
        public Dictionary<String, String> Words { get { return _Words; } set { _Words = value; } }

        public String FixWord(String msg)
        {
            #region 初始化
            var ss = Sections;
            if (ss.Count == 0)
            {
                ss.Add("Fatal error", "致命错误");
                ss.Add("fatal error", "致命错误");
                ss.Add("Could not open file", "无法打开文件");
                ss.Add("No such file or directory", "文件或目录不存在");
                ss.Add("Undefined symbol", "未定义标记");
                ss.Add("referred from", "引用自");
                ss.Add("Program Size", "程序大小");
                ss.Add("Finished", "程序大小");
                ss.Add("declared at", "声明于");
                ss.Add("required for copy that was eliminated", "已淘汰");
                ss.Add("it is a deleted function", "函数已标记为删除");
                ss.Add("be referenced", "被引用");
                ss.Add("the format string ends before this argument", "格式字符串参数不足");
                ss.Add("has already been declared in the current scope", "已在当前区域中定义");
                ss.Add("more than one operator", "多于一个运算符");
                ss.Add("matches these operands", "匹配该操作");
                ss.Add("operand types are", "操作类型");
                ss.Add("no instance of overloaded function", "没有函数");
                ss.Add("matches the argument list", "匹配参数列表");
                ss.Add("argument types are", "参数类型是");
                ss.Add("object type is", "对象类型是");
                ss.Add("initial value of reference to non-const must be an lvalue", "非常量引用初值必须是左值");
                ss.Add("too many arguments in function call", "函数调用参数过多");
                ss.Add("cannot be initialized with a value of type", "不能初始化为类型");
                ss.Add("a reference of type", "引用类型");
                ss.Add("connot be assigned to an entity of type", "不能赋值给类型");
                ss.Add("detected during instantiation of", "在检测实例化");
                ss.Add("not const-qualified", "非常量约束");
                ss.Add("no instance of constructor", "没有构造函数");
                ss.Add("is undefined", "未定义");
                ss.Add("declaration is incompatible with", "声明不兼容");
                ss.Add("is inaccessible", "不可访问");
                ss.Add("expression must have class type", "表达式必须是类");
                ss.Add("argument is incompatible with corresponding format string conversion", "格式化字符串不兼容参数");
                ss.Add("no suitable constructor exists to convert from", "没有合适的构造函数去转换");
                ss.Add("nonstandard form for taking the address of a member function", "获取成员函数地址不标准（&Class::Method）");
                ss.Add("argument of type", "实参类型");
                ss.Add("is incompatible with parameter of type", "不兼容形参类型");
            }

            ss = Words;
            if (ss.Count == 0)
            {
                ss.Add("Error", "错误");
                ss.Add("Warning", "警告");
                ss.Add("Warnings", "警告");
                ss.Add("cannot", "不能");
                ss.Add("identifier", "标识符");
                /*ss.Add("open", "打开");
                ss.Add("source", "源");
                ss.Add("input", "输入");
                ss.Add("file", "文件");
                ss.Add("No", "没有");
                ss.Add("Not", "没有");
                ss.Add("such", "该");
                ss.Add("or", "或");
                ss.Add("And", "与");
                ss.Add("Directory", "目录");
                ss.Add("Enough", "足够");
                ss.Add("Information", "信息");
                ss.Add("to", "去");
                ss.Add("from", "自");
                ss.Add("list", "列出");
                ss.Add("image", "镜像");
                ss.Add("Symbol", "标识");
                ss.Add("Symbols", "标识");
                ss.Add("the", "");
                ss.Add("map", "映射");
                ss.Add("Finished", "完成");
                ss.Add("line", "行");
                ss.Add("messages", "消息");
                ss.Add("this", "这个");
                ss.Add("feature", "功能");
                ss.Add("supported", "被支持");
                ss.Add("on", "在");
                ss.Add("target", "目标");
                ss.Add("architecture", "架构");
                ss.Add("processor", "处理器");
                ss.Add("Undefined", "未定义");
                ss.Add("referred", "引用");*/
            }
            #endregion

            foreach (var item in Sections)
            {
                msg = msg.Replace(item.Key, item.Value);
            }

            //var sb = new StringBuilder();
            //var ss = msg.Trim().Split(" ", ":", "(", ")");
            //var ss = msg.Trim().Split(" ");
            //for (int i = 0; i < ss.Length; i++)
            //{
            //    var rs = "";
            //    if (Words.TryGetValue(ss[i], out rs)) ss[i] = rs;
            //}
            //return String.Join(" ", ss);
            //var ms = Regex.Matches(msg, "");
            msg = Regex.Replace(msg, "(\\w+\\s?)", match =>
            {
                var w = match.Captures[0].Value;
                var rs = "";
                if (Words.TryGetValue(w.Trim(), out rs)) w = rs;
                return w;
            });
            return msg;
        }
        #endregion
    }
}