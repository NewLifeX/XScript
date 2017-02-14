using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// 有关程序集的一般信息由以下
// 控制。更改这些特性值可修改
// 与程序集关联的信息。
[assembly: AssemblyProduct("NewLife.Build")]
[assembly: AssemblyTitle("新生命编译引擎")]
[assembly: AssemblyDescription("编译链接嵌入式C/C++代码")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("新生命开发团队")]
[assembly: AssemblyCopyright("©2002-2016 新生命开发团队 http://www.NewLifeX.com")]
[assembly: AssemblyTrademark("四叶草")]
[assembly: AssemblyCulture("")]

//将 ComVisible 设置为 false 将使此程序集中的类型
//对 COM 组件不可见。  如果需要从 COM 访问此程序集中的类型，
//请将此类型的 ComVisible 特性设置为 true。
[assembly: ComVisible(false)]

// 如果此项目向 COM 公开，则下列 GUID 用于类型库的 ID
[assembly: Guid("98e8e88c-fcf1-46aa-9388-ff3bf4422b20")]

// 程序集的版本信息由下列四个值组成: 
//
//      主版本
//      次版本
//      生成号
//      修订号
//
//可以指定所有这些值，也可以使用“生成号”和“修订号”的默认值，
// 方法是按如下所示使用“*”: :
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.2.*")]
[assembly: AssemblyFileVersion("1.2.2017.0214")]

/*
 * v1.2.2017.0214   增加JLink封装，支持烧写固件
 * 
 * v1.1.2016.1114   MDK编译时检查依赖关系，头文件被修改时也能确保重新编译
 * 
 * v1.0.2016.1031   创建
 */
