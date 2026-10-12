# 开发记录

## 2026-10-13 · 建立独立公开 GitHub 源码仓库

- 授权：用户要求上传GitHub，检查并说明创建公开 `baiye-gift/BaiyeCryogenicPantry`、启用Issues、整理文件、验证及提交推送的范围，用户回复“上传”确认。本项目此前未建立独立Git仓库，位于工作区未跟踪目录。
- 实际修改 `.gitignore`、`README.md`、本日志，新增项目根 `preview.png`（已确认第二版工坊图）；初始化项目自己的Git元数据。源码、SCML、KAnim、版本0.1.3及游戏运行代码不变。保留已有AGENTS.md作为独立开发入口；排除bin/obj/dist、安装备份、本机审计、编辑器缓存和环境文件；premium-ui.json保留为现有UI约定。
- README补入个人开发测试目的、存档提醒、源码/Issues地址、工坊图与可指定GameManaged的独立构建步骤。提交前检查：Release编译0错误/2个既有MSB3277程序集引用警告，91项核心规则、64项原生托管/静态契约、3515项源/原生动画与运动断言通过。暂存62文件，git diff --cached --check通过，游戏DLL/存档/日志/环境文件未暂存，令牌/私钥模式扫描无匹配；bin/obj/dist/premium-audit.json排除检查通过。
- 发布：公开仓库 https://github.com/baiye-gift/BaiyeCryogenicPantry 已创建，Issues启用，默认分支main。首个源码提交 `ae27e3671779c97d4a564e3cced16d321b6653e5` 已推送；GitHub API确认远端提交与本地相同、62个文件列表完全匹配、封面189790字节。独立检出仅使用62个提交文件，构建成功，64项原生托管检查与3515项素材断言再次通过，不依赖父工作区源码或未提交文件。本节最终状态随后随文档提交同步至远端，详细本机检查留在忽略目录 `dist/verification/github-20261013/`。
- 本次不改变0.1.3运行逻辑，未安装或操作游戏。命令行检查不启动Unity/SimDLL，真实供电/管路调度、饮用、食品腐败、旧存档与DLC/多mod完整验收尚未完成；上传为个人开发测试源码，不额外发布二进制Release。

## 2026-10-06 · 0.1.3 默认管路条件误停机与具体诊断修复，本地升级

