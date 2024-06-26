using System;
using System.Collections.Generic;
using GPUSkin;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
namespace GPUSkin
{
    public partial class GPUSkinCreatWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        [MenuItem("Window/UI Toolkit/GPUSkinCreatWindow")]
        public static void ShowExample()
        {
            GPUSkinCreatWindow wnd = GetWindow<GPUSkinCreatWindow>();
            wnd.maxSize = new Vector2(600f, 1080f);
            wnd.titleContent = new GUIContent("GPUSkinCreatWindow");
        }

        private EnumField FrameRateField;
        private Label SavePathLabel;
        private Button SavePathButton;
        private Button ExportButton;
        private ObjectField SourceObjectField;
        //private ObjectField AnimatorControllerField;
        private ListView listView;
        public List<AnimationClip> animationClips;
        private FrameRate frameRate => (FrameRate)FrameRateField.value;
        private string floderPath => SavePathLabel.text;
        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;
            //root.Q("");
            //// VisualElements objects can contain other VisualElement following a tree hierarchy.
            //VisualElement label = new Label("Hello World! From C#");
            //root.Add(label);

            //// Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);
            FrameRateField = labelFromUXML.Q("FrameRate") as EnumField;
            SavePathLabel = labelFromUXML.Q("PathLabel") as Label;
            SavePathButton = labelFromUXML.Q("Path") as Button;
            ExportButton = labelFromUXML.Q<Button>("Export");
            ExportButton.clicked += ExportButton_clicked;
            SavePathButton.clicked += SavePathButton_clicked;

            SourceObjectField = labelFromUXML.Q<ObjectField>("SourceObject_Field");
            //AnimatorControllerField = labelFromUXML.Q<ObjectField>("AnimatorController_Field");
            SavePathLabel.text = EditorPrefs.GetString(nameof(SavePathLabel), SavePathLabel.text);
            SerializedProperty serializedProperty = new SerializedObject(this).FindProperty(nameof(animationClips));
            listView = labelFromUXML.Q<ListView>("ListView");
            var add = labelFromUXML.Q<Button>("Add");
            var sub = labelFromUXML.Q<Button>("Sub");
            add.clicked += Add_onClick;
            sub.clicked += Sub_clicked;
        }

        private void Sub_clicked()
        {
            listView.hierarchy.RemoveAt(listView.hierarchy.childCount - 1);
        }

        private void Add_onClick()
        {
            var field = new ObjectField("AnimationClip-" + listView.hierarchy.childCount);
            field.objectType = typeof(AnimationClip);
            listView.hierarchy.Add(field);
        }

        private void SavePathButton_clicked()
        {
            var text = EditorUtility.OpenFolderPanel("select path", SavePathLabel.text, null);
            text = FileUtil.GetProjectRelativePath(text);
            SavePathLabel.text = text;
            EditorPrefs.SetString(nameof(SavePathLabel), SavePathLabel.text);
            
        }

        private void ExportButton_clicked()
        {
            Export();
        }
    }
}