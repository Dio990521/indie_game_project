using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Dialogue;

namespace IndieGame.Core
{
    // 对话系统事件：显示/隐藏、打字机、说话人、玩家交互检测
    // （L5 重构：原 GameEvents.cs 单文件 1000+ 行，按领域拆分为 GameEvents.*.cs 多文件，
    // 命名空间与全部类型定义保持不变，纯文件级重组。）
    /// <summary>
    /// 对话界面显示请求事件：
    /// 由 DialogueManager 发布，DialogueUIView 监听并执行显示动画。
    /// </summary>
    public struct DialogueShowRequestEvent
    {
        // 是否播放显示动画
        public bool Animate;
    }

    /// <summary>
    /// 对话界面隐藏请求事件：
    /// 由 DialogueManager 发布，DialogueUIView 监听并执行隐藏动画。
    /// </summary>
    public struct DialogueHideRequestEvent
    {
        // 是否播放隐藏动画
        public bool Animate;
    }

    /// <summary>
    /// 对话说话人文本更新事件：
    /// Manager 在切换到新句子后发布，View 只负责渲染文本。
    /// </summary>
    public struct DialogueSpeakerChangedEvent
    {
        // 当前句子的说话人（本地化解析后的最终字符串）
        public string SpeakerText;
    }

    /// <summary>
    /// 对话打字机启动请求事件：
    /// Manager 传入已经完成富文本处理的文本，View 按速度执行逐字显示。
    /// </summary>
    public struct DialogueTypewriterRequestEvent
    {
        // 富文本格式内容（已包含关键词高亮）
        public string FormattedText;
        // 每个可见字符之间的间隔（秒）
        public float Speed;
    }

    /// <summary>
    /// 对话打字机跳过请求事件：
    /// 当玩家在打字中按下交互键时，Manager 发布该事件，View 应立即展示完整文本。
    /// </summary>
    public struct DialogueSkipTypewriterRequestEvent
    {
    }

    /// <summary>
    /// 对话打字机完成事件：
    /// View 在“自然打完”或“被跳过后立即显示完整文本”两种情况下都要发布此事件，
    /// Manager 监听后把 IsTyping 切换为 false。
    /// </summary>
    public struct DialogueTypewriterCompletedEvent
    {
    }

    /// <summary>
    /// 对话开始事件：
    /// 方便日志系统、教程系统或其他模块监听“进入对话”的时机。
    /// </summary>
    public struct DialogueStartedEvent
    {
        // 当前启动的对话资源
        public DialogueDataSO DialogueData;
    }

    /// <summary>
    /// 对话结束事件：
    /// 用于外部系统监听“对话流程已结束”。
    /// </summary>
    public struct DialogueEndedEvent
    {
        // 刚刚结束的对话资源
        public DialogueDataSO DialogueData;
    }
    /// <summary>
    /// 玩家“当前可交互目标”变更事件：
    /// 由 PlayerInteractionDetector 在目标切换时广播。
    /// 常见用途：交互提示 UI（如“按 E 对话”）的显示/隐藏与目标名刷新。
    /// </summary>
    public struct PlayerInteractableTargetChangedEvent
    {
        // 是否存在可交互目标
        public bool HasTarget;
        // 当前目标对象（HasTarget=false 时为 null）
        public GameObject Target;
    }

    /// <summary>
    /// 玩家交互执行完成事件：
    /// 由 PlayerInteractionController 在成功调用 IInteractable.Interact 后广播。
    /// 常见用途：音效、日志、教学引导统计。
    /// </summary>
    public struct PlayerInteractionPerformedEvent
    {
        // 发起交互的对象（通常是玩家）
        public GameObject Interactor;
        // 被交互的目标对象
        public GameObject Target;
    }
}