- 授权：继续用户已确认的气体、运行停机和缺字修复范围。用户实机确认自动化已启用但保鲜柜仍停机；展示该范围内的默认输入组件修正、真实阻塞条件说明和验证方案后继续处理。用户另明确回复已保存并退出游戏，安装前再次核对进程已退出。
- 实际文件：修改 `src/Game/{PantryConfig,PantryBuilding,PantryMod}.cs`、`src/UI/PantrySideScreen.cs`、`tests/Native/Program.cs`、`mod_info.yaml`、`README.md`、`UX-CONTRACT.md`、`docs/playtest.md`、`premium-audit.json` 和本日志。生成0.1.3 ZIP/校验文件、分发暂存目录、0.1.2安装备份及 `dist/verification/installation-0.1.3.json`。未修改其他模组、核心制冷参数、存档字段或美术资源。
- 根因证据：截图明确显示“由自动化电路启用”和红色“空管道”；已安装DLL与当前0.1.2构建哈希一致，排除旧DLL未更新。原生BuildingLoader在自定义ConfigureBuildingTemplate之后，按InputConduitType=Gas自动添加ConduitConsumer并配置RequireInputs要求管路。RequireInputs会以`pipesHaveMass=false`阻止Operational；默认消费者还有无限吸取速率，并默认绑定Storage。此前只修改自定义取气与停机文案，漏掉这套默认组件，造成空管/断管也停止制冷，且默认消费者可能抢在自定义过滤/付款流程外取气。
- 修正：在DoPostConfigureComplete中，RequireInputs只保留电力要求、取消管路/管内质量条件及整机管路错误展示；默认ConduitConsumer的isOn/isConsuming关闭、速率置零、满足状态置真，保留组件与原生端口/网络。PantryBuilding.OnSpawn再次应用相同配置，覆盖既有建筑加载；自定义已付费、元素及温度检查的Intake继续唯一实际取气。空/断气管只影响补气，不能停止制冷；罐内有效气氛保留，空罐仍可制冷但缺少无菌保鲜条件，缺水仍只影响加工。
- 诊断：读取实际Operational.Flags的false项，分别说明手动禁用、淹没、掩埋、损坏、管路及其他阻塞原因；手动禁用优先于缺电提示。未知第三方条件显示其标识，避免只有泛化“检查各种状态”。仅在阻塞标志集合变化时写一条本柜实例诊断日志，保留所有真实安全/电力/自动化条件，不强行将Operational设为可运行。
- 余留缺字：新Player.log明确报告Description的U+2082下标2缺字，截图说明里CO2也有方框。将本柜面板与建筑说明中的CO₂改为ASCII `CO2`，补充针对性回归检查；不更换字体或修改共享主题。
- 回归过程：抽出完整prefab后处理实际使用的ConfigureInputs辅助方法，先保留旧默认“电力+管路”行为；用原生RequireInputs/ConduitConsumer托管对象执行，空管不能停制冷的期望检查预期失败。修正后通过，并覆盖重复应用配置、原生IsSatisfied、无默认取气、仅电力状态展示、实际false标志说明及不误报true标志。U+2082检查也先预期失败，再替换文案通过。
- 验证：Release编译0错误，保留两项既有MSB3277依赖版本警告。核心规则91项通过；原生托管/静态契约64项通过，包括新增默认输入配置、重应用、SkipSaveFileSerialization合同、具体阻塞说明和缺字检查，原有字段序列化与温度/气体规则仍通过。上述原生对象检查没有创建GameObject、执行完整BuildingLoader、运行RequireInputs.Sim200ms或启动真实电路/管路，不等于实机运行验收。
- 静态/素材/包：strict面板审计0 findings，10个改动文件尾随空白检查通过；现有KAnim结构/运动3515项通过。包内12个文件、唯一自有DLL、ZIP路径和全部文件SHA256核对通过，日志更新后重新打包并纳入本记录。
- 安装：完整备份已安装0.1.2的12个文件至 `dist/install-backups/BaiyeCryogenicPantry-0.1.2-20261006-223511/`，逐文件确认备份哈希后，将 `D:/Documents/Klei/OxygenNotIncluded/mods/local/BaiyeCryogenicPantry/` 更新为0.1.3并核对12个安装文件。mod.yaml身份与六个动画文件保持；不改mods.json、不移动六仓库存。最终日志同步后重新核对最终分发内容，安装报告留在上述验证JSON。未提交、推送或发布。
- 未验证：修正后未启动Unity/游戏/SimDLL；原存档真实加载后的自动恢复、空/断管持续制冷、默认消费者不抢吸、当前或历史错仓气体、全部安全条件及第三方运行标志、实际付款/动画/腐败/缺字效果仍需 `docs/playtest.md`。不以托管配置检查代替完整prefab/实机验收，不擅自删除或迁移旧库存。

## 2026-10-06 · 0.1.2 保鲜气体、制冷速度和原生交互修复，本地升级

