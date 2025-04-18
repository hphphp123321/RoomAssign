﻿# 抢房好好好

<center>
![house.ico](./house.ico)
</center>

抢房好好好是一款基于 C# WPF 构建的自动化选房工具，主要提供两种选房模式：
- **模拟点击模式**：利用 Selenium WebDriver 控制浏览器自动登录、选房并完成确认。
- **HTTP 发包模式**：通过 HttpClient 发起 HTTP 请求，自动匹配并选择房源。

该软件支持灵活的房间筛选条件，尤其是在楼层过滤上支持**范围输入**（例如 “3,4,6,9-11”），当输入为空或为默认值（0、null 等）时则不进行该条件的筛选。

---

## 目录

- [安装要求](#安装要求)
- [使用步骤](#使用步骤)
- [启动抢房流程详解](#启动抢房流程详解)
- [筛选原理](#筛选原理)
- [注意事项](#注意事项)

---

## 安装要求

- **安装环境**：Windows 10 或更高版本
- **下载地址**：https://github.com/hphphp123321/RoomAssign/releases

---

## 使用步骤

1. **填入相关参数**

   在主界面上，请按照下列提示填写各项参数：

    - **浏览器与运行模式**
        - **浏览器类型**：选择使用“Chrome”或“Edge”驱动（通过下拉框选择）。
        - **运行模式**：选择“模拟点击”模式（使用浏览器自动操作）或“HTTP 发包”模式（直接发起 HTTP 请求）。

    - **登录参数**
        - **用户账号**：用于登录网站的账号。
        - **用户密码**：用于登录网站的密码。
        - **Cookie**：可选的登录凭证，若填写 Cookie，将优先使用 Cookie 登录；否则系统将提示你完成账号密码登录，并需要手动拖拽验证码。

    - **申请人信息**
        - **申请人名称**：对应网站中申请人列表的名称，此项必填，用于匹配对应的申请人ID。

    - **时间设置**
        - **选房开始时间**：通过选择日期并填写小时、分钟和秒来设定开始抢房的具体时刻。
        - **点击间隔**：每次点击或发包之间的间隔时间（单位毫秒），建议默认值为 200 毫秒，可按实际网络与页面响应情况调整。

    - **房源条件设置**（在房源条件列表中填写）
        - **社区名称**：待选房社区的名称。
        - **建筑号**：填写具体建筑号；为 0 时表示不过滤此条件。
        - **楼层范围**：填写楼层筛选条件，支持单个楼层或多个楼层及范围（例如 “3,4,6,9-11”）；为空或默认值时表示不过滤此条件。
        - **价格上限**：所能接受的最高房价；填写 0 则表示不过滤此条件。
        - **最小面积**：需要的房屋的最低面积；填写 0 则表示不过滤此条件。
        - **房型**：选择房屋类型，如“一居室”、“二居室”、“三居室”等。

    - **自动确认**
        - **自动确认选房**：勾选后，当房源选定后系统会自动点击“最终确认”按钮，否则需要用户在界面上手动确认。

2. **启动抢房流程**

   点击“开始抢房”按钮后，系统将执行以下操作：

    - **参数采集与校验**  
      程序首先会读取所有界面上的输入参数，包括浏览器类型、运行模式、登录信息、申请人名称、选房开始时间、点击间隔及所有房源条件。在采集参数时，程序会进行简单校验（例如确保申请人名称、选房时间和房源条件不为空），确保各项参数满足抢房操作的要求。

    - **任务启动与异步执行**  
      在确认参数有效后，程序启动一个后台任务（使用异步 Task 运行），避免阻塞 UI 线程。用户可以在后台实时监控通过日志文本框输出的消息。

    - **模式选择与执行**  
      根据用户选定的“运行模式”，程序执行两种不同的抢房逻辑：

        - **模拟点击模式**
            1. 根据选定的浏览器类型调用相应的驱动（Chrome 或 Edge）。
            2. 使用提供的 Cookie（或账号密码）进行登录；若 Cookie 登录失败则要求用户手动完成验证码操作。
            3. 登录成功后，程序导航至选房页面，并使用 Selenium 控制浏览器自动搜索并筛选房源。
            4. 采用内置的辅助筛选逻辑，遍历页面上符合条件的房源记录（包括通过范围解析的楼层条件），依次选择完全匹配或备用的选项。
            5. 完成房源选定后，系统调用确认操作，如启用自动确认则会自动点击“最终确认”按钮，否则提示用户手动确认。

        - **HTTP 发包模式**
            1. 程序利用 HttpClient 发送 GET 请求获取页面数据，通过正则表达式解析对应的申请人ID。
            2. 等待预设选房开始时间到达后，发送 POST 请求匹配房源信息，通过辅助筛选逻辑选出符合条件的房间ID。
            3. 在一个限定时间窗口内，不断发包请求选房，直至匹配成功或时间窗口结束。

    - **日志与状态监控**  
      整个抢房流程中，系统会不断输出当前时间、剩余时间以及各关键步骤的执行情况（如登录成功、房源匹配、页面导航、选房确认等），方便用户跟踪抢房进程和诊断问题。

    - **异常处理与手动停止**  
      如果在抢房过程中遇到异常或网络延时等问题，系统会通过日志提示错误信息。用户也可以随时点击“停止抢房”按钮来取消当前任务，并在模拟点击模式下关闭浏览器窗口。

3. **停止抢房**  
   如需终止抢房流程，点击“停止抢房”按钮，软件会取消当前后台任务，并调用相应的清理或退出方法（例如在模拟点击模式下关闭浏览器），确保抢房操作安全中断。

---

## 启动抢房流程详解

启动抢房流程涉及以下详细步骤：

1. **启动前准备**
    - 所有用户输入的参数会被读取并进行简单校验，确保必填项（如申请人名称、选房时间、社区条件）有效。
    - 控制台的日志输出已被重定向到界面内的文本框，用户可即时看到每个步骤的反馈信息。

2. **异步任务执行**
    - 程序采用 `Task.Run` 异步执行抢房流程，防止 UI 阻塞，同时保证界面响应及时。
    - 异步任务内部会首先判断各参数的有效性，然后根据选定的运行模式选择后续流程。

3. **模式执行分支**
    - **模拟点击模式**：
        - 调用 `GetDriver` 方法创建对应浏览器驱动。
        - 使用 Cookie 或账号密码登录，并导航到选房首页。
        - 调用 `DriverSelector.RunAsync` 方法，等待达到设定抢房时刻，并启动轮询搜索房源。
        - 在搜索房源过程中，利用辅助函数根据社区名称、建筑号、楼层范围、价格和面积等条件筛选出最适合的房源，优先选择完全匹配的记录。
        - 当匹配到房源后，自动点击选房并调用确认操作，若启用自动确认则自动完成最终确认。

    - **HTTP 发包模式**：
        - 通过 HTTP GET 请求获取页面信息，利用正则表达式解析出申请人的ID。
        - 等待抢房开始时刻后，发送 HTTP POST 请求匹配房源信息，并解析获得房间ID。
        - 在一个预设的时间窗口内，不断发包尝试选定房间，直到成功或时间到期。

4. **过程监控与反馈**
    - 在整个流程中，系统每秒输出当前时间和距离选房开始的剩余时间，并在关键操作前后记录详细日志，方便用户实时监控进展。
    - 若遇到异常（如网络延迟、页面结构变化、登录失败等），会在日志中详细记录错误描述，协助用户排查问题。

5. **流程终止与后续确认**
    - 用户可以通过点击“停止抢房”按钮，随时中断抢房流程。程序会取消所有待处理任务，并关闭 Selenium 浏览器（如处于模拟点击模式）。
    - 若选房成功但未启用自动确认，后续需要用户手动在界面上点击确认按钮，以完成整个抢房操作。

---

## 筛选原理

为保证房源筛选灵活且便于维护，项目中采用了以下设计方案：

- **默认不过滤**
    - **数值类型（如建筑号、价格、面积等）**：当值为 `0` 时，视为不过滤该条件。
    - **楼层过滤**：楼层条件由字符串表示，如果字符串为空或 null，也不进行筛选。

- **范围解析**  
  对于楼层，用户可以输入诸如 `3,4,6,9-11` 的格式，程序内部通过辅助方法 `ParseFloorRange` 分析输入内容，解析后得到一个具体的楼层集合（例如 [3, 4, 6, 9, 10, 11]）。

- **辅助过滤函数**  
  在 `DriverSelector` 中引入了若干辅助方法，如 `FilterEqual`、`FilterPrice`、`FilterArea`、`FilterFloor` 等。调用这些方法后：
    - 当条件为默认值（例如 0 或空）时直接返回 `true`，不参与比较；
    - 否则根据实际条件进行匹配判断。

- **筛选逻辑实现**  
  在 `SelectBestHouse` 方法中，遍历页面中所有房源记录，对每一条记录按以下条件进行验证：
    - 社区名称、房型必须严格匹配；
    - 建筑号、价格、面积及楼层使用辅助过滤函数验证。如果所有条件均满足，则认为是“完全匹配”房源；否则按优先级选择备用房源（例如仅根据楼层和社区匹配）。
    - 通过这种方式使得代码逻辑分离，各个条件独立判断，易于扩展和维护。

---

## 注意事项

- **登录方式**  
  优先使用 Cookie 登录，如果无法成功登录，程序将尝试使用账号密码方式登录，并要求用户手动完成拖拽验证码操作。

- **时间调度**  
  抢房操作会在预设的开始时间临近时启动，期间会不断输出时间和剩余时间信息。建议确保系统时间准确，网络环境稳定。

- **异常处理**  
  软件在各个关键点（如获取申请人ID、解析房源信息、执行自动点击或 HTTP 发包）均有异常捕获并输出日志，请关注日志中的提示信息以便发现问题。

- **界面与数据绑定**  
  若修改筛选条件（例如楼层从 int 改为 string），请确保在 UI 数据绑定时同步更新相应控件类型，默认值设置为 "0" 或空字符串表示不过滤。

- **环境适配**  
  模拟点击模式依赖于浏览器及其驱动，确保对应版本的 Chrome/Edge 驱动已正确安装且与浏览器版本匹配。