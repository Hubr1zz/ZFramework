using ZFramework;
using UnityEditor;
using UnityEngine;

public static class ZFrameworkSettingsProvider  
{  
    [MenuItem("ZFramework/Settings/ZFramework UpdateSettings", priority = -1)]
    public static void OpenSettings() => SettingsService.OpenProjectSettings("Project/ZFramework/UpdateSettings");
    
    private const string SettingsPath = "Project/ZFramework/UpdateSettings";  

    [SettingsProvider]  
    public static SettingsProvider CreateMySettingsProvider()  
    {  
        return new SettingsProvider(SettingsPath, SettingsScope.Project)  
        {  
            label = "ZFramework/UpdateSettings",  
            guiHandler = (searchContext) =>
            {
                var settings = Settings.UpdateSetting;  
                var serializedObject = new SerializedObject(settings);  

                EditorGUILayout.PropertyField(serializedObject.FindProperty("projectName"));  
                EditorGUILayout.PropertyField(serializedObject.FindProperty("UpdateStyle"));  
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ResDownLoadPath"));  
                EditorGUILayout.PropertyField(serializedObject.FindProperty("FallbackResDownLoadPath"));  
                serializedObject.ApplyModifiedProperties();  
            },  
            keywords = new[] { "ZFramework", "Settings", "Custom" }  
        };  
    }
}
