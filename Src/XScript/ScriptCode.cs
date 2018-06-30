using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NewLife.Log;

namespace NewLife.XScript
{
    /// <summary>脚本代码</summary>
    class ScriptCode
    {
        #region 属性
        /// <summary>代码文件</summary>
        public String CodeFile { get; set; }

        /// <summary>引用程序集</summary>
        public ICollection<String> Refs { get; } = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region 构造
        public ScriptCode(String file)
        {
            CodeFile = file;

            // 分析要导入的第三方程序集。默认包含XScript所在目录的所有程序集
            var rf = AppDomain.CurrentDomain.BaseDirectory.EnsureEnd("\\");
            if (!Refs.Contains(rf)) Refs.Add(rf);
            //// 以及源代码所在目录的所有程序集
            //rf = Path.GetDirectoryName(file).EnsureEnd("\\");
            //if (!Refs.Contains(rf)) Refs.Add(rf);
        }
        #endregion

        #region 读取源码
        /// <summary>读取源代码，同时嵌入被引用的代码文件</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public String ReadCode(Boolean includeLine = true)
        {
            // 防止递归包含
            var fs = new Stack<String>();
            fs.Push(CodeFile);
            return ReadCode(CodeFile, fs, Refs, includeLine).Trim();
        }

        static String ReadCode(String file, Stack<String> fs, ICollection<String> rfs, Boolean includeLine)
        {
            var ss = File.ReadAllLines(file);
            var dir = Path.GetDirectoryName(file);

            var sb = new StringBuilder(ss.Length * 50);
            // 源码行
            if (includeLine) sb.AppendFormat("#line {0} \"{1}\"\r\n", 1, file);

            for (var i = 0; i < ss.Length; i++)
            {
                var line = ss[i];

                // 空行跳过
                if (String.IsNullOrEmpty(line))
                {
                    sb.AppendLine(line);
                    continue;
                }

                line = line.Trim();

                // 包含源码指令
                if (line.StartsWithIgnoreCase("//Include="))
                {
                    if (includeLine) sb.AppendLine(ss[i]);

                    var inc = line.Substring("//Include=".Length).Trim('\"');
                    inc = Path.Combine(dir, inc);

                    var inc2 = inc.ToLower();
                    if (fs.Contains(inc2)) throw new XException("{0}中递归包含{1}！", file, inc);
                    fs.Push(inc2);
                    sb.Append(ReadCode(inc, fs, rfs, includeLine));
                    fs.Pop();

                    // 恢复原来的代码行号
                    if (includeLine) sb.AppendFormat("#line {0} \"{1}\"\r\n", i + 1, file);
                }
                // 程序集引用指令
                else if (line.StartsWithIgnoreCase("//Assembly="))
                {
                    if (includeLine) sb.AppendLine(ss[i]);

                    var asm = line.Substring("//Assembly=".Length).Trim('\"');
                    if ((asm.Contains("/") || asm.Contains("\\")) && !Path.IsPathRooted(asm)) asm = Path.Combine(dir, asm).GetFullPath();

                    if (!rfs.Contains(asm)) rfs.Add(asm);
                }
                else
                {
                    sb.AppendLine(ss[i]);
                }
            }

            return sb.ToString();
        }
        #endregion

        #region 引用程序集
        /// <summary>获取引用DLL数组</summary>
        /// <returns></returns>
        public String[] GetRefArray()
        {
            var ss = new String[Refs.Count];
            Refs.CopyTo(ss, 0);

            return ExpendAssembly(ss);
        }

        /// <summary>获取引用目录字符串</summary>
        /// <returns></returns>
        public String GetRefStr()
        {
            var sb = new StringBuilder();
            foreach (var item in Refs)
            {
                if (item.EqualIgnoreCase(AppDomain.CurrentDomain.BaseDirectory.EnsureEnd("\\"))) continue;
                if (item.EqualIgnoreCase(Path.GetDirectoryName(CodeFile).EnsureEnd("\\"))) continue;

                if (sb.Length > 0) sb.AppendLine();
                sb.AppendFormat("//Assembly={0}", item);
            }

            return sb.ToString();
        }

        /// <summary>扩展引用程序集，拆分目录</summary>
        /// <param name="afs"></param>
        /// <returns></returns>
        static String[] ExpendAssembly(String[] afs)
        {
            var list = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in afs)
            {
                if (item.IsNullOrWhiteSpace()) continue;
                if (list.Contains(item)) continue;

                var fi = item.GetFullPath();
                if (File.Exists(fi))
                {
                    //list.Add(fi);
                    var asm = Assembly.LoadFile(fi);
                    list.Add(asm.Location);
                }
                else if (item.EndsWithIgnoreCase(".dll"))
                {
                    // 尝试从GAC加载
                    var file = Path.GetDirectoryName(typeof(Object).Assembly.CodeBase).CombinePath(item);
                    if (File.Exists(file)) list.Add(file);
                }
                // 有可能是目录，目录要遍历文件
                else if (item.EndsWith("/") || item.EndsWith("\\") || Directory.Exists(item))
                {
                    var fs = Directory.GetFiles(item, "*.dll", SearchOption.TopDirectoryOnly);
                    if (fs.Length > 0)
                    {
                        foreach (var elm in fs)
                        {
                            if (!list.Contains(elm)) list.Add(elm);
                        }
                    }
                }
                // 加载系统程序集
                else
                {
                    //list.Add(item);
                    try
                    {
#pragma warning disable CS0618 // 类型或成员已过时
                        var asm = Assembly.LoadWithPartialName(item);
#pragma warning restore CS0618 // 类型或成员已过时
                        //Console.WriteLine(asm);
                        list.Add(asm.Location);
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteException(ex);
                    }
                }
            }

            var arr = new String[list.Count];
            list.CopyTo(arr, 0);
            return arr;
        }
        #endregion
    }
}