- 授权：用户实机反馈切气出现气态铜、降温过慢、操作别扭和三角缺字；检查截图、游戏元素表、原生组件及 Player.log 后提出气相/输入温度限制、加速制冷、真实食品温度、明确气体选择/取消、原生横排模板与图形气量条，以及验证和备份升级方案，用户回复“可以”。
- 实际源文件：修改 `src/Core/PantryRules.cs`、`src/Game/{PantryBuilding,PantryMod,PantryStatus}.cs`、`src/UI/PantrySideScreen.cs`、`tests/Regression/Program.cs`、`tests/Native/Program.cs`、`mod_info.yaml`、`README.md`、`DESIGN.md`、`UX-CONTRACT.md`、`docs/playtest.md`、`premium-audit.json` 和本日志。生成 `dist/BaiyeCryogenicPantry-0.1.2.zip`、`.sha256`、分发暂存目录、安装备份及 `dist/verification/installation-0.1.2.json`。未修改其他模组或美术源文件。
- 根因证据：气体候选只检查原生无菌映射与元素气相标志，导致原生标记无菌的气态铜也入选；游戏 `elements/gas.yaml` 中 CopperGas 低温相变线为2834 K，Steam为372.5 K，均不适合−22°C储藏。原生 ButtonMenuSideScreen 对横排使用独立 horizontalButtonPrefab，原实现页签/增减行误用竖排模板。截图显示自动化信号停机，同时将目标气体当成当前气体展示；Player.log 确认 U+25B0 无字形而被替换成方框。
- 气体规则：支持原生无菌映射且气相区间覆盖−22°C至50°C的元素，不写死 ID 白名单；CO₂/氯气/氢气优先展示，仍允许符合条件的第三方气体。真正取走管内质量之前检查元素、目标、实际温度≤50°C及气相区间；错误/过热/过冷失去气相的包留在管内。显示实际罐内气体（混合时注明）、目标气体、供气/拒收原因。无效存档目标恢复有效罐内气体，否则恢复CO₂；不删除库存，旧无效气体不能提供无菌效果。
- 换气交互：明确气体选择列表替代循环切换；置换中也能修改目标。取消恢复罐内仍存在的有效旧气体目标，即使新气体已占多数；只改目标，不恢复已经消耗的质量，后续仍按实际来气收据置换。不存在有效旧气体时禁用取消但仍允许选择其他目标。自动补气关闭时选气不启动置换。
- 制冷与保鲜：主动制冷24→240 kDTU/s、保温制冷9.6→96 kDTU/s、换热响应0.15→0.75/s；保持120/240/600 W及加工额外120 W的实际电路付款、部分付款比例、真实物品浮点温度结算、双精度柜内热量及对外排热。食品冷藏/深冻与原生腐败分类改为使用食品自身温度，不再取柜内温度与食品温度的较低值；页面和状态增加最暖易腐食品温度。具体停机说明涵盖红色自动化信号、无电线、供电不足、建筑不可运行和等待实际付款。
- 原生 UI：页签及“增加/减少”行克隆 horizontalButtonPrefab，消除竖排模板首选宽度，使用独立高优先级等宽布局；配方按钮13 px，其他文字沿用已有样式。移除U+25BE/U+25B8文字三角及U+25B0/U+25B1块状条，使用“展开/收起”及6 px Image气量条；气量矩形宽度由真实质量驱动。保留三页、固定页签、滚动和私有文字/按钮样式。
- 回归过程：先添加满载启动合同，旧制冷在360秒内真实食品未达到深冻线，预期失败；将原有候选判断抽出到可在托管环境运行的 SupportsGas 后，气态铜排除检查也预期失败。修复后通过。更快换热使旧断电测试的“刚插入热食品后仍深冻”前提不再成立，调整为验证被升温后的温度仍有惯性、环境泄漏温变有界且不重置。
- 核心检查：91项通过。新增工况包含一堆500 kg、比热4 kDTU/kg/K、食品/柜内20°C、恒定30°C外壁、不开门且足额付款，按真实浮点物品温变、原有制冷启停计算，360秒内食品与柜内均低于−18°C，热量残差<0.001 kDTU；另覆盖相态、50°C边界、过热/过冷/非有限温度、旧目标恢复及取消目标/库存不被改写。该工况没有模拟真实厨房散热、原生物品拆分或完整Unity世界，不能承诺任意实机负载均在该时间达标。
- 原生托管/静态检查：52项通过，包括原生映射与Element相变夹具的气态铜/蒸汽排除、CO₂/氯气/氢气及第三方气体支持、已知缺字字符禁用、原生横排模板接口、原有字段KSerialization往返和文字对比度。没有初始化ElementLoader/Unity世界；真实气管、完整旧存档迁移及按钮几何仍需实机验收。
- 构建和静态/素材：Release编译0错误，保留两项既有MSB3277依赖版本警告。strict面板静态审计0 findings；13个改动文件尾随空白、六个配色与DESIGN.md一致性及旧循环控件/缺字字符移除检查通过。素材结构/运动3515项通过；未重新导出动画。打包检查12个文件、唯一自有DLL、ZIP路径及逐文件SHA256通过。
- 安装：游戏进程未运行时，将已安装0.1.1的全部12个文件备份至 `dist/install-backups/BaiyeCryogenicPantry-0.1.1-20261006-172407/`，逐文件核对备份，升级 `D:/Documents/Klei/OxygenNotIncluded/mods/local/BaiyeCryogenicPantry/` 至0.1.2并检查全部安装哈希。mod.yaml身份和六个动画文件与旧版一致；建筑/饮料ID、序列化字段和六仓顺序保留。日志更新后重打包并同步最终日志，再核对最终12个安装文件；报告留存在上述安装验证JSON。未改mods.json。
- 未验证：修正后未启动游戏/Unity/SimDLL。实际布局、字体初始化与不再缺字、滚动/点击、全部管路温度及拒收质量、取消中途换气、0.1.1完整世界读档与库存/任务身份、加速制冷的厨房热积累和过热、真实食品新鲜度及其他模组兼容性仍待 `docs/playtest.md`。加速制冷增加峰值排热，实机必须有有效热出口。未提交、推送或发布。

