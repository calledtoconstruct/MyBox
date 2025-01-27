#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MyBox.EditorTools
{
	public class ReorderableCollection
	{
		public bool IsExpanded
		{
			get { return _property.isExpanded; }
			set { _property.isExpanded = value; }
		}

		public void Draw()
		{
			var headerRect = EditorGUILayout.GetControlRect();

			DrawHeader(headerRect);

			if (_property.isExpanded)
			{
				_list.DoLayoutList();
			}
		}

		public void Draw(Rect rect)
		{
			DrawHeader(rect);
			rect.y += EditorGUIUtility.singleLineHeight;
			rect.y += EditorGUIUtility.standardVerticalSpacing;
			if (_property.isExpanded) _list.DoList(rect);
		}

		public float Height
		{
			get {
				return _property.isExpanded
					? _list.GetHeight() + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing
					: EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 12;
			}
		}

		public SerializedProperty Property
		{
			get { return _property; }
		}

		public Action<SerializedProperty, Rect, int> CustomDrawer;

		/// <summary>
		/// Return Height, Receive element index.
		/// Use EditorApplication.delayCall to perform custom logic
		/// </summary>
		public Func<int, int> CustomDrawerHeight;

		/// <summary>
		/// Return false to perform default logic, Receive element index.
		/// Use EditorApplication.delayCall to perform custom logic.
		/// </summary>
		public Func<int, bool> CustomAdd;

		/// <summary>
		/// Return false to perform default logic, Receive element index.
		/// Use EditorApplication.delayCall to perform custom logic.
		/// </summary>
		public Func<int, bool> CustomRemove;

		public Action<SerializedProperty> CustomHeader;

		public Action<Rect, int> CustomElementHeader;

		private ReorderableList _list;
		private SerializedProperty _property;
		private readonly string _customHeader; 

		public ReorderableCollection(
			SerializedProperty property,
			bool withAddButton = true,
			bool withRemoveButton = true,
			string customHeader = null)
		{
			_property = property;
			_property.isExpanded = true;
			_customHeader = customHeader;

			// For legacy support
			CustomElementHeader = (rect, index) => {
				var element = _property.GetArrayElementAtIndex(index);

				if (element.propertyType == SerializedPropertyType.Generic)
				{
					rect.x += 12;
					rect.width -= 14;
					var genericsLabel = rect;
					genericsLabel.height = EditorGUIUtility.singleLineHeight;
					
					EditorGUI.LabelField(genericsLabel, element.displayName);
				}
			};
			
			CreateList(property, withAddButton, withRemoveButton, false);
		}

		~ReorderableCollection()
		{
			_property = null;
			_list = null;
		}

		private void DrawHeader(Rect rect)
		{
			CustomElementHeader = CustomElementHeader ?? ((Rect _rect, int _index) => {});

			var headerRect = new Rect(rect);
			headerRect.height = EditorGUIUtility.singleLineHeight;
			headerRect.height += EditorGUIUtility.standardVerticalSpacing;
			
			ReorderableList.defaultBehaviours.DrawHeaderBackground(headerRect);
			
			headerRect.width -= 50;
			headerRect.x += 6;
			headerRect.y += 2;
			headerRect.y -= EditorGUIUtility.standardVerticalSpacing;

			var headerText = _customHeader != null ? _customHeader : _property.displayName;

			_property.isExpanded = EditorGUI.ToggleLeft(
				headerRect,
				headerText + "[" + _property.arraySize + "]",
				_property.isExpanded,
				EditorStyles.boldLabel);

			rect.y += headerRect.height;
		}

		private void CreateList(SerializedProperty property, bool withAddButton, bool withRemoveButton, bool displayHeader)
		{
			_list = new ReorderableList(
				property.serializedObject,
				property,
				true,
				displayHeader,
				withAddButton,
				withRemoveButton);

			_list.onChangedCallback += list => Apply();
			_list.onAddCallback += AddElement;
			_list.onRemoveCallback += RemoveElement;
			_list.onCanRemoveCallback += (list) => _list.count > 0;
			_list.drawElementCallback += DrawElement;
			_list.elementHeightCallback += GetElementHeight;
		}

		private void AddElement(ReorderableList list)
		{
			if (CustomAdd == null || !CustomAdd(_property.arraySize))
				ReorderableList.defaultBehaviours.DoAddButton(list);
		}

		private void RemoveElement(ReorderableList list)
		{
			if (CustomRemove == null || !CustomRemove(list.index))
				ReorderableList.defaultBehaviours.DoRemoveButton(list);
		}

		private void DrawElement(Rect rect, int index, bool active, bool focused)
		{
			EditorGUI.BeginChangeCheck();

			var property = _property.GetArrayElementAtIndex(index);
			rect.height = GetElementHeight(index);
			rect.y += 1;

			if (CustomDrawer != null) CustomDrawer(property, rect, index);
			else
			{
				CustomElementHeader(rect, index);
				EditorGUI.PropertyField(rect, property, GUIContent.none, true);
			}

			_list.elementHeight = rect.height + 4.0f;
			if (EditorGUI.EndChangeCheck()) Apply();
		}

		private float GetElementHeight(int index)
		{
			if (CustomDrawerHeight != null) return CustomDrawerHeight(index);

			try {
				var element = _property.GetArrayElementAtIndex(index);
				var height = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
				return Mathf.Max(EditorGUIUtility.singleLineHeight, height + 4.0f);
			} catch (Exception exception) {
				return EditorGUIUtility.singleLineHeight;
			}
		}

		private void Apply()
		{
			_property.serializedObject.ApplyModifiedProperties();
		}
	}
}
#endif
