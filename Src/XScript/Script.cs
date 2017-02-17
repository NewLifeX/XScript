using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.Win32;
using NewLife.IO;
using NewLife.Log;
using NewLife.Reflection;

namespace NewLife.XScript
{
    /// <summary>脚本</summary>
    public class Script
    {
        /// <summary>脚本配置</summary>
        public static ScriptConfig Config { get; set; }

        public static ScriptEngine ProcessFile(String file)
        {
            var config = Config;

            var sc = new ScriptCode(file);
            //// 加入参数中标明的程序集
            //sc.AddRef(config.Assembly);

            // 使用VisualStudio打开源码文件进行编辑
            if (config.Vs)
            {
                OpenWithVs(sc);
                return null;
            }

            var dir = Path.GetDirectoryName(file);
            //Environment.CurrentDirectory = dir;
            //PathHelper.BaseDirectory = dir;
            Environment.SetEnvironmentVariable("XScriptFile", file);
            Host.File = file;

            var se = ScriptEngine.Create(sc.ReadCode(true), false);
            if (Config.Debug) se.Log = XTrace.Log;
            se.WorkingDirectory = dir;

            // 添加默认命名空间
            se.NameSpaces.Add("NewLife.XScript");
            se.NameSpaces.Add("NewLife.Build");

            // 引用程序集
            if (sc.Refs.Count > 0) se.ReferencedAssemblies.AddRange(sc.GetRefArray());

            // 调试状态下输出最终代码
            if (config.Debug)
            {
                var codefile = Path.ChangeExtension(file, "code.cs");
                File.WriteAllText(codefile, se.FinalCode);
            }

            // 从源码读取配置
            config.ParseCode(se.Code);

            // 生成Exe
            if (config.Exe)
                MakeExe(se, file);
            else
                Run(se);

            return se;
        }

        public static Boolean ProcessCode(String code)
        {
            var se = ScriptEngine.Create(code, true);
            if (Config.Debug) se.Log = XTrace.Log;

            // 添加默认命名空间
            se.NameSpaces.Add("NewLife.Build");
            se.ReferencedAssemblies.Add("NewLife.Build.dll");

            Run(se, false);

            return true;
        }

        /// <summary>生成Exe文件</summary>
        /// <param name="session"></param>
        /// <param name="codefile"></param>
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

        /// <summary>使用VisualStudio打开源码文件进行编辑</summary>
        /// <param name="sc"></param>
        static Boolean OpenWithVs(ScriptCode sc)
        {
            // 先读取一次，让其分析Assembly引用
            var code = sc.ReadCode();
            code = sc.GetRefStr();
            if (!String.IsNullOrEmpty(code)) code += Environment.NewLine;

            var se = ScriptEngine.Create(sc.ReadCode(false), false);
            if (Config.Debug) se.Log = XTrace.Log;
            code += se.FinalCode;
            File.WriteAllText(sc.CodeFile, code);

            // 判断项目文件是否存在，若不存在，则根据源码文件生成项目
            var codefile = sc.CodeFile;
            var asm = Assembly.GetExecutingAssembly();
            var name = Path.GetFileNameWithoutExtension(codefile) + ".csproj";
            var dir = codefile.ToLower().MD5();
            var proj = Path.GetDirectoryName(asm.Location).CombinePath("Projs", dir, name);

            MakeProj(sc, proj);

            // 找到安装VisualStudio地址，暂时还不支持Express
            var root = Registry.ClassesRoot;
            var vs = "";
            for (int i = 15; i >= 8; i--)
            {
                var reg = root.OpenSubKey(String.Format("VisualStudio.sln.{0}.0", i));
                if (reg != null)
                {
                    reg = reg.OpenSubKey("shell\\Open\\Command");
                    if (reg != null) vs = reg.GetValue("") + "";
                    if (!vs.IsNullOrWhiteSpace()) break;
                }
            }
            if (vs.IsNullOrWhiteSpace())
            {
                XTrace.WriteLine("无法找到VisualStudio！");
                return false;
            }

            vs = vs.TrimEnd("\"%1\"").Trim().Trim('\"');
            var sln = Path.ChangeExtension(proj, "sln");
            Process.Start(vs, String.Format("\"{0}\"", sln));

            return true;
        }