## 2026-10-06 · 0.1.1 侧栏高度与饮料文字修复，更新本地安装

- 授权：用户报告保鲜柜列表超出游戏屏幕、饮料制作列表颜色看不清；检查后提出三页侧栏、按实际剩余空间限高、固定页签与可见滚动条、独立文字样式及编译/打包/安装核对方案，用户回复“修改”。本次仅涉及该修复范围。
- 实际源文件：新增 `src/Core/PantryPanelLayout.cs`；修改 `src/UI/PantrySideScreen.cs`、`CryogenicPantry.csproj`、`tests/Regression/Program.cs`、`tests/Native/Program.cs`、`mod_info.yaml`、`DESIGN.md`、`UX-CONTRACT.md`、`premium-ui.json`、`premium-audit.json`、`README.md`、`docs/playtest.md` 和本日志。生成 `dist/BaiyeCryogenicPantry-0.1.1.zip`、对应 `.sha256`、分发暂存目录及 `dist/verification/installation-0.1.1.json`。未修改其他模组。
- 高度与导航：将原来堆叠的内容分为“保鲜 / 饮料 / 高级”三个原生按钮页签。固定顶部页签，单一内容 ScrollRect 与永久可见滚动条；面板上限 360 画布像素，取消强制 240 最小高度，按实际顶边、裁切区域、下方同级面板和底部留白计算可用空间。保鲜页保留常用补气/切气，饮料页保留四配方及单项展开，高级页保留原料保留量和耗用账目。切建筑回到保鲜页、收起配方，切页滚动回顶部；这些视图状态不写入存档。
- 文字：原生 LocText/SetTextStyleSetting 初始化或刷新会重新应用模板样式，覆盖临时赋予的文字颜色。每个克隆标签改为独立 TextStyleSetting，按钮保留独立 ColorStyleSetting，浅色区使用深蓝/深灰文字；同时控制悬停、选中、按下和禁用底色。原生字体与换行保留，标签首选高度取自然文字高度，不修改共享游戏主题。
- 编译过程：首次编译发现新增 Canvas/RectTransformUtility 依赖缺少 UnityEngine.UIModule 引用；补充 `Private=false` 的游戏程序集引用后，Release 编译 0 错误、两项既有 MSB3277 依赖版本警告。该游戏程序集不进入分发包。
- 核心检查：71 项通过，其中新增 26 项高度预算检查覆盖短空间、多种 UI 缩放、展开内容上限与自然收缩；原生托管/静态契约 42 项通过，包含原有存档字段往返和新增 7 组实际配色的文字对比度检查。高度预算检查是纯数值检查，不代表 Unity 布局验收；配色检查也不代表实机初始化或字体显示验收。
- 素材与静态检查：现有 KAnim 资源的 3515 项结构/运动断言通过；未改美术。面板 strict 静态审计 0 findings；12 个改动文件尾随空白检查及六个运行时配色与 DESIGN.md 一致性检查通过。当前环境未提供 `designmd` 官方 lint 命令，因此未运行该命令；使用现有项目审计与配色一致性检查。打包核对 12 个文件、唯一自有 DLL、所有 ZIP 路径及逐文件 SHA256，通过。
- 安装：在游戏进程未运行时，完整备份原来安装的 12 个文件至 `dist/install-backups/BaiyeCryogenicPantry-0.1.0-20261006-163326/`，逐文件确认备份一致，再更新 `D:/Documents/Klei/OxygenNotIncluded/mods/local/BaiyeCryogenicPantry/` 为 0.1.1。逐文件安装哈希检查通过，mod.yaml 身份及六个动画文件与旧版一致。更新日志后重打包，将最终分发日志同步至安装目录，再核对最终 12 个文件；结果保留在上述安装报告中。未更改 mods.json。
- 未验证：未启动 Unity 或游戏。720p/1080p、多种 UI 缩放、其他原生面板展开时的实际剩余空间、滚动条拖动/滚轮、文字初始化与状态色、英文长文换行、切建筑与读档等仍需按 `docs/playtest.md` 实机验收；不能仅凭托管检查保证所有屏幕上的实际布局。建筑/饮料/存档 ID、运行逻辑、配方、存储顺序及动画资源保持。未提交、推送或发布。

