using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace MyBox
{
    /// <summary>
    /// Conditionally Show/Hide field in inspector, based on some other field value
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class MultiConditionalFieldAttribute : PropertyAttribute
    {
        public readonly string[] FieldsToCheck;
        public readonly string[] CompareValues;
        public readonly bool Inverse;
        public readonly bool ClearOtherwise;

        /// <param name="fieldsToCheck">String name of fields to check value</param>
        /// <param name="inverse">Inverse check result</param>
        /// <param name="compareValues">On which values field will be shown in inspector</param>
        public MultiConditionalFieldAttribute(string fieldToCheck, bool inverse = false, bool clearOtherwise = false, params object[] compareValues)
        {
            FieldsToCheck = new[] { fieldToCheck };
            Inverse = inverse;
            ClearOtherwise = clearOtherwise;
            CompareValues = compareValues.Select(c => c.ToString().ToUpper()).ToArray();
        }
        public MultiConditionalFieldAttribute(string firstFieldToCheck, string secondFieldToCheck, bool inverse = false, bool clearOtherwise = false, params object[] compareValues)
        {
            FieldsToCheck = new[] { firstFieldToCheck, secondFieldToCheck };
            Inverse = inverse;
            ClearOtherwise = clearOtherwise;
            CompareValues = compareValues.Select(c => c.ToString().ToUpper()).ToArray();
        }
        public MultiConditionalFieldAttribute(string firstFieldToCheck, string secondFieldToCheck, string thirdFieldToCheck, bool inverse = false, bool clearOtherwise = false, params object[] compareValues)
        {
            FieldsToCheck = new[] { firstFieldToCheck, secondFieldToCheck, thirdFieldToCheck };
            Inverse = inverse;
            ClearOtherwise = clearOtherwise;
            CompareValues = compareValues.Select(c => c.ToString().ToUpper()).ToArray();
        }
        public MultiConditionalFieldAttribute(string[] fieldsToCheck, bool inverse = false, bool clearOtherwise = false, params object[] compareValues)
        {
            FieldsToCheck = fieldsToCheck;
            Inverse = inverse;
            ClearOtherwise = clearOtherwise;
            CompareValues = compareValues.Select(c => c.ToString().ToUpper()).ToArray();
        }
    }
}

#if UNITY_EDITOR
namespace MyBox.Internal
{
	using UnityEditor;
	using EditorTools;

	[CustomPropertyDrawer(typeof(MultiConditionalFieldAttribute))]
	public class MultiConditionalFieldAttributeDrawer : PropertyDrawer
	{
		private bool _toShow = false;

		/// <summary>
		/// Key is Associated with drawer type (the T in [CustomPropertyDrawer(typeof(T))])
		/// Value is PropertyDrawer Type
		/// </summary>
		private static Dictionary<Type, Type> _allPropertyDrawersInDomain;

		private bool _initialized;
		private PropertyDrawer _customAttributeDrawer;
		private PropertyDrawer _customTypeDrawer;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (!(attribute is MultiConditionalFieldAttribute conditional)) return 0;

			Initialize(property);

            _toShow = conditional.Inverse;

            foreach (var fieldToCheck in conditional.FieldsToCheck)
            {
                if (_toShow == conditional.Inverse)
                {
                    var propertyToCheck = ConditionalFieldUtility.FindRelativeProperty(property, fieldToCheck);
                    _toShow = ConditionalFieldUtility.PropertyIsVisible(propertyToCheck, conditional.Inverse, conditional.CompareValues);
                }
            }

			if (!_toShow)
            {
                if (conditional.ClearOtherwise) ConditionalFieldUtility.Clear(property);
                return 0;
            }

			if (_customAttributeDrawer != null) return _customAttributeDrawer.GetPropertyHeight(property, label);
			if (_customTypeDrawer != null) return _customTypeDrawer.GetPropertyHeight(property, label);

			return EditorGUI.GetPropertyHeight(property);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (!_toShow) return;

			if (_customAttributeDrawer != null) TryUseAttributeDrawer();
			else if (_customTypeDrawer != null) TryUseTypeDrawer();
			else EditorGUI.PropertyField(position, property, label, true);

			void TryUseAttributeDrawer()
			{
				try
				{
					_customAttributeDrawer.OnGUI(position, property, label);
				}
				catch (Exception e)
				{
					EditorGUI.PropertyField(position, property, label);
					LogWarning("Unable to use Custom Attribute Drawer " + _customAttributeDrawer.GetType() + " : " + e, property);
				}
			}

