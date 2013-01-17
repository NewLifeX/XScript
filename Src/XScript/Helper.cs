using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NewLife.Exceptions;

namespace NewLife.XScript
{
    /// <summary>工具类</summary>
    static class Helper
    {
        /// <summary>读取源代码，同时嵌入被引用的代码文件</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static String ReadCode(String file)
        {
            // 防止递归包含
            var fs = new Stack<String>();
            fs.Push(file);
            return ReadCode(file, fs);
        }

        static String ReadCode(String file, Stack<String> fs)
        {
            var ss = File.ReadAllLines(file);
            var dir = Path.GetDirectoryName(file);

            var sb = new StringBuilder(10240);
            // 源码行
            sb.AppendFormat("#line {0} \"{1}\"\r\n", 1, file);

            for (int i = 0; i < ss.Length; i++)
            {
                var item = ss[i];
                sb.AppendLine(item);

                // 包含源码指令
                if (item.StartsWith("//Include=", StringComparison.OrdinalIgnoreCase))
                {
                    var f = item.Substring("//Include=".Length);
                    f = Path.Combine(dir, f);

                    var f2 = f.ToLower();
                    if (fs.Contains(f2)) throw new XException("{0}中递归包含{1}！", file, f);
                    fs.Push(f2);
                    sb.Append(ReadCode(f, fs));
                    fs.Pop();

                    // 恢复原来的代码行号
                    sb.AppendFormat("#line {0} \"{1}\"\r\n", i + 1, file);
                }
            }

            return sb.ToString();
        }

        /// <summary>分析代码中导入的第三方程序集</summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static String[] ParseAssembly(String code)
        {
            var list = new List<String>();

            var ss = code.Split(new String[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (var item in ss)
            {
                if (item.StartsWith("//Assembly=", StringComparison.OrdinalIgnoreCase))
                {
                    var name = item.Substring("//Assembly=".Length).Trim();
                    list.Add(name);
                }
            }

            return list.ToArray();
        }

        /// <summary>扩展引用程序集，拆分目录</summary>
        /// <param name="afs"></param>
        /// <returns></returns>
        public static String[] ExpendAssembly(String[] afs)
        {
            var list = new List<String>();

            foreach (var item in afs)
            {
                if (item.IsNullOrWhiteSpace()) continue;

                // 有可能是目录，目录要遍历文件
                if (item.EndsWith("/") || item.EndsWith("\\") || !Path.GetFileName(item).Contains("."))
                {
                    var fs = Directory.GetFiles(item, "*.dll", SearchOption.AllDirectories);
                    if (fs.Length > 0)
                    {
                        foreach (var elm in fs)
                        {
                            list.Add(elm);
                        }
                    }
                }
                else
                    list.Add(item);
            }

            return list.ToArray();
        }
    }
}