## 2026-10-06 · 0.1.0 本地安装

- 授权：用户明确要求“安装”。核对最新包及本地 MOD 目录后执行首次安装。
- 实际文件：将 `dist/BaiyeCryogenicPantry-0.1.0.zip` 的全部 12 个文件解压至 `D:/Documents/Klei/OxygenNotIncluded/mods/local/BaiyeCryogenicPantry/`，包含自有 DLL、两套动画的六个资源、mod.yaml、mod_info.yaml、README.md、CHANGELOG.md 和 PLAYTEST.md；源项目仅更新本日志。
- 检查：ZIP SHA256 为 `e513a6d334da7e714514c8a56d2bebdc30c043178f6db39ce0272151adeb8c78`，与校验文件一致。逐项检查归档路径范围、安装文件数、唯一自有 DLL 和全部 12 个安装文件的 SHA256，均通过。目标安装目录此前不存在，无旧版覆盖。
- 状态：游戏进程正在运行，新增本地安装文件夹，未终止游戏、修改 mods.json 或改变其他模组。需要用户重启游戏，在模组列表启用“恒温气调保鲜柜”；本次没有加载到游戏或执行实机验收。
- 安装的是上述已验证压缩包；本日志的安装记录保留于源项目，不为此次记录重打包或改动已安装运行文件。未提交、推送或发布。

## 2026-10-06 · 0.1.0 美术轻度做旧与运行动画核对

- 授权：用户要求增加类似 Klei 建筑的少量做旧/废土气息，明确仅微调，并询问液体与运行效果；继续已批准的外观与动画范围。最新指示覆盖此前完全禁止使用痕迹的偏好。
- 实际文件：新增 `art/source/future-pantry-parts-oni-v3.png`、`art/source/oni-v3-prompt.txt`、`art/cooling-preview.gif`；修改 `tools/build-anims.py`、`tools/verify-assets.py`、`AGENTS.md`、`DESIGN.md`、`README.md`、`art/README.md`、`docs/playtest.md` 和本日志；重新生成两组 `art/source/baiye_*/` 部件 PNG/SCML、两组 `anim/assets/baiye_*/` PNG/anim.bytes/build.bytes、`art/pantry-preview.png`、`art/processing-preview.gif`。更新 `dist/BaiyeCryogenicPantry-0.1.0.zip` 与 `.sha256`；`dist/verification/` 留存原生图集重新解析后的部件、SCML 与抽帧检查图，不进入分发包。
- 外观：内置 image_gen 编辑 v2 源图，加入稀疏灰蓝底漆露出、边角掉漆、浅划痕和接缝积灰；主体轮廓及冷白/深蓝/青蓝保持，避免大面积锈蚀或黄铜重设计。门窗改为层架暗影，液管去除烘焙的静止气泡。源图 1447×1087 RGBA、alpha 0–255；原图各版留存，完整提示词在 `art/source/oni-v3-prompt.txt`。
- 动画：保留原有运行状态接入，制冷转子、加工叶轮/喷嘴/独立气泡、取放门动作均为分离部件。加工气泡由三个符号逐帧上升，略增尺寸提高辨识；非加工片段不含气泡。上方气罐与玻璃反光为静态材质，管内动画表示加工状态，并非流体物理或真实液位。另导出制冷动图，与加工动图均为 40 帧、20 fps 的离线合成。
- 导出故障与修复：初次导出失败于原临时目录的 Kanimal 已被清理；旧二进制仍能通过结构检查，因此未将该结果作为新版资源完成依据。从官方 `skairunner/kanimal-SE` 的 1.3.31 发布恢复同版本 Windows NET dependent 工具后重新导出；补充逐部件独立动作断言与可选 `--roundtrip` 像素检查，避免只验证旧资源结构。
- 验证：两组 KAnim 导出成功；结构/独立运动断言 3515 项通过，包括喷嘴、叶轮、转子、门与三个气泡上升、非加工片段无气泡。用 Kanimal 将两套最终二进制重新解析，再运行 `tools/verify-assets.py --roundtrip dist/verification`，共 3579 项通过；16 张部件 alpha 与当前源完全一致，可见 RGB 误差 ≤1/255。人工查看组装预览和加工第 10 帧，门窗与管内气泡位置正常。Release 编译 0 错误，保留两项既有依赖版本警告；包内 12 个文件、单一自有 DLL 和逐文件 ZIP 哈希校验通过，更新日志后重打包纳入本记录。
- 未验证：未运行 Unity 或游戏，实际缩放、与原版画风并排、状态切换及流动/人物动作仍待实机验收；已补充验收清单。本轮未改变运行逻辑、存档 ID 或配方，也未重跑先前核心规则/原生托管行为检查。未安装、提交、推送或发布。

## 2026-10-06 · 0.1.0 美术轻调：贴近缺氧手绘画风

- 授权：用户认可当前方向，并要求“和缺氧的画风差别有些大，可以稍微调整一下”；属于已批准外观范围内的迭代。
- 实际文件：新增 `art/source/future-pantry-parts-oni-v2.png`、`art/source/oni-v2-prompt.txt`；修改 `tools/build-anims.py`、`art/README.md`、`DESIGN.md`、本日志；重新导出两组 `art/source/baiye_*/` 的部件 PNG/SCML、两组 `anim/assets/baiye_*/` 的 PNG/anim.bytes/build.bytes，以及 `art/pantry-preview.png`、`art/processing-preview.gif`。更新 `dist/BaiyeCryogenicPantry-0.1.0.zip` 和 `.sha256`。
- 调整：内置 image_gen 编辑原有透明部件图，保留冷白/深蓝/青蓝配色、12 个部件与未来设备结构；减少光滑渐变、金属高光和复杂玻璃反射，改用略不规则的粗轮廓、平涂色块与单层硬边阴影。四种饮料瓶同步画风。原始源图留存，完整提示词见 `art/source/oni-v2-prompt.txt`。
- 导入：新源图为 1448×1086 RGBA，alpha 0–255；调整顶部四部件裁切边界，现有图层尺寸、动画坐标、门/转子/喷嘴/叶轮动作与占格沿用。离线检查裁切边缘仅有 alpha ≤4 的近透明残点，没有可见轮廓截断；检查组装预览，确认柜体、门与饮料模块保持完整。
- 验证：两组 KAnim 导出成功；素材 3470 项断言通过，覆盖原生二进制符号、图集 alpha、2×3 几何和机械动作。Release 编译 0 错误，仍有两项既有游戏依赖版本警告；12 个包文件、单一自有 DLL 和逐文件 ZIP 哈希校验通过，日志更新后重新打包纳入本记录。
- 未验证：本轮为美术资源迭代，未重跑既有核心逻辑和原生托管行为检查；未运行 Unity 或游戏，缩放后的最终画风、与原版建筑并排效果及实机动画仍需验收。预览是导出坐标的离线合成。未安装、提交、推送或发布，未改运行逻辑及存档 ID。

## 2026-10-06 · 0.1.0 首版开发包

