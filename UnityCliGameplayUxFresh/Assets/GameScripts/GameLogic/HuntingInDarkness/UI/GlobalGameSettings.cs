using UnityEngine;

namespace UI
{
    /// <summary>少量跨阶段玩家设置。数值写入 PlayerPrefs，控制器只读取倍率，不持有菜单引用。</summary>
    public static class GlobalGameSettings
    {
        private const string CameraPanSpeedKey = "HID.CameraPanSpeed";
        private const string CameraZoomSpeedKey = "HID.CameraZoomSpeed";
        private const string MasterVolumeKey = "HID.MasterVolume";

        public static float CameraPanSpeed { get; private set; } = 1f;
        public static float CameraZoomSpeed { get; private set; } = 1f;
        public static float MasterVolume { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            CameraPanSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(CameraPanSpeedKey, 1f), 0.25f, 3f);
            CameraZoomSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(CameraZoomSpeedKey, 1f), 0.25f, 3f);
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            AudioListener.volume = MasterVolume;
        }

        public static void SetCameraPanSpeed(float value)
        {
            CameraPanSpeed = Mathf.Clamp(value, 0.25f, 3f);
            PlayerPrefs.SetFloat(CameraPanSpeedKey, CameraPanSpeed);
        }

        public static void SetCameraZoomSpeed(float value)
        {
            CameraZoomSpeed = Mathf.Clamp(value, 0.25f, 3f);
            PlayerPrefs.SetFloat(CameraZoomSpeedKey, CameraZoomSpeed);
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        }
    }
}
