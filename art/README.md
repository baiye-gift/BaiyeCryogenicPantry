# 原创美术与导入

- `source/future-pantry-concept.png`：imagegen 新版未来柜概念，依据用户“干净、科技感、未来感、缺氧画风”的纠正生成。
- `source/future-pantry-parts.png`：imagegen 生成的透明部件图，保留原始 alpha。未采用用户拒绝的第一版废土风格。
- `source/future-pantry-parts-oni-v2.png`：内置 image_gen 编辑上一版源图；根据用户“稍微调整、贴近缺氧画风”的反馈，减少高光、渐变和玻璃反射，使用粗线条、平涂和硬边阴影。保留 12 个部件、冷白/深蓝/青蓝配色与原有结构；留作追溯。
- `source/oni-v2-prompt.txt`：本轮完整生成提示词与工具参数。原始画布 1448×1086，RGBA alpha 0–255；导入矩形适当留边，避免新轮廓被裁断。
- `source/future-pantry-parts-oni-v3.png`：当前运行素材源图，内置 image_gen 编辑 v2，加入稀疏边角磨损、浅划痕和接缝积灰，保留未来配色与结构。门窗改为层架暗影，液管去除烘焙静止气泡。1447×1087 RGBA，原始 alpha 保留。完整提示词在 `source/oni-v3-prompt.txt`。
- `source/baiye_cryogenic_pantry`：独立外壳、门、制冷转子、气罐、加工叶轮、气泡管、喷嘴、状态条、效果粒子及 SCML。
- `source/baiye_pantry_drinks`：四种独立瓶装物品及 SCML。
- `tools/build-anims.py` 只做游戏资源导入所需的裁切、缩放、图层组合与动画坐标导出，不重绘 AI 源图；气泡为简单运行效果粒子。
- `pantry-preview.png`、`processing-preview.gif` 与 `cooling-preview.gif` 为与导出坐标一致的离线合成预览，没有运行 Unity。

图层角色：静态青蓝轮廓为材质涂装；独立 status 图层是工作指示，off 动画不含此图层。制冷动画旋转 rotor，加工动画增加叶轮旋转、喷嘴泵送和气泡上升。门动作单独循环一次约 2 秒，但游戏取放状态持续 1 秒；只表示门机械短暂移动。

液管气泡为三个独立可移动符号，只在 processing 片段中出现，表示饮料加工；v3 移除管图上的静止气泡，并略放大移动气泡以改善小尺寸辨识。上方气罐和观察窗反光保持静态。此处没有流体物理动画，也不把装饰管的蓝色面积当作真实料位。运行时通过 `PantryBuilding.UpdateAnimation()` 接入有电、制冷、加工、门动作与损坏状态；实机播放尚未验收。

只有原创图集和 KAnim 进入分发包。复制人使用本机原生饮用动画引用，原版人物资源与游戏 DLL 不分发。生成源图无第三方上传参考，风格参考用户要求的缺氧卡通设备。