- 授权：用户确认审核后的完整开发范围，并要求从废土机械风改为干净、科技感与未来感的缺氧卡通设备。
- 实际文件：新增 `CryogenicPantry.csproj`、`mod.yaml`、`mod_info.yaml`、`AGENTS.md`、`.gitignore`；`src/Core/PantryRules.cs`；`src/Game/{PantryConfig,PantryMod,PantryBuilding,PantryDrinks,PantryStatus}.cs`；`src/UI/PantrySideScreen.cs`；`tests/Regression/{Regression.csproj,Program.cs}`、`tests/Native/{Native.csproj,Program.cs}`；`tools/{build-anims.py,verify-assets.py,build-package.ps1}`；`README.md`、`DESIGN.md`、`UX-CONTRACT.md`、`premium-ui.json`、`premium-audit.json`、本日志及 `docs/{原始设计文档.md,playtest.md}`。
- 美术文件：`art/source/{future-pantry-concept.png,future-pantry-parts.png}`、两组 `art/source/baiye_*/` 内导入部件与可编辑 SCML；`anim/assets/baiye_{cryogenic_pantry,pantry_drinks}/` 内原创 PNG/build.bytes/anim.bytes；`art/{README.md,pantry-preview.png,processing-preview.gif}`。
- 实现：2×3、食品 500 kg/饮料 50 kg/气体 2 kg 分仓、水缓冲 20 kg；400 kg 精炼金属+200 kg 塑料、LiquidTemperature 解锁。原生食品筛选/搬运，局限本柜库存的原生腐败环境适配，未锁新鲜度、未使用 Preserve 修饰。
- 制冷：目标 −22°C，深冻使用食物定义的严格阈值。内部双精度热量模型、原生物品温度实际变化结算，电路实际付款驱动制冷/补气/加工；制冷热及电耗热进入原生结构温度系统，断电自然回温。保温 240 W/制冷 600 W/待机 120 W，加工额外 120 W，峰值 720 W。
- 气氛：读取当前原生 Sterilizing 映射、拒收非所选气体；真实入管收据推进置换，95% 完成，混合仅存在于置换过渡。开门损耗、配方 CO₂、置换废气分别记账；普通损耗/置换废气明确为抽象消耗，拆除及完全损坏真实释放剩余气体。
- 饮料：四种可扩展 1 kg 配方；扩展可复用通用瓶装图、自供动画或选择外部 prefab 所有者，避免重复注册。实际原料预留和保留量、目标份数、无限与启停。批次及饮用中断保留/退回真实库存，效果仅在饮用完成后生效；复用原生休闲任务/人物动画及病菌摄入，饮料互斥和冷却期，普通/仿生复制人的能量低谷序列化组件。
- 外观与 UI：干净冷白未来设备，原生深蓝状态卡与浅色控制区；StatusItem 首先呈现状态，四配方逐项展开，高级库存折叠，原生按钮与字体；独立门/转子/喷嘴/叶轮/气泡动作，不修改其他面板主题。
- 回归过程：制冷占位实现先触发“cooling extracts paid heat”预期失败。后续长周期热量检查暴露 305.734 kDTU 单精度累计偏差，改为双精度柜体及真实物品可表达温变后通过更严格能量检查。扩展配方拒绝 NaN/Infinity 与重复原料，避免绕过保留量。
- 最终审查：已预留原料的批次在切到氯气或 CO₂ 置换未完成时也暂停，重新建立 CO₂ 气氛后继续；关闭生产不扣额外加工功率。电费结算前冻结本步制冷/加工计划，刚补齐原料或气氛时等待下个已付加工功率步，待机不会免费启动制冷。断电普通冷藏显示“依靠余冷”，不误报正在降温；能量低谷使用原生负面效果标志。
- 验证：本机 Player.log 核对 U59-744825，当前程序集编译 0 错误；存在两项游戏依赖版本警告 MSB3277（System.IO.Compression/System.Net.Http）。核心规则检查最终 45 项；原生托管/静态契约 35 项，包括真实 KSerialization 温度、气体、进度、配方与保留量字段往返。素材 3470 项断言，覆盖导出的原生符号、图集 alpha、2×3 几何和机械动画。面板静态 strict 审计 0 findings；源文件尾随空白检查通过。
- 包：`dist/BaiyeCryogenicPantry-0.1.0.zip` 与 `.sha256`，仅一个自有 DLL；工具成功校验 12 个分发文件和逐文件 ZIP 哈希。首次打包发现 PowerShell 数组拼接把三个资源名合成一个路径，已改为三个明确字符串；修复后打包成功，最终审查后重新打包以包含最终 DLL 与日志。
- 未验证：Unity/SimDLL、游戏启动与真实世界存档、管路/热量实际运行、复制人 AI/站位/饮用、面板缩放/键盘/滚动和完整 MOD 兼容性；见 `docs/playtest.md`。所有预览为图层合成，不是游戏截图。首轮平衡参数尚未实机调校。
- 未执行安装、提交、推送或发布；只新增本项目，未修改其他模组及工作区公共文档。
