using UnityEngine;

namespace ZFramework
{
    [CreateAssetMenu(menuName = "ZFramework/AudioSetting", fileName = "AudioSetting")]
    public class AudioSetting : ScriptableObject
    {
        public AudioGroupConfig[] audioGroupConfigs = null;
    }
}