        static void MakeProj(ScriptCode sc, String proj)
        {
            if (!File.Exists(proj))
            {
                XTrace.WriteLine("释放csproj模版到：{0}", proj);
                FileSource.ReleaseFile(null, "tmpCmd.csproj", proj, true);
            }

            var doc = new XmlDocument();
            doc.Load(proj);

            var uri = "http://schemas.microsoft.com/developer/msbuild/2003";
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ns", uri);

            var group = doc.SelectSingleNode("//ns:PropertyGroup", nsmgr);
            // Guid
            var node = group.SelectSingleNode("ns:ProjectGuid", nsmgr);
            if (node == null)
            {
                node = doc.CreateElement("ProjectGuid", uri);
                group.AppendChild(node);
                node.InnerText = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
            }
            var guid = node.InnerText.Trim('{', '}');

            // 版本
            node = group.SelectSingleNode("ns:TargetFrameworkVersion", nsmgr);
#if NET4
            node.InnerText = "v4.0";
#else
            node.InnerText = "v2.0";
#endif
            // 程序集名称
            node = group.SelectSingleNode("ns:AssemblyName", nsmgr);
            if (node.InnerText.IsNullOrWhiteSpace()) node.InnerText = Path.GetFileNameWithoutExtension(proj);

            // 输出目录
            node = group.SelectSingleNode("ns:OutputPath", nsmgr);
            if (node.InnerText.IsNullOrWhiteSpace()) node.InnerText = Path.GetDirectoryName(proj);

            var items = doc.SelectSingleNode("//ns:ItemGroup", nsmgr);
            // 设定源码文件
            node = items.SelectSingleNode("ns:Compile", nsmgr);
            if (node == null)
            {
                node = doc.CreateElement("Compile", uri);
                items.AppendChild(node);
            }
            //node.InnerText = null;
            var att = node.Attributes["Include"];
            if (att == null)
            {
                att = doc.CreateAttribute("Include");
                node.Attributes.Append(att);
            }
            att.Value = sc.CodeFile;

            // 引用DLL
            foreach (var item in sc.GetRefArray())
            {
                var name = Path.GetFileNameWithoutExtension(item);
                node = items.SelectSingleNode("ns:Reference[@Include='" + name + "']", nsmgr);
                if (node == null)
                {
                    node = doc.CreateElement("Reference", uri);
                    items.AppendChild(node);

                    var node2 = doc.CreateElement("HintPath", uri);
                    node.AppendChild(node2);
                    node2.InnerText = Path.GetDirectoryName(sc.CodeFile).CombinePath(item).GetFullPath();
                }
                att = node.Attributes["Include"];
                if (att == null)
                {
                    att = doc.CreateAttribute("Include");
                    node.Attributes.Append(att);
                }
                att.Value = name;
            }

            doc.Save(proj);

            var sln = Path.ChangeExtension(proj, "sln");
            if (!File.Exists(sln))
            {
                XTrace.WriteLine("释放sln模版到：{0}", sln);
                FileSource.ReleaseFile(null, "tmpCmd.sln", sln, true);

                var txt = File.ReadAllText(sln);
                txt = txt.Replace("{$SlnGUID}", Guid.NewGuid().ToString().ToUpper());
                txt = txt.Replace("{$GUID}", guid);
                txt = txt.Replace("{$Name}", Path.GetFileNameWithoutExtension(proj));
                File.WriteAllText(sln, txt, Encoding.UTF8);
            }
        }

        public static void Run(ScriptEngine se, Boolean showTip = true)
        {
            // 预编译
            se.Compile();

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

                se.Compile();
                Object param = null;
                // 如果Main函数带有参数，则需要传入参数
                if (se.Method.GetParameters().Length > 0)
                {
                    var ps = Environment.GetCommandLineArgs();
                    if (ps.Length > 1)
                    {
                        ps = ps.Skip(1).ToArray();
                        param = ps;
                    }
                }
                // param为空时，可能导致内部得到一个带有空元素的数组，导致无法反射调用Main方法
                var rs = se.Invoke(param != null ? new Object[] { param } : null);
                if (se.IsExpression)
                {
                    var old = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("结果：");
                    Console.ForegroundColor = old;
                    Console.WriteLine(rs);
                }

                if (!Config.NoTime)
                {
                    sw.Stop();

                    var old = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("耗时：{0}", sw.Elapsed);
                    if (showTip && !Config.NoStop)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("  C重复执行，M回主界面，其它键退出");
                    }
                    else
                    {
                        Console.WriteLine();
                    }
                    Console.ForegroundColor = old;
                }
            }
        }
    }
}