			void TryUseTypeDrawer()
			{
				try
				{
					_customTypeDrawer.OnGUI(position, property, label);
				}
				catch (Exception e)
				{
					EditorGUI.PropertyField(position, property, label);
					LogWarning("Unable to instantiate " + fieldInfo.FieldType + " : " + e, property);
				}
			}
		}

		private void Initialize(SerializedProperty property)
		{
			if (_initialized) return;

			CacheAllDrawersInDomain();

			TryGetCustomAttributeDrawer();
			TryGetCustomTypeDrawer();

			_initialized = true;

			void CacheAllDrawersInDomain()
			{
				if (!_allPropertyDrawersInDomain.IsNullOrEmpty()) return;

				_allPropertyDrawersInDomain = new Dictionary<Type, Type>();
				var propertyDrawerType = typeof(PropertyDrawer);

				var allDrawerTypesInDomain = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(x => x.GetTypes())
					.Where(t => propertyDrawerType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

				foreach (var type in allDrawerTypesInDomain)
				{
					var drawerAttribute = CustomAttributeData.GetCustomAttributes(type).FirstOrDefault();
					if (drawerAttribute == null) continue;
					var associatedType = drawerAttribute.ConstructorArguments.FirstOrDefault().Value as Type;
					if (associatedType == null) continue;

					if (_allPropertyDrawersInDomain.ContainsKey(associatedType)) continue;
					_allPropertyDrawersInDomain.Add(associatedType, type);
				}
			}

			void TryGetCustomAttributeDrawer()
			{
				if (fieldInfo == null) return;
				//Get the second attribute flag
				var secondAttribute = (PropertyAttribute) fieldInfo.GetCustomAttributes(typeof(PropertyAttribute), false)
					.FirstOrDefault(a => !(a is MultiConditionalFieldAttribute));
				if (secondAttribute == null) return;
				var genericAttributeType = secondAttribute.GetType();

				//Get the associated attribute drawer
				if (!_allPropertyDrawersInDomain.ContainsKey(genericAttributeType)) return;

				var customAttributeDrawerType = _allPropertyDrawersInDomain[genericAttributeType];
				var customAttributeData = fieldInfo.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType == secondAttribute.GetType());
				if (customAttributeData == null) return;


				//Create drawer for custom attribute
				try
				{
					_customAttributeDrawer = (PropertyDrawer) Activator.CreateInstance(customAttributeDrawerType);
					var attributeField = customAttributeDrawerType.GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic);
					if (attributeField != null) attributeField.SetValue(_customAttributeDrawer, secondAttribute);
				}
				catch (Exception e)
				{
					LogWarning("Unable to construct drawer for " + secondAttribute.GetType() + " : " + e, property);
				}
			}

			void TryGetCustomTypeDrawer()
			{
				if (fieldInfo == null) return;
				// Skip checks for mscorlib.dll
				if (fieldInfo.FieldType.Module.ScopeName.Equals(typeof(int).Module.ScopeName)) return;


				// Of all property drawers in the assembly we need to find one that affects target type
				// or one of the base types of target type
				Type fieldDrawerType = null;
				Type fieldType = fieldInfo.FieldType;
				while (fieldType != null)
				{
					if (_allPropertyDrawersInDomain.ContainsKey(fieldType))
					{
						fieldDrawerType = _allPropertyDrawersInDomain[fieldType];
						break;
					}

					fieldType = fieldType.BaseType;
				}

				if (fieldDrawerType == null) return;

				//Create instances of each (including the arguments)
				try
				{
					_customTypeDrawer = (PropertyDrawer) Activator.CreateInstance(fieldDrawerType);
				}
				catch (Exception e)
				{
					LogWarning("No constructor available in " + fieldType + " : " + e, property);
					return;
				}

				//Reassign the attribute field in the drawer so it can access the argument values
				var attributeField = fieldDrawerType.GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic);
				if (attributeField != null) attributeField.SetValue(_customTypeDrawer, attribute);
				var fieldInfoField = fieldDrawerType.GetField("m_FieldInfo", BindingFlags.Instance | BindingFlags.NonPublic);
				if (fieldInfoField != null) fieldInfoField.SetValue(_customTypeDrawer, fieldInfo);
			}
		}

		private void LogWarning(string log, SerializedProperty property)
		{
			var warning = "Property <color=brown>" + fieldInfo.Name + "</color>";
			if (fieldInfo != null && fieldInfo.DeclaringType != null)
				warning += " on behaviour <color=brown>" + fieldInfo.DeclaringType.Name + "</color>";
			warning += " caused: " + log;

			WarningsPool.LogWarning(warning, property.serializedObject.targetObject);
		}
	}
}
#endif