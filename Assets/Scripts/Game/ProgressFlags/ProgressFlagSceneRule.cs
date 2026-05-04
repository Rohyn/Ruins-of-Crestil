using System;
using UnityEngine;

namespace ROC.Game.ProgressFlags
{
    [Serializable]
    public struct ProgressFlagSceneRule
    {
        [SerializeField] private string sceneId;
        [SerializeField] private ProgressFlagSceneEventType eventType;
        [SerializeField] private ProgressFlagRequirement[] requirements;
        [SerializeField] private ProgressFlagMutation[] mutations;

        public string SceneId => sceneId;
        public ProgressFlagSceneEventType EventType => eventType;
        public ProgressFlagRequirement[] Requirements => requirements;
        public ProgressFlagMutation[] Mutations => mutations;
    }
}