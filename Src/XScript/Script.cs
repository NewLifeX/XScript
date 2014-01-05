﻿using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using NewLife.IO;
using NewLife.Reflection;
using NewLife.Security;
using NewLife.Log;
using Microsoft.Win32;

namespace NewLife.XScript
{
    /// <summary>脚本</summary>
    public class Script
    {
        private static ScriptConfig _Config;
        /// <summary>脚本配置</summary>
        public static ScriptConfig Config { get { return _Config; } set { _Config = value; } }

        public static Boolean ProcessFile(String file, ScriptConfig config)
        {
            Config = config;

            var code = Helper.ReadCode(file);

            // 分析要导入的第三方程序集。默认包含XScript所在目录的所有程序集
            code = "//Assembly=" + AppDomain.CurrentDomain.BaseDirectory + Environment.NewLine + code;
            // 以及源代码所在目录的所有程序集
            code = "//Assembly=" + Path.GetDirectoryName(file) + Environment.NewLine + code;
            var rs = Helper.ParseAssembly(code);
            rs = Helper.ExpendAssembly(rs);

            Environment.CurrentDirectory = Path.GetDirectoryName(file);

            var session = ScriptEngine.Create(code, false);

            // 加入代码中标明的程序集
            if (rs.Length > 0) session.ReferencedAssemblies.AddRange(rs);
            // 加入参数中标明的程序集
            if (!String.IsNullOrEmpty(config.Assembly))
            {
                rs = config.Assembly.Split(';');
                rs = Helper.ExpendAssembly(rs);
                if (rs.Length > 0) session.ReferencedAssemblies.AddRange(rs);
            }

            // 调试状态下输出最终代码
            if (config.Debug)
            {
                session.GenerateCode();
                var codefile = Path.ChangeExtension(file, "code.cs");
                File.WriteAllText(codefile, session.FinalCode);
            }

            // 使用VisualStudio打开源码文件进行编辑
            if (config.Vs)
            {
                File.WriteAllText(file, session.FinalCode);
                return OpenWithVs(file, rs);
            }

            // 生成Exe
            if (config.Exe)
                MakeExe(session, file);
            else
                Run(session);

            return false;
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
        /// <param name="codefile"></param>
        /// <param name="rs"></param>
        static Boolean OpenWithVs(String codefile, String[] rs)
        {
            // 判断项目文件是否存在，若不存在，则根据源码文件生成项目
            var asm = Assembly.GetExecutingAssembly();
            var name = Path.GetFileNameWithoutExtension(codefile) + ".csproj";
            var dir = DataHelper.Hash(codefile.ToLower());
            var proj = Path.GetDirectoryName(asm.Location).CombinePath("Projs", dir, name);
            //if (!File.Exists(proj))
            MakeProj(codefile, proj, rs);

            // 找到安装VisualStudio地址，暂时还不支持Express
            var root = Registry.ClassesRoot;
            var vs = "";
            for (int i = 11; i >= 8; i--)
            {
                var reg = root.OpenSubKey(String.Format("VisualStudio.sln.{0}.0", i));
                if (reg != null)
                {
                    reg = reg.OpenSubKey("shell\\Open\\Command");
                    if (reg != null) vs = reg.GetValue("") + "";
                    if (vs.IsNullOrWhiteSpace()) break;
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

        static void MakeProj(String codefile, String proj, String[] rs)
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
            node = items.SelectSingleNode("Compile");
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
            att.Value = codefile;

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
                File.WriteAllText(sln, txt);
            }
        }

        static void Run(ScriptEngine session)
        {
            // 预编译
            session.Compile();

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
            if (String.IsNullOrEmpty(name)) return null;

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

            return null;
        }
    }
}