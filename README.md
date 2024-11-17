# 以管理员身份自启动

本插件可以通过计划任务，让 ClassIsland 在开机时以管理员身份自启动。

![图片](https://github.com/user-attachments/assets/55e09328-5cde-49b9-be42-6e48a895f8eb)

## 使用方法

1. 在插件市场下载本插件；
2. 转到[【管理员自启动】](classisland://app/settings/classisland.startUpAsAdmin)页面；
3. 点击【创建/更新计划任务】按钮；
4. 大功告成！现在 ClassIsland 可以在用户登录时以管理员身份自启动了。

## 注意事项

如果您在创建计划任务之后移动了 ClassIsland 程序本体，需要按照上面的步骤再创建一遍计划任务。创建的计划任务不会随插件卸载/禁用而被删除。

## 致谢

本项目使用了以下第三方库：

- [TaskScheduler](https://github.com/dahall/taskscheduler)
- [ClassIsland.PluginSdk](https://github.com/ClassIsland/ClassIsland/)

## 许可证

本项目基于 [GNU General Public License v3.0](https://github.com/ClassIsland/StartUpAsAdmin/blob/master/LICENSE.txt) 许可。
