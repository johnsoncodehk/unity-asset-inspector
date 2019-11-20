using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class AssetInspectorWindow : EditorWindow
{

    [MenuItem("Window/Asset Inspector")]
    static void Init()
    {
        AssetInspectorWindow editor = (AssetInspectorWindow)EditorWindow.GetWindow(typeof(AssetInspectorWindow));
        editor.titleContent = new GUIContent("Asset Inspector");
    }

    [MenuItem("CONTEXT/Object/Delete Asset")]
    static void DeleteAssetMenuItem(MenuCommand command)
    {
        if (EditorUtility.DisplayDialog("Delete selected asset?", "   " + AssetDatabase.GetAssetPath(command.context) + "\n    - " + command.context.name + "\n\nYou cannot undo this action.", "Delete", "Cancel"))
        {
            string assetPath = AssetDatabase.GetAssetPath(command.context);
            AssetImporter assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(command.context));
            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Object.DestroyImmediate(command.context, true);
            EditorUtility.SetDirty(assetImporter);
            assetImporter.SaveAndReimport();
            Selection.activeObject = mainAsset;
        }
    }

    [MenuItem("CONTEXT/Object/Delete Asset", true)]
    static bool DeleteAssetMenuItemValidate(MenuCommand command)
    {
        return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(command.context))
            && !AssetDatabase.IsMainAsset(command.context);
    }

    [MenuItem("CONTEXT/Object/Copy Asset")]
    static void CopyAssetMenuItem(MenuCommand command)
    {
        m_AssetCopySource = System.Activator.CreateInstance(command.context.GetType()) as Object;
        EditorUtility.CopySerialized(command.context, m_AssetCopySource);
    }

    [MenuItem("CONTEXT/Object/Copy Asset", true)]
    static bool CopyAssetMenuItemValidate(MenuCommand command)
    {
        return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(command.context));
    }

    [MenuItem("CONTEXT/Object/Paste Asset Data")]
    static void PasteAssetMenuItem(MenuCommand command)
    {
        EditorUtility.CopySerialized(m_AssetCopySource, command.context);
    }

    [MenuItem("CONTEXT/Object/Paste Asset Data", true)]
    static bool PasteAssetMenuItemValidate(MenuCommand command)
    {
        return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(command.context))
            && m_AssetCopySource
            && m_AssetCopySource.GetType() == command.context.GetType();
    }

    [MenuItem("CONTEXT/Object/Paste Asset As New")]
    static void PasteAssetAsNewMenuItem(MenuCommand command)
    {
        Object assetCopy = System.Activator.CreateInstance(m_AssetCopySource.GetType()) as Object;
        EditorUtility.CopySerialized(m_AssetCopySource, assetCopy);
        AssetDatabase.AddObjectToAsset(assetCopy, Selection.activeObject);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("CONTEXT/Object/Paste Asset As New", true)]
    static bool PasteAssetAsNewMenuItemValidate(MenuCommand command)
    {
        return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(command.context))
            && m_AssetCopySource;
    }

    static Object m_AssetCopySource = null;
    Vector2 m_ScrollPosition;
    Dictionary<int, bool> m_Folds = new Dictionary<int, bool>();

    void OnGUI()
    {
        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, new GUILayoutOption[0]);
        EditorGUILayout.Space();
        DrawAssets();
        EditorGUILayout.EndScrollView();
    }

    void OnSelectionChange()
    {
        m_Folds.Clear();
        Repaint();
    }

    void DrawAssets()
    {
        Object activeObject = Selection.activeObject;
        if (!activeObject)
            return;

        string assetPath = AssetDatabase.GetAssetPath(activeObject);
        if (string.IsNullOrEmpty(assetPath))
            return;

        AssetImporter assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(activeObject));

        string newAssetPath = EditorGUILayout.DelayedTextField("Asset Path", assetPath);
        SelectableLabelField("GUID", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(activeObject)));
        SelectableLabelField("Time Stamp", assetImporter.assetTimeStamp.ToString());
        assetImporter.userData = TextAreaField("User Data", assetImporter.userData);

        if (newAssetPath != assetPath)
            AssetDatabase.MoveAsset(assetPath, newAssetPath);

        EditorGUI.indentLevel++;

        Object[] allAsset = activeObject.GetType() != typeof(SceneAsset) ?
            AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(asset => asset != null).ToArray() :
            new Object[] { activeObject };

        foreach (Object asset in allAsset)
            DrawAssetElement(asset);

        EditorGUI.indentLevel--;
    }

    void DrawAssetElement(Object asset)
    {
        int instanceID = asset.GetInstanceID();
        m_Folds[instanceID] = EditorGUILayout.InspectorTitlebar(GetFold(instanceID), asset);
        if (m_Folds[instanceID])
        {
            EditorGUI.BeginChangeCheck();
            asset.name = EditorGUILayout.TextField("Name", asset.name);
            asset.hideFlags = (HideFlags)EditorGUILayout.EnumPopup("Hide Flags", asset.hideFlags);
            if (EditorGUI.EndChangeCheck())
                AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(asset)).SaveAndReimport();

            GUI.enabled = !AssetDatabase.IsMainAsset(asset);
            EditorGUI.BeginChangeCheck();
            bool isMainAsset = EditorGUILayout.Toggle("Is Main Asset", AssetDatabase.IsMainAsset(asset));
            if (EditorGUI.EndChangeCheck() && isMainAsset)
                SetMainObject(asset);
            GUI.enabled = true;
        }
    }

    bool GetFold(int instanceID)
    {
        if (!m_Folds.ContainsKey(instanceID))
        {
            m_Folds[instanceID] = Selection.objects.Any(o => o.GetInstanceID() == instanceID);
        }
        return m_Folds[instanceID];
    }

    static void SetMainObject(Object asset)
    {
        string assetPath = AssetDatabase.GetAssetPath(asset);
        AssetImporter assetImporter = AssetImporter.GetAtPath(assetPath);
        string name = asset.name;
        string newPath = System.IO.Path.GetDirectoryName(assetPath) + System.IO.Path.AltDirectorySeparatorChar + name + System.IO.Path.GetExtension(assetPath);
        string validate = AssetDatabase.ValidateMoveAsset(assetPath, newPath);
        if (string.IsNullOrEmpty(validate) || assetPath == newPath)
        {
            AssetDatabase.SetMainObject(asset, AssetDatabase.GetAssetPath(asset));
            EditorUtility.SetDirty(assetImporter);
            assetImporter.SaveAndReimport();
            AssetDatabase.MoveAsset(assetPath, newPath);
            assetPath = newPath;
        }
        else
        {
            Debug.Log(validate);
        }
    }

    static void SelectableLabelField(string key, string val)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(key);
        int indentLevel = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        EditorGUILayout.SelectableLabel(val, GUILayout.Height(16));
        EditorGUI.indentLevel = indentLevel;
        EditorGUILayout.EndHorizontal();
    }

    static string TextAreaField(string label, string text, params GUILayoutOption[] options)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        int indentLevel = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        string newText = EditorGUILayout.TextArea(text, options);
        EditorGUI.indentLevel = indentLevel;
        EditorGUILayout.EndHorizontal();
        return newText;
    }
}
