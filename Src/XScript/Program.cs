using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NewLife.Log;
using NewLife.Reflection;

namespace NewLife.XScript
{
    class Program
    {
        private static ScriptConfig _Config;
        /// <summary>脚本配置</summary>
        public static ScriptConfig Config { get { return _Config; } set { _Config = value; } }

        static void Main(string[] args)
        {
            // 分解参数
            try
            {
                Config = ScriptConfig.Parse(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
                return;
            }

            var asmx = AssemblyX.Create(Assembly.GetExecutingAssembly());
            Console.Title = asmx.Title;

            if (Config.Debug) XTrace.UseConsole();

            //XTrace.TempPath = "XTemp";

            // 发送到菜单
            ThreadPool.QueueUserWorkItem(s => SetSendTo());

            if (args == null || args.Length == 0 || args[0] == "?" || args[0] == "/?")
            {
                // 输出版权信息
                ShowCopyright();

                // 显示帮助菜单
                ShowHelp();

                Console.ReadKey(true);
            }
            else
            {
                // 加上源文件路径
                Console.Title += " " + Config.File;

                if (!Config.NoLogo) ShowCopyright();

                try
                {
                    var file = Config.File;
                    if (!File.Exists(file)) throw new FileNotFoundException(String.Format("文件{0}不存在！", file), file);

                    if (Config.Debug) Console.WriteLine("脚本：{0}", file);

                    // 增加源文件路径，便于调试纠错
                    if (!Path.IsPathRooted(file)) file = Path.Combine(Environment.CurrentDirectory, file);

                    Process(file);
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                    if (!Config.Debug) Console.WriteLine(ex.ToString());
                }

                // 暂停，等待客户查看输出
                if (!Config.NoStop)
                {
                    Console.WriteLine("任意键退出……");
                    Console.ReadKey();
                }
            }
        }

        static void Process(String file)
        {
            var code = Helper.ReadCode(file);

            // 分析要导入的第三方程序集。默认包含XScript所在目录的所有程序集
            code = "//Assembly=" + AppDomain.CurrentDomain.BaseDirectory + Environment.NewLine + code;
            // 以及源代码所在目录的所有程序集
            code = "//Assembly=" + Path.GetDirectoryName(file) + Environment.NewLine + code;
            var rs = Helper.ParseAssembly(code);
            rs = Helper.ExpendAssembly(rs);

            //var vPath = Environment.GetEnvironmentVariable("Path");
            //Environment.SetEnvironmentVariable("Path", vPath += ";" + Path.GetDirectoryName(file));
            Environment.CurrentDirectory = Path.GetDirectoryName(file);

            var session = ScriptEngine.Create(code, false);

            // 加入代码中标明的程序集
            if (rs.Length > 0) session.ReferencedAssemblies.AddRange(rs);
            // 加入参数中标明的程序集
            if (!String.IsNullOrEmpty(Config.Assembly))
            {
                rs = Config.Assembly.Split(';');
                rs = Helper.ExpendAssembly(rs);
                if (rs.Length > 0) session.ReferencedAssemblies.AddRange(rs);
            }

            // 调试状态下输出最终代码
            if (Config.Debug)
            {
                session.GenerateCode();
                //File.WriteAllText(String.Format("{0:yyyyMMdd_HHmmss_fff}.cs", DateTime.Now), se.FinalCode);
                var codefile = Path.ChangeExtension(file, "code.cs");
                File.WriteAllText(codefile, session.FinalCode);
            }

            // 生成Exe
            if (Config.Exe)
            {
                MakeExe(session, file);

                return;
            }

            Run(session);
        }

        static void MakeExe(ScriptEngine session, String codefile)
        {
            var exe = Path.ChangeExtension(codefile, "exe");
            var option = new CompilerParameters();
            option.OutputAssembly = exe;
            option.GenerateExecutable = true;
            option.GenerateInMemory = false;
            option.IncludeDebugInformation = Config.Debug;

            // 生成图标
            if (!Config.NoLogo)
            {
                var ico = "leaf.ico".GetFullPath();
                option.CompilerOptions = String.Format("/win32icon:\"{0}\"", ico);
                if (!File.Exists(ico))
                {
                    var ms = Assembly.GetEntryAssembly().GetManifestResourceStream("NewLife.XScript.leaf.ico");
                    File.WriteAllBytes(ico, ms.ReadBytes());
                }
            }

            var code = session.FinalCode;

            //// 加上版权信息
            //code = "\r\n[assembly: System.Reflection.AssemblyCompany(\"新生命开发团队\")]\r\n[assembly: System.Reflection.AssemblyCopyright(\"(C)2002-2013 新生命开发团队\")]\r\n[assembly: System.Reflection.AssemblyVersion(\"1.0.*\")]\r\n" + code;

            var cr = session.Compile(code, option);
            if (cr.Errors == null || !cr.Errors.HasErrors)
            {
                Console.WriteLine("已生成{0}", exe);
            }
            else
            {
                //var err = cr.Errors[0];
                //Console.WriteLine("{0} {1} {2}({3},{4})", err.ErrorNumber, err.ErrorText, err.FileName, err.Line, err.Column);
                //Console.WriteLine(cr.Errors[0].ToString());
                Console.WriteLine("编译出错：");
                foreach (var item in cr.Errors)
                {
                    Console.WriteLine(item.ToString());
                }
            }
        }

        static void Run(ScriptEngine session)
        {
            // 预编译
            session.Compile();

            //// 提前加载引用
            //foreach (var item in session.ReferencedAssemblies)
            //{
            //    try
            //    {
            //        Assembly.LoadFile(item);
            //    }
            //    catch { }
            //}

            // 考虑到某些要引用的程序集在别的目录
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            var sw = new Stopwatch();
            var times = Config.Times;
            if (times < 1) times = 1;
            while (times-- > 0)
            {
                if (!Config.NoTime)
                {
                    sw.Reset();
                    sw.Start();
                }

                session.Invoke();

                if (!Config.NoTime)
                {
                    sw.Stop();

                    var old = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("执行时间：{0}", sw.Elapsed);
                    //Console.WriteLine("按c键重复执行，其它键退出！");
                    Console.ForegroundColor = old;
                }
            }
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = args.Name;
            if (!String.IsNullOrEmpty(name))
            {
                // 遍历现有程序集
                foreach (var item in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (item.FullName == name) return item;
                }

                // 查找当前目录的程序集，就是源代码所在目录
                var p = name.IndexOf(",");
                if (p >= 0) name = name.Substring(0, p);
                var fs = Directory.GetFiles(Environment.CurrentDirectory, name + ".dll", SearchOption.AllDirectories);
                if (fs != null && fs.Length > 0)
                {
                    // 可能多个，遍历加载
                    foreach (var item in fs)
                    {
                        try
                        {
                            var asm = Assembly.LoadFile(item);
                            if (asm != null && asm.FullName == args.Name) return asm;
                        }
                        catch { }
                    }
                }
            }

            return null;
        }

        static void ShowCopyright()
        {
            var asmx = AssemblyX.Create(Assembly.GetExecutingAssembly());

            var oldcolor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("欢迎使用{0}！", asmx.Title);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("本工具由新生命开发团队开发，{0}！", asmx.Description);
            Console.WriteLine("版权所有：{0}", asmx.Asm.GetCustomAttributeValue<AssemblyCopyrightAttribute, String>());
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(".Net版本：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("v{0}", Environment.Version);
            //Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("编译时间：");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("{0:yyyy-MM-dd HH:mm:ss}", asmx.Compile);
            Console.WriteLine();

            Console.ForegroundColor = oldcolor;
        }

        static void ShowHelp()
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Program).Namespace + ".帮助.txt");
            var txt = stream.ToStr();
            //Console.WriteLine(txt);
            foreach (var item in txt.Split(new String[] { Environment.NewLine }, StringSplitOptions.None))
            {
                // 改变颜色
                if (item.StartsWith("[Color:"))
                {
                    var name = item.Substring("[Color:".Length);
                    name = name.TrimEnd(']');
                    var fix = FieldInfoX.Create(typeof(ConsoleColor), name);
                    if (fix != null) Console.ForegroundColor = (ConsoleColor)fix.GetValue();
                }
                else if (item == "[Pause]")
                {
                    Console.ReadKey(true);
                }
                else
                    Console.WriteLine(item);
            }
        }

        static void SetSendTo()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
            if (!Directory.Exists(dir)) return;

            var asmx = AssemblyX.Create(Assembly.GetExecutingAssembly());

            var file = Path.Combine(dir, asmx.Title + ".lnk");
            // 每次更新，用于覆盖，避免错误
            //if (File.Exists(file)) return;

            //XTrace.WriteLine("添加快捷方式到“发送到”菜单！");

            try
            {
                var sc = new Shortcut();
                sc.Path = Assembly.GetEntryAssembly().Location;
                //sc.Arguments = "启动参数";
                sc.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                sc.Description = asmx.Description;
                sc.Save(file);

                file = Path.Combine(dir, asmx.Title + "（调试）.lnk");
                sc = new Shortcut();
                sc.Path = Assembly.GetEntryAssembly().Location;
                sc.Arguments = "/D";
                sc.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                sc.Description = asmx.Description;
                sc.Save(file);

                file = Path.Combine(dir, asmx.Title + "（生成Exe）.lnk");
                sc = new Shortcut();
                sc.Path = Assembly.GetEntryAssembly().Location;
                sc.Arguments = "/Exe";
                sc.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                sc.Description = asmx.Description;
                sc.Save(file);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine(ex.ToString());
            }
        }
    }
}