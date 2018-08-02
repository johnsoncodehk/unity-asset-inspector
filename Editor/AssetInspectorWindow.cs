using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class AssetInspectorWindow : EditorWindow {

	[MenuItem("Window/Asset Inspector")]
	private static void Init() {
		AssetInspectorWindow editor = (AssetInspectorWindow)EditorWindow.GetWindow(typeof(AssetInspectorWindow));
		editor.titleContent = new GUIContent("AssetInspector");
	}

	private Vector2 m_ScrollPosition;
	private Object m_CopyingObj = null;
	private Dictionary<int, bool> m_Folds = new Dictionary<int, bool>();
	private bool m_MainFoldOut = true;

	void OnGUI() {
		m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, new GUILayoutOption[0]);
		EditorGUILayout.Space();
		DrawAsset();
		EditorGUILayout.EndScrollView();
	}
	void OnSelectionChange() {
		m_Folds.Clear();
		Repaint();
	}

	private void DrawAsset() {
		Object activeObject = Selection.activeObject;
		if (activeObject == null) {
			return;
		}
		string assetPath = AssetDatabase.GetAssetPath(activeObject);
		if (string.IsNullOrEmpty(assetPath)) {
			return;
		}

		AssetImporter assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(activeObject));

		if (m_MainFoldOut = EditorGUILayout.Foldout(m_MainFoldOut, "Meta Data")) {
			EditorGUI.indentLevel++;

			LabelField("GUID", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(activeObject)));
			LabelField("Time Stamp", assetImporter.assetTimeStamp.ToString());
			Object mainObj = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(activeObject));
			Object newMainObj = EditorGUILayout.ObjectField("Main Object", mainObj, typeof(Object), false);
			if (newMainObj != null && mainObj != newMainObj) {
				if (AssetDatabase.GetAssetPath(mainObj) == AssetDatabase.GetAssetPath(newMainObj)) {
					SetMainObject(newMainObj);
				}
				else {
					Debug.Log(newMainObj.name + " is not child asset.");
				}
			}
			TextArea(assetImporter, "User Data", assetImporter.userData, userData => assetImporter.userData = userData);
			LabelField("", "");

			EditorGUI.indentLevel--;
		}

		m_CopyingObj = EditorGUILayout.ObjectField("Copying Asset", m_CopyingObj, typeof(Object), false);
		DrawButton(m_CopyingObj != null, "Paste Asset As New", () => {
			object obj = System.Activator.CreateInstance(m_CopyingObj.GetType());
			if (obj != null) {
				Object unityObj = obj as Object;
				if (unityObj != null) {
					EditorUtility.CopySerialized(m_CopyingObj, unityObj);
					AssetDatabase.AddObjectToAsset(unityObj, Selection.activeObject);
					AssetDatabase.SaveAssets();
					AssetDatabase.Refresh();
					Selection.activeObject = unityObj;
				}
				else
					Debug.Log(m_CopyingObj.GetType() + " is not a Unity Object.");
			}
			else
				Debug.Log(m_CopyingObj.GetType() + " can not Instance.");
		});


		EditorGUI.indentLevel++;
		Object[] allAsset = new Object[] {
			activeObject,
		};
		if (activeObject.GetType() != typeof(SceneAsset)) {
			allAsset = AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(asset => asset != null).ToArray();
		}
		foreach (Object asset in allAsset) {
			DrawObject(asset);
		}
		EditorGUI.indentLevel--;
	}
	private void DrawObject(Object obj) {
		int instanceID = obj.GetInstanceID();
		m_Folds[instanceID] = EditorGUILayout.InspectorTitlebar(GetFold(instanceID), obj);
		if (m_Folds[instanceID]) {
			AssetImporter assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(obj));
			// LabelField("Instance ID", obj.GetInstanceID().ToString());
			LabelField("File ID", GetFileId(obj).ToString());
			EnumPopup(assetImporter, "Hide Flags", obj.hideFlags, newHideFlags => obj.hideFlags = (HideFlags)newHideFlags);
			TextField(assetImporter, "Name", obj.name, newName => obj.name = newName);
			LabelField("Is Main Asset", AssetDatabase.IsMainAsset(obj).ToString());
			// SerializedObject serializedObject = new SerializedObject(obj);
			// PropertyField(assetImporter, serializedObject, "m_PrefabParentObject");
			// PropertyField(assetImporter, serializedObject, "m_PrefabInternal");
			DrawObjectEditButtons(obj);
		}
	}
	private long GetFileId(Object obj) {
		SerializedObject serializedObject = new SerializedObject(obj);
		PropertyInfo inspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
		inspectorModeInfo.SetValue(serializedObject, InspectorMode.Debug, null);
		SerializedProperty localIdProp = serializedObject.FindProperty("m_LocalIdentfierInFile");
		return localIdProp.longValue;
	}
	private bool GetFold(int instanceID) {
		if (!m_Folds.ContainsKey(instanceID)) {
			m_Folds[instanceID] = Selection.objects.Any(o => o.GetInstanceID() == instanceID);
		}
		return m_Folds[instanceID];
	}
	private void DrawObjectEditButtons(Object obj) {
		AssetImporter assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(obj));

		EditorGUILayout.BeginHorizontal();
		GUILayout.Space(EditorGUI.indentLevel * 20);
		DrawButton(true, "Copy Asset", () => {
			m_CopyingObj = null;
			object temp = System.Activator.CreateInstance(obj.GetType());
			if (temp != null) {
				Object unityObj = temp as Object;
				if (unityObj != null) {
					EditorUtility.CopySerialized(obj, unityObj);
					m_CopyingObj = unityObj;
				}
				else
					Debug.Log(obj.GetType() + " is not a Unity Object.");
			}
			else
				Debug.Log(m_CopyingObj.GetType() + " can not Instance.");
		});
		DrawButton(m_CopyingObj != null && m_CopyingObj.GetType() == obj.GetType(), "Paste Asset Values", () => {
			if (EditorUtility.DisplayDialog("Paste selected asset?", "   " + AssetDatabase.GetAssetPath(obj) + "\n    - " + obj.name + " (" + obj.GetType().Name + ")" + "\n\nYou cannot undo this action.", "Paste", "Cancel")) {
				EditorUtility.CopySerialized(m_CopyingObj, obj);
				EditorUtility.SetDirty(assetImporter);
				assetImporter.SaveAndReimport();
			}
		});
		DrawButton(!AssetDatabase.IsMainAsset(obj), "Delete", () => {
			DeleteObject(obj);
		});
		EditorGUILayout.EndHorizontal();
	}
	private void SetMainObject(Object obj) {
		string assetPath = AssetDatabase.GetAssetPath(obj);
		AssetImporter assetImporter = AssetImporter.GetAtPath(assetPath);
		string name = obj.name;
		string newPath = System.IO.Path.GetDirectoryName(assetPath) + System.IO.Path.AltDirectorySeparatorChar + name + System.IO.Path.GetExtension(assetPath);
		string validate = AssetDatabase.ValidateMoveAsset(assetPath, newPath);
		if (string.IsNullOrEmpty(validate) || assetPath == newPath) {
			AssetDatabase.SetMainObject(obj, AssetDatabase.GetAssetPath(obj));
			EditorUtility.SetDirty(assetImporter);
			assetImporter.SaveAndReimport();
			AssetDatabase.MoveAsset(assetPath, newPath);
			assetPath = newPath;
		}
		else {
			Debug.Log(validate);
		}
	}
	private void DeleteObject(Object obj) {
		if (EditorUtility.DisplayDialog("Delete selected asset?", "   " + AssetDatabase.GetAssetPath(obj) + "\n    - " + obj.name + "\n\nYou cannot undo this action.", "Delete", "Cancel")) {
			string assetPath = AssetDatabase.GetAssetPath(obj);
			AssetImporter assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(obj));
			Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
			Object.DestroyImmediate(obj, true);
			EditorUtility.SetDirty(assetImporter);
			assetImporter.SaveAndReimport();
			Selection.activeObject = mainAsset;
		}
	}
	private void LabelField(string key, string val) {
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.PrefixLabel(key);
		int indentLevel = EditorGUI.indentLevel;
		EditorGUI.indentLevel = 0;
		EditorGUILayout.SelectableLabel(val, GUILayout.Height(16));
		EditorGUI.indentLevel = indentLevel;
		EditorGUILayout.EndHorizontal();
	}
	private void TextField(AssetImporter assetImporter, string key, string val, System.Action<string> setter) {
		string newVal = EditorGUILayout.DelayedTextField(key, val);
		if (newVal != val) {
			setter(newVal);
			EditorUtility.SetDirty(assetImporter);
			assetImporter.SaveAndReimport();
		}
	}
	// private void PropertyField(AssetImporter assetImporter, SerializedObject serializedObject, string propertyPath) {
	// 	var property = serializedObject.FindProperty(propertyPath);
	// 	switch (property.propertyType) {
	// 		case SerializedPropertyType.String:
	// 			EditorGUILayout.DelayedTextField(property);
	// 			break;
	// 		case SerializedPropertyType.ObjectReference:
	// 			EditorGUILayout.ObjectField(property);
	// 			break;
	// 		default:
	// 			Debug.Log("Type " + property.propertyType + " not support.");
	// 			return;
	// 	}
	// 	if (serializedObject.ApplyModifiedProperties()) {
	// 		assetImporter.SaveAndReimport();
	// 	}
	// }
	private void TextArea(AssetImporter assetImporter, string key, string val, System.Action<string> setter) {
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.PrefixLabel(key);
		int indentLevel = EditorGUI.indentLevel;
		EditorGUI.indentLevel = 0;
		string newVal = EditorGUILayout.TextArea(val);
		if (newVal != val) {
			setter(newVal);
		}
		EditorGUI.indentLevel = indentLevel;
		EditorGUILayout.EndHorizontal();
	}
	private void EnumPopup(AssetImporter assetImporter, string key, System.Enum val, System.Action<System.Enum> setter) {
		System.Enum newVal = EditorGUILayout.EnumPopup(key, val);
		if (newVal.ToString() != val.ToString()) {
			setter(newVal);
			EditorUtility.SetDirty(assetImporter);
			assetImporter.SaveAndReimport();
		}
	}
	private void DrawButton(bool enable, string name, System.Action onClick) {
		GUI.enabled = enable;
		if (GUILayout.Button(name)) {
			onClick();
		}
		GUI.enabled = true;
	}
}
