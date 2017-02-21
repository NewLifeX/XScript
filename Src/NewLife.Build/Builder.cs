using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

        /// <summary>是否修正日志为中文</summary>
        public Boolean FixLog { get; set; } = true;
        #endregion

        #region 工厂构造
        /// <summary>不允许外部实例化</summary>
        protected Builder() { }

        static Builder()
        {
            var oc = ObjectContainer.Current;
            foreach (var item in typeof(Builder).GetAllSubclasses(true))
            {
                try
                {
                    //var obj = item.CreateInstance() as Builder;
                    //oc.Register<Builder>(obj, obj.Name);

                    var name = item.GetDisplayName() ?? item.Name;
                    oc.Register(typeof(Builder), item, null, name);
                }
                catch (Exception ex)
                {
                    if (XTrace.Debug) XTrace.WriteException(ex);
                }
            }
        }

        /// <summary>根据指定的编译器名称来创建编译器</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Builder Create(String name)
        {
            try
            {
                var builder = ObjectContainer.Current.Resolve<Builder>(name);
                if (builder == null)
                {
                    // 猜测大小写错误
                    var b = ObjectContainer.Current.ResolveAll(typeof(Builder)).FirstOrDefault(m => (m.Identity + "").EqualIgnoreCase(name));
                    var msg = "无法找到编译器 {0}".F(name);
                    if (b != null) msg = "{0}，你需要的可能是 {1}".F(msg, b.Identity);
                    throw new Exception(msg);
                }

                return builder;
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                throw ex;
            }
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
        /// <summary>C/C++编译器</summary>
        public String Complier { get; set; }
        /// <summary>汇编</summary>
        public String Asm { get; set; }
        /// <summary>链接目标文件</summary>
        public String Link { get; set; }
        /// <summary>链接静态库</summary>
        public String Ar { get; set; }
        /// <summary>导出对象</summary>
        public String ObjCopy { get; set; }
        /// <summary>头文件包含目录</summary>
        public String IncPath { get; set; }
        /// <summary>库文件包含目录</summary>
        public String LibPath { get; set; }

        /// <summary>初始化</summary>
        /// <param name="addlib"></param>
        /// <returns></returns>
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
        public String CPU { get; set; }

        /// <summary>分散加载文件</summary>
        public String Scatter { get; set; }

        /// <summary>重新编译时间，默认60分钟</summary>
        public Int32 RebuildTime { get; set; } = 0;

        /// <summary>是否使用Linux标准</summary>
        public Boolean Linux { get; set; }

        /// <summary>分步编译。优先选用与目录同名的静态库</summary>
        public Boolean Partial { get; set; }

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
        public String ExtCompiles { get; set; }

        /// <summary>扩展编译集合</summary>
        public String ExtBuilds { get; set; }
        #endregion

        #region 主要编译方法
        /// <summary>根路径</summary>
        protected String _Root;

        /// <summary>获取编译用的命令行</summary>
        /// <param name="cpp">是否C++</param>
        /// <returns></returns>
        public abstract String GetCompileCommand(Boolean cpp);

        /// <summary>检查源码文件是否需要编译</summary>
        /// <param name="src"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        protected virtual Boolean Check(String src, FileInfo obj)
        {
            if (!obj.Exists) return true;
            if (obj.LastWriteTime < src.AsFile().LastWriteTime) return true;

            if (RebuildTime == 0) return false;
            if (RebuildTime > 0)
            {
                // 单独验证源码文件的修改时间不够，每小时无论如何都编译一次新的
                if (obj.LastWriteTime.AddMinutes(RebuildTime) > DateTime.Now) return false;
            }

            return true;
        }

        /// <summary>编译源文件</summary>
        /// <param name="cmd"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public Int32 Compile(String cmd, String file)
        {
            var objName = GetObjPath(file);

            // 如果文件太新，则不参与编译
            var obj = (objName + ".o").AsFile();
            if (!Check(file, obj)) return -2;

            obj.DirectoryName.EnsureDirectory(false);

            var sb = new StringBuilder();
            sb.Append(cmd);

            foreach (var item in Includes)
            {
                sb.AppendFormat(" -I{0}", item);
            }

            var rs = OnCompile(file);
            if (!rs.IsNullOrEmpty()) sb.AppendFormat(" {0}", rs);

            // 先删除目标文件
            if (obj.Exists) obj.Delete();

            return Complier.Run(sb.ToString(), 100, WriteLog);
        }

        /// <summary>编译输出</summary>
        /// <param name="file"></param>
        protected virtual String OnCompile(String file)
        {
            var sb = new StringBuilder();
            var objName = GetObjPath(file);
            sb.AppendFormat(" -o \"{0}.o\"", objName);
            sb.AppendFormat(" -c \"{0}\"", file);

            return sb.ToString();
        }

        /// <summary>编译汇编程序</summary>
        /// <param name="file"></param>
        /// <param name="showCmd"></param>
        /// <returns></returns>
        public Int32 Assemble(String file, Boolean showCmd)
        {
            var lstName = GetListPath(file);
            var objName = GetObjPath(file);

            // 如果文件太新，则不参与编译
            var obj = (objName + ".o").AsFile();
            if (!Check(file, obj)) return -2;

            obj.DirectoryName.EnsureDirectory(false);

            var cmd = OnAssemble(file);

            if (showCmd)
            {
                Console.Write("汇编参数：");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(cmd);
                Console.ResetColor();
            }

            var sb = new StringBuilder();
            sb.Append(cmd);
            sb.AppendFormat(" \"{0}\" -o \"{1}.o\"", file, objName);

            // 先删除目标文件
            if (obj.Exists) obj.Delete();

            return Asm.Run(sb.ToString(), 100, WriteLog);
        }

        /// <summary>汇编程序</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected virtual String OnAssemble(String file) { return null; }

        /// <summary>编译所有文件</summary>
        /// <returns></returns>
        public Int32 CompileAll()
        {
            Objs.Clear();
            var count = 0;

            if (Files.Count == 0) return 0;

            // 计算根路径，输出的对象文件以根路径下子路径的方式存放
            GetRoot();

            // 提前创建临时目录
            var obj = GetObjPath(null);
            var list = new List<String>();

            var cmd = GetCompileCommand(true);
            var cmd2 = GetCompileCommand(false);

            Console.Write("编译参数：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            if (Files.Any(e => e.EndsWithIgnoreCase(".cpp", ".cxx")))
                Console.WriteLine(cmd);
            if (Files.Any(e => e.EndsWithIgnoreCase(".c")))
                Console.WriteLine(cmd2);
            Console.ResetColor();

            var asm = 0;
            foreach (var item in Files)
            {
                var rs = 0;
                var sw = new Stopwatch();
                sw.Start();
                switch (Path.GetExtension(item).ToLower())
                {
                    case ".c":
                        rs = Compile(cmd2, item);
                        break;
                    case ".cpp":
                    case ".cxx":
                        rs = Compile(cmd, item);
                        break;
                    case ".s":
                        rs = Assemble(item, asm == 0);
                        if (rs != -2) asm++;
                        break;
                    default:
                        break;
                }

                sw.Stop();

                if (rs == 0 || rs == -1)
                {
                    var fi = item;
                    var root = _Root;
                    if (fi.StartsWith(root)) fi = fi.Substring(root.Length);
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
            var left = 0;
            try
            {
                // 如果外部把编译结果输出到文本文件，那么这里会抛出异常
                left = Console.CursorLeft;
            }
            catch { }
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
                try
                {
                    // 如果外部把编译结果输出到文本文件，那么这里会抛出异常
                    Console.CursorLeft = left;
                }
                catch
                {
                    Console.WriteLine();
                }
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
        public Int32 BuildLib(String name = null)
        {
            if (Objs.Count == 0) return 0;

            var ext = Path.GetExtension(name);
            if (!ext.IsNullOrEmpty()) name = name.TrimEnd(ext);

            name = GetOutputName(name);
            XTrace.WriteLine("链接：{0}", name);

            if (ext.IsNullOrEmpty()) ext = ".lib";
            var lib = name.EnsureEnd(ext);
            var sb = new StringBuilder();
            sb.Append(OnBuildLib(lib));

            Console.Write("链接参数：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(sb);
            Console.ResetColor();

            if (Objs.Count < 6) Console.Write("使用对象文件：");
            foreach (var item in Objs)
            {
                sb.Append(" ");
                sb.Append(item);
                if (Objs.Count < 6) Console.Write(" {0}", item);
            }
            if (Objs.Count < 6) Console.WriteLine();

            LoadLib(sb);

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

            return rs;
        }

        /// <summary>链接静态库</summary>
        /// <returns></returns>
        protected virtual String OnBuildLib(String lib)
        {
            return null;
        }

        /// <summary>编译目标文件</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Int32 Build(String name = null)
        {
            if (Objs.Count == 0) return 0;

            var ext = Path.GetExtension(name);
            if (!ext.IsNullOrEmpty()) name = name.TrimEnd(ext);

            name = GetOutputName(name);
            Console.WriteLine();
            XTrace.WriteLine("生成：{0}", name);

            var sb = new StringBuilder();
            sb.Append(OnBuild(name));

            Console.Write("生成参数：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(sb);
            Console.ResetColor();

            if (Objs.Count < 6) Console.Write("使用对象文件：");
            foreach (var item in Objs)
            {
                sb.Append(" ");
                sb.Append(item);
                if (Objs.Count < 6) Console.Write(" {0}", item);
            }
            if (Objs.Count < 6) Console.WriteLine();

            LoadLib(sb);

            if (!ExtBuilds.IsNullOrEmpty()) sb.AppendFormat(" {0}", ExtBuilds.Trim());

            var objName = GetObjPath(name);
            var axf = objName.EnsureEnd(".axf");
            XTrace.WriteLine("链接：{0}", axf);

            var rs = Link.Run(sb.ToString(), 3000, WriteLog);
            if (rs != 0) return rs;

            // 预处理axf。修改编译信息
            BuildHelper.WriteBuildInfo(axf);

            if (ext.IsNullOrEmpty()) ext = ".bin";
            var target = name.EnsureEnd(ext);
            XTrace.WriteLine("生成：{0}", target);
            Console.WriteLine("");

            Dump(axf, target);

            if (name.Contains("\\")) name = name.Substring("\\", "_");
            if (rs == 0)
                "编译目标{0}完成".F(name).SpeakAsync();
            else
                "编译目标{0}失败".F(name).SpeakAsync();

            return rs;
        }

        /// <summary>链接静态库</summary>
        /// <returns></returns>
        protected virtual String OnBuild(String lib)
        {
            return null;
        }

        /// <summary>加载库文件</summary>
        /// <param name="sb"></param>
        protected virtual void LoadLib(StringBuilder sb)
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

            Console.WriteLine("使用静态库：");
            foreach (var item in dic)
            {
                sb.Append(" ");
                sb.Append(item.Value);
                Console.WriteLine("\t{0}\t{1}", item.Key, item.Value);
            }
        }

        /// <summary>导出目标文件</summary>
        /// <param name="axf"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        protected virtual Boolean Dump(String axf, String target) { return false; }
        #endregion

        #region 辅助方法
        /// <summary>添加指定目录所有文件</summary>
        /// <param name="path">要编译的目录</param>
        /// <param name="exts">后缀过滤</param>
        /// <param name="allSub">是否添加所有子目录文件</param>
        /// <param name="excludes">要排除的文件</param>
        /// <returns></returns>
        public Int32 AddFiles(String path, String exts = "*.c;*.cpp", Boolean allSub = true, String excludes = null)
        {
            // 目标目录也加入头文件搜索
            //AddIncludes(path);

            var count = 0;

            // 要排除的集合
            var excs = new HashSet<String>((excludes + "").Split(",", ";"), StringComparer.OrdinalIgnoreCase);

            path = path.GetFullPath().EnsureEnd("\\");
            if (String.IsNullOrEmpty(exts)) exts = "*.c;*.cpp";
            foreach (var item in path.AsDirectory().GetAllFiles(exts, allSub))
            {
                if (!item.Extension.EqualIgnoreCase(".c", ".cpp", ".s")) continue;

                // 检查指定目录下是否有同名静态库
                if (Partial && CheckPartial(item.Directory, path)) continue;

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

        /// <summary>检查指定目录下是否有同名静态库</summary>
        /// <param name="di"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        private Boolean CheckPartial(DirectoryInfo di, String root)
        {
            var fi = di.GetAllFiles("*.lib;*.a").FirstOrDefault();
            if (fi != null && fi.Exists)
            {
                if (!Libs.Contains(fi.FullName))
                {
                    var lib = new LibFile(fi.FullName);
                    WriteLog("发现静态库：{0, -12} {1}".F(lib.Name, fi.FullName));
                    Libs.Add(fi.FullName);
                }
                return true;
            }

            var p = di.Parent;
            if (p == null || p == di) return false;
            // 截止到当前目录
            if (p.FullName.EnsureEnd("\\").EqualIgnoreCase(root.GetFullPath().EnsureEnd("\\"))) return false;

            return CheckPartial(p, root);
        }

        /// <summary>添加包含目录</summary>
        /// <param name="path"></param>
        /// <param name="sub"></param>
        /// <param name="allSub"></param>
        public void AddIncludes(String path, Boolean sub = false, Boolean allSub = false)
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
                    if (item.Name.EqualIgnoreCase("List", "Obj", "ObjD", "Log")) continue;

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

        /// <summary>添加库文件</summary>
        /// <param name="path"></param>
        /// <param name="filter"></param>
        /// <param name="allSub"></param>
        public void AddLibs(String path, String filter = null, Boolean allSub = false)
        {
            path = path.GetFullPath();
            if (!Directory.Exists(path)) return;

            if (filter.IsNullOrEmpty()) filter = "*.lib;*.a";
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

        /// <summary>添加对象文件</summary>
        /// <param name="path"></param>
        /// <param name="filter"></param>
        /// <param name="allSub"></param>
        public void AddObjs(String path, String filter = null, Boolean allSub = false)
        {
            path = path.GetFullPath();
            if (!Directory.Exists(path)) return;

            if (filter.IsNullOrEmpty()) filter = "*.o";
            //var opt = allSub ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var item in path.AsDirectory().GetAllFiles(filter, allSub))
            {
                // 不包含，直接增加
                if (!Objs.Contains(item.FullName))
                {
                    var lib = new LibFile(item.FullName);
                    WriteLog("发现对象文件：{0, -12} {1}".F(lib.Name, item.FullName));
                    Objs.Add(item.FullName);
                }
            }
        }

        internal class LibFile
        {
            /// <summary>名称</summary>
            public String Name { get; set; }

            /// <summary>全名</summary>
            public String FullName { get; set; }

            /// <summary>是否调试版文件</summary>
            public Boolean Debug { get; set; }

            /// <summary>是否精简版文件</summary>
            public Boolean Tiny { get; set; }

            public LibFile(String file)
            {
                FullName = file;
                Name = Path.GetFileNameWithoutExtension(file);
                Debug = Name.EndsWith("D");
                Tiny = Name.EndsWith("T");
                Name = Name.TrimEnd('D', 'T');
            }
        }

        private void GetRoot()
        {
            // 计算根路径，输出的对象文件以根路径下子路径的方式存放
            var di = Files.First().AsFile().Directory;
            var root = di.FullName;
            foreach (var item in Files)
            {
                while (!item.StartsWithIgnoreCase(root))
                {
                    di = di.Parent;
                    if (di == null) break;

                    root = di.FullName;
                }
                if (di == null) break;
            }
            // 使用源码路径计算根路径，而不使用库路径
            //foreach (var item in Libs)
            //{
            //    while (!item.StartsWithIgnoreCase(root))
            //    {
            //        di = di.Parent;
            //        if (di == null) break;

            //        root = di.FullName;
            //    }
            //    if (di == null) break;
            //}
            root = root.EnsureEnd("\\");
            Console.WriteLine("根目录：{0}", root);
            _Root = root;
        }

        /// <summary>获取输出名</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public String GetOutputName(String name)
        {
            if (name.IsNullOrEmpty())
            {
                var file = Environment.GetEnvironmentVariable("XScriptFile");
                if (!file.IsNullOrEmpty())
                {
                    file = Path.GetFileNameWithoutExtension(file);
                    name = file.TrimStart("Build_", "编译_", "Build", "编译").TrimEnd(".cs", "_GCC", "_ICC");
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

        /// <summary>输出目录。obj/list等位于该目录下，默认当前目录</summary>
        public String Output = "";

        /// <summary>获取对象文件路径</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected String GetObjPath(String file)
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

        /// <summary>获取列表文件目录</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected String GetListPath(String file)
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
        /// <summary>输出日志</summary>
        /// <param name="msg"></param>
        protected void WriteLog(String msg)
        {
            if (msg.IsNullOrEmpty()) return;

            // 截取前面部分
            var root = _Root;
            if (!root.IsNullOrEmpty())
            {
                if (msg.StartsWithIgnoreCase(root)) msg = msg.Substring(root.Length);
                if (msg.Contains(root)) msg = msg.Replace(root, null);
            }

            if (FixLog) msg = FixWord(msg);

            var clr = GetColor(Thread.CurrentThread.ManagedThreadId);
            if (msg.StartsWithIgnoreCase("错误", "Error", "致命错误", "Fatal error") || msg.Contains("Error:") || msg.Contains("错误:"))
                clr = ConsoleColor.Red;

            Console.ForegroundColor = clr;
            Console.WriteLine(msg);
            Console.ResetColor();
        }

        static Dictionary<Int32, ConsoleColor> dic = new Dictionary<Int32, ConsoleColor>();
        static ConsoleColor[] colors = new ConsoleColor[] {
            ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Magenta, ConsoleColor.White, ConsoleColor.Yellow,
            ConsoleColor.DarkGreen, ConsoleColor.DarkCyan, ConsoleColor.DarkMagenta, ConsoleColor.DarkRed, ConsoleColor.DarkYellow };
        private ConsoleColor GetColor(Int32 threadid)
        {
            if (threadid == 1) return ConsoleColor.Gray;

            // 好像因为dic.TryGetValue也会引发线程冲突，真是悲剧！
            lock (dic)
            {
                ConsoleColor cc;
                var key = threadid;
                if (!dic.TryGetValue(key, out cc))
                {
                    //lock (dic)
                    {
                        //if (!dic.TryGetValue(key, out cc))
                        {
                            cc = colors[dic.Count % colors.Length];
                            dic[key] = cc;
                        }
                    }
                }

                return cc;
            }
        }

        /// <summary>片段字典集合</summary>
        public Dictionary<String, String> Words { get; set; } = new Dictionary<String, String>();

        /// <summary>函数输出日志</summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public String FixWord(String msg)
        {
            if (Words.Count == 0) InitWord();

            foreach (var item in Words)
            {
                //msg = msg.Replace(item.Key, item.Value);
                msg = Replace(msg, item.Key, item.Value);
            }

            return msg;
        }

        private String Replace(String str, String oldValue, String newValue)
        {
            var p = 0;
            while (true)
            {
                p = str.IndexOf(oldValue, p, StringComparison.OrdinalIgnoreCase);
                if (p < 0) break;

                str = str.Substring(0, p) + newValue + str.Substring(p + oldValue.Length);
                p += newValue.Length;
            }

            return str;
        }

        /// <summary>初始化关键字</summary>
        protected virtual void InitWord()
        {
            var ss = Words;
            ss["warning:"] = "警告:";
            ss["note:"] = "提示:";
            ss["Fatal error"] = "致命错误";
            ss["fatal error"] = "致命错误";
            ss["error:"] = "错误:";
            ss[" warnings"] = "警告";
            ss[" errors"] = "错误";
            ss[" error"] = "错误";
            ss["Could not open file"] = "无法打开文件";
            ss["No such file or directory"] = "文件或目录不存在";
            ss["Undefined symbol"] = "未定义标记";
            ss["referred from"] = "引用自";
            ss["Program Size"] = "程序大小";
            ss["Finished "] = "完成 ";
            ss["declared at"] = "声明于";
            ss["identifier "] = "标记 ";
            ss["In function"] = "在函数";
            ss["pure virtual function"] = "纯虚函数";
            ss["function "] = "函数 ";
            ss["was declared but never referenced"] = "被声明但从未被引用";
            ss["expected a "] = "预期一个";
            ss["expected an expression"] = "预期一个表达式";
            ss[", line "] = ", 行 ";
            ss["variable "] = "变量";
            ss["is ambiguous"] = "不明确";
        }
        #endregion
    }
}