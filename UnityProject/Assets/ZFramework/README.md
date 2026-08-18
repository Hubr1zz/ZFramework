# ZFramework
<p align="center">
    <img src="Books/src/ZFramework512.png" alt="logo" width="384" height="384">
</p>

<h3 align="center"><strong>ZFramework<strong></h3>

<p align="center">
  <strong>Unity框架解决方案<strong>
    <br>
  <a style="text-decoration:none">
    <img src="https://img.shields.io/badge/Unity%20Ver-2021.3.20++-blue.svg?style=flat-square" alt="status" />
  </a>
  <a style="text-decoration:none">
    <img src="https://img.shields.io/github/license/ALEXTANGXIAO/ZFramework" alt="license" />
  </a>
  <a style="text-decoration:none">
    <img src="https://img.shields.io/github/last-commit/ALEXTANGXIAO/ZFramework" alt="last" />
  </a>
  <a style="text-decoration:none">
    <img src="https://img.shields.io/github/issues/ALEXTANGXIAO/ZFramework" alt="issue" />
  </a>
  <a style="text-decoration:none">
    <img src="https://img.shields.io/github/languages/top/ALEXTANGXIAO/ZFramework" alt="topLanguage" />
  </a>
</p>


# <strong>ZFramework

#### ZFramework 是基于开源 Unity 框架 [TEngine](https://github.com/Alex-Rachel/TEngine) 扩展的个人架构。项目使用独立的 ZFramework 品牌、命名空间与程序集名称，同时保留 TEngine 的原始版权和 MIT 许可证。ZFramework 是一个简单（新手友好、开箱即用）且强大的 Unity 框架全平台解决方案。

## 文档快速预览 - 5分钟
* [全平台跑通示意](Books/99-各平台运行RunAble.md): 全平台跑通示意。
* [01_介绍](Books/0-介绍.md): 简单介绍。
* [02_框架概览](Books/2-框架概览.md): 展示框架概览。
* [03_资源模块](Books/3-1-资源模块.md): 展示资源模块概览。
* [04_事件模块](Books/3-2-事件模块.md): 展示事件模块概览。
* [05_内存池模块](Books/3-3-%E5%86%85%E5%AD%98%E6%B1%A0%E6%A8%A1%E5%9D%97.md): 展示内存池模块概览。
* [06_对象池模块](Books/3-4-%E5%AF%B9%E8%B1%A1%E6%B1%A0%E6%A8%A1%E5%9D%97.md): 展示对象池模块概览。
* [07_配置表模块](Books/3-6-%E9%85%8D%E7%BD%AE%E8%A1%A8%E6%A8%A1%E5%9D%97.md): 展示配置表模块概览。
* [08_流程模块](Books/3-7-%E6%B5%81%E7%A8%8B%E6%A8%A1%E5%9D%97.md): 展示商业化流程模块。
* [09_UI模块](Books/3-5-UI模块.md): 展示商业化UI模块。


## <strong>为什么要使用ZFramework
0. 开箱即用5分钟即可上手整套开发流程，代码整洁，思路清晰，功能强大。高内聚低耦合。您可以很轻易的把您不需要的模块进行移除替换。
1. 使用 Luban 配置表（支持懒加载、异步加载、同步加载配置）和 YooAsset 资源框架，并为 DLC/Mod 独立内容包保留扩展点。
2. 提供商业化 UI 开发流程和资源管理，设计并实现了 YooAsset 资源自动释放，支持 LRU、ARC 管理资源内存。
3. 支持全平台，已有项目使用ZFramework上架Steam、Wechat-minigame、AppStore。

## <strong>最新的Demo飞机大战位于demo分支

## <strong>服务器相关
ZFramework本身为纯净的客户端。不强绑定任何服务器。但是个人开发以及中小型公司开发双端则推荐C#服务器。

Net Core现在已经更新到了8.0的版本，在性能和设计上其实是远超JAVA和GO。在JAVAER还在为JVM更新和添加更多功能时，其实他们已经被国内大环境所包围了，看不到.Net Core的性能之强，组件化的结构。国内大环境是JAVA和GO的天下这个不可否认，但是国外C#也确实很多。其实.Net Core最大的问题是大多数自己人都不知道他的优点(AOT、JIT混合编译、热重载等等)，甚至很多守旧派抵制core。GO喜欢吹性能，但其实目前来看，除了协程的轻量级，大多数性能测试其实不如JAVA和.Net。简单可以说出了C++的性能以外，Net Core其实都打得过。

需要服务器可以合并<a href="https://github.com/ALEXTANGXIAO/GameNetty"><strong>GameNetty</strong></a>过来，或者分支Fantasy为接好的带有Fantasy服务器的双端分支。

## <strong>项目结构概览
```
Assets
├── AssetRaw            // YooAsset资源目录
├── Atlas               // 自动生成图集目录
├── ZFramework             // 框架核心目录
└── GameScripts         // 程序集目录
    ├── Editor          // 编辑器程序集
    ├── Main            // 主程序程序集(启动器与流程)
    └── HotFix          // 兼容目录名（普通Player程序集）
        ├── GameBase    // 游戏基础框架程序集 [Dll]
        ├── GameProto   // 游戏配置协议程序集 [Dll]  
        ├── BattleCore  // 游戏核心战斗程序集 [Dll] 
        └── GameLogic   // 游戏业务逻辑程序集 [Dll]
            └── GameApp.cs                  // 游戏逻辑主入口


ZFramework
├── Editor              // ZFramework编辑器核心代码
└── Runtime             // ZFramework运行时核心代码
```

 - 必要：项目使用了以下第三方插件，请自行购买导入：
   - /Unity/Assets/Plugins/Sirenix

---
## <strong>优质开源项目推荐

#### <a href="https://github.com/tuyoogame/YooAsset"><strong>YooAsset</strong></a> - YooAsset是一套商业级经历百万DAU游戏验证的资源管理系统。

#### <a href="https://github.com/JasonXuDeveloper/JEngine"><strong>JEngine</strong></a> - 使Unity开发的游戏支持热更新的解决方案。

#### <a href="https://github.com/qq362946/Fantasy"><strong>Fantasy</strong></a> - Fantasy是一套源于ETServer但极为简洁，更好上手的一套商业级服务器框架。

#### <a href="https://github.com/ALEXTANGXIAO/GameNetty"><strong>GameNetty</strong></a> - GameNetty是一套源于ETServer，首次拆分最新的ET8.1的前后端解决方案（包），客户端最精简大约750k，完美做成包的形式，几乎零成本 无侵入的嵌入进你的框架。

## <strong>Buy me a 奶茶.

[您的赞助会让我们做得更快更好，如果觉得ZFramework对您有帮助，不妨请我可爱的女儿买杯奶茶吧~](Books/Donate.md)
