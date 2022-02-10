﻿/* Copyright (c) 2020 Lyuma <xn.lyuma@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */

//#define DISABLE_GESTURE_MANAGER
# if !DISABLE_GESTURE_MANAGER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using GestureManager.Scripts.Editor.Modules.Vrc3;

[CustomEditor(typeof(GestureManagerAv3Menu))]
public class GestureManagerAv3MenuEditor : Editor
{
    private readonly Dictionary<Texture2D, Texture2D> _resizedIcons = new Dictionary<Texture2D, Texture2D>();
    private VRCExpressionsMenu _currentMenu;

    class StubAv3Module : GestureManager.Scripts.Editor.Modules.Vrc3.ModuleVrc3 {
        class VelocityParam : GestureManager.Scripts.Editor.Modules.Vrc3.Params.Vrc3Param {
            public LyumaAv3Runtime runtime;
            public int axis;
            public VelocityParam(string name, LyumaAv3Runtime runtime, int axis, float amplifier=1) : base(name, AnimatorControllerParameterType.Float, amplifier) {
                this.axis = axis;
                this.runtime = runtime;
            }
            public override float Get() {
                Vector3 vel = runtime.Velocity;
                return vel[axis];
            }
            protected internal override void InternalSet(float value) {
                Vector3 vel = runtime.Velocity;
                vel[axis] = value;
                runtime.Velocity = vel;
            }
        }
        class ReflectedVrcParam : GestureManager.Scripts.Editor.Modules.Vrc3.Params.Vrc3Param {
            public System.Reflection.FieldInfo property;
            public LyumaAv3Runtime runtime;
            public ReflectedVrcParam(string name, LyumaAv3Runtime runtime, System.Reflection.FieldInfo property, float amplifier=1) : base(name,
                    property.FieldType == typeof(bool) ? AnimatorControllerParameterType.Bool : (property.FieldType == typeof(float) ? AnimatorControllerParameterType.Float : AnimatorControllerParameterType.Int),
                    amplifier) {
                this.property = property;
                this.runtime = runtime;
            }
            public override float Get() {
                if (property.FieldType == typeof(bool)) {
                    return ((bool)property.GetValue(runtime)) ? 1.0f : 0.0f;
                } else if (property.FieldType == typeof(float)) {
                    return (float)property.GetValue(runtime);
                } else if (property.FieldType == typeof(int)) {
                    return (int)property.GetValue(runtime);
                } else {
                    return (float)(int)Convert.ChangeType(property.GetValue(runtime), typeof(int));
                }
            }
            protected internal override void InternalSet(float value) {
                // UnityEngine.Debug.Log("Internal set bool " + param.name + " to " + value + " was " + param.value + "(" + param.lastValue + ")");
                if (property.FieldType == typeof(bool)) {
                    property.SetValue(runtime, value > 0.5f);
                } else if (property.FieldType == typeof(float)) {
                    property.SetValue(runtime, value);
                } else if (property.FieldType == typeof(int)) {
                    property.SetValue(runtime, (int)value);
                } else {
                    property.SetValue(runtime, Convert.ChangeType((int)value, Enum.GetUnderlyingType(property.FieldType)));
                }
            }
        }
        class StubVrcBoolParam : GestureManager.Scripts.Editor.Modules.Vrc3.Params.Vrc3Param {
            public LyumaAv3Runtime.BoolParam param;
            public StubVrcBoolParam(string name, LyumaAv3Runtime.BoolParam param, float amplifier=1) : base(name, AnimatorControllerParameterType.Bool, amplifier) { this.param = param; }
            public override float Get() {
                return param.value ? 1.0f : 0.0f;
            }
            protected internal override void InternalSet(float value) {
                UnityEngine.Debug.Log("Internal set bool " + param.name + " to " + value + " was " + param.value + "(" + param.lastValue + ")");
                param.value = value > 0.5f ? true: false;
            }
        }
        class StubVrcIntParam : GestureManager.Scripts.Editor.Modules.Vrc3.Params.Vrc3Param {
            public LyumaAv3Runtime.IntParam param;
            public StubVrcIntParam(string name, LyumaAv3Runtime.IntParam param, float amplifier=1) : base(name, AnimatorControllerParameterType.Int, amplifier) { this.param = param; }
            public override float Get() {
                return param.value;
            }
            protected internal override void InternalSet(float value) {
                UnityEngine.Debug.Log("Internal set int " + param.name + " to " + value + " was " + param.value + "(" + param.lastValue + ")");
                param.value = (int)value;
            }
        }
        class StubVrcFloatParam : GestureManager.Scripts.Editor.Modules.Vrc3.Params.Vrc3Param {
            public LyumaAv3Runtime.FloatParam param;
            public StubVrcFloatParam(string name, LyumaAv3Runtime.FloatParam param, float amplifier=1) : base(name, AnimatorControllerParameterType.Float, amplifier) { this.param = param; }
            public override float Get() {
                return param.value;
            }
            protected internal override void InternalSet(float value) {
                param.value = value;
            }
        }
        private LyumaAv3Runtime _runtime;
        public StubAv3Module(LyumaAv3Runtime runtime, VRCAvatarDescriptor avatarDescriptor) : base(null, avatarDescriptor)
        {
            _runtime = runtime;
        }

        public override void Update()
        {
            UnityEngine.Debug.Log("Updating " + Params.Keys);
            // if (_dummyMode != DummyMode.None && (!DummyAvatar || Avatar.activeSelf)) DisableDummy();
            // foreach (var weightController in _weightControllers) weightController.Update();
            foreach (var param in Params.Values) {
                if (param is StubVrcBoolParam) {
                    StubVrcBoolParam bparam = (StubVrcBoolParam) param;
                    if (bparam.param.lastValue != bparam.param.value) {
                        bparam.Set(RadialMenus.Values, bparam.param.value ? 1.0f : 0.0f);
                    }
                }
                if (param is StubVrcIntParam) {
                    StubVrcIntParam iparam = (StubVrcIntParam) param;
                    if (iparam.param.lastValue != iparam.param.value) {
                        iparam.Set(RadialMenus.Values, iparam.param.value);
                    }
                }
                if (param is StubVrcFloatParam) {
                    StubVrcFloatParam fparam = (StubVrcFloatParam) param;
                    if (fparam.param.lastValue != fparam.param.value) {
                        fparam.Set(RadialMenus.Values, fparam.param.value);
                    }
                }
            }
        }

        public override void InitForAvatar()
        {
            if (Params.Count > 0) {
                _runtime.ResetAvatar = true;
                return;
            }
            Params.Clear();
            foreach (var builtinprop in LyumaAv3Runtime.BUILTIN_PARAMETERS) {
                if (builtinprop == "VelocityX") {
                    Params.Add(builtinprop, new VelocityParam(builtinprop, _runtime, 0, 8));
                } else if (builtinprop == "VelocityY") {
                    Params.Add(builtinprop, new VelocityParam(builtinprop, _runtime, 1, 8));
                } else if (builtinprop == "VelocityZ") {
                    Params.Add(builtinprop, new VelocityParam(builtinprop, _runtime, 2, 8));
                } else {
                    var property = _runtime.GetType().GetField(builtinprop, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Default);
                    if (property == null) {
                        UnityEngine.Debug.Log("Failed to find property " + builtinprop + " for " + _runtime);
                    } else {
                        Params.Add(builtinprop, new ReflectedVrcParam(builtinprop, _runtime, property));
                    }
                }
            }
            foreach (var xbool in _runtime.Bools) {
                Params.Add(xbool.name, new StubVrcBoolParam(xbool.name, xbool));
            }
            foreach (var xint in _runtime.Ints) {
                Params.Add(xint.name, new StubVrcIntParam(xint.name, xint));
            }
            foreach (var xfloat in _runtime.Floats) {
                Params.Add(xfloat.name, new StubVrcFloatParam(xfloat.name, xfloat));
            }
            UnityEngine.Debug.Log("Initing params " + Params.Keys);
        }

        // public override AnimationClip GetFinalGestureByIndex(GestureHand hand, int gestureIndex)
        // {
            // later
            // return ModuleVrc3Styles.Data.GestureClips[gestureIndex];
        // }

        // public void NoExpressionRefresh()
        // {
        //     if (Dummy.State) return;
        //     if (Menu) ResetAvatar();
        // }

        // public void ResetAvatar()
        // {
        //     _runtime.ResetAvatar = true;
        //     // InitForAvatar();
        // }

    }
    public override bool RequiresConstantRepaint() => false; //Manager.Module?.RequiresConstantRepaint ?? false;

    private VisualElement _root;

    private const int AntiAliasing = 4;


    StubAv3Module av3Module;
    public RadialMenu GetOrCreateRadial(UnityEditor.Editor editor)
    {
        if (av3Module.Params.Count == 0) {
            av3Module.InitForAvatar();
            Debug.Log(String.Join(",",av3Module.Params.Keys.ToArray()));
        }
        var menu = (GestureManagerAv3Menu)target;
        if (av3Module.RadialMenus.ContainsKey(editor)) return av3Module.RadialMenus[editor];

        av3Module.RadialMenus[editor] = new RadialMenu(av3Module);
        av3Module.RadialMenus[editor].Set(menu.RootMenu);
        return av3Module.RadialMenus[editor];
    }
    public override VisualElement CreateInspectorGUI()
    {

        UnityEngine.Debug.Log("Create Inspector GUI");
        VRCAvatarDescriptor avadesc = ((Component)target).GetComponent<VRCAvatarDescriptor>();
        av3Module = new StubAv3Module(((Component)target).GetComponent<LyumaAv3Runtime>(), avadesc);
        _root = new VisualElement();
        _root.Add(new IMGUIContainer(ManagerGui));
        foreach (var inspectorWindow in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(window => window.titleContent.text == "Inspector")) {
            if (!inspectorWindow || inspectorWindow.GetAntiAliasing() == AntiAliasing) continue;

            inspectorWindow.SetAntiAliasing(AntiAliasing);
            // Dumb workaround method to trigger the internal MakeParentsSettingsMatchMe() method on the EditorWindow.
            inspectorWindow.minSize = inspectorWindow.minSize;
        }
        return _root;
    }

    private static GUIStyle _guiHandTitle = null;
    internal static GUIStyle GuiHandTitle => _guiHandTitle ?? (_guiHandTitle = new GUIStyle(GUI.skin.label)
    {
        fontSize = 12,
        fontStyle = FontStyle.Bold,
        alignment = TextAnchor.UpperCenter,
        padding = new RectOffset(10, 10, 10, 10)
    });

    private void ManagerGui () {
        var gmenu = (GestureManagerAv3Menu)target;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(gmenu.IsMenuOpen ? "Close menu" : "Open menu"))
        {
            gmenu.ToggleMenu();
        }

        if (gmenu.gameObject.GetComponents<GestureManagerAv3Menu>().Length == 1)
        {
            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                OpenMenuForTwoHandedSupport(gmenu);
            }
        }

        GUILayout.EndHorizontal();

        var menu = GetOrCreateRadial(this as UnityEditor.Editor);
        if (!gmenu.IsMenuOpen) {
            GUILayout.Label(
                "Expressions" +
                (gmenu.IsMenuOpen ? "" : " [Menu is closed]"),
                EditorStyles.boldLabel);
            menu.style.display = DisplayStyle.None;
            return;
        }

        GUILayout.Space(4);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        GUILayout.Label("Radial Menu", GuiHandTitle);

        GUILayout.Label("", GUILayout.ExpandWidth(true), GUILayout.Height(RadialMenu.Size));
        if (gmenu.IsMenuOpen) {
            if (!(Event.current.type == EventType.Layout || Event.current.type == EventType.Used)) {
                menu.Rect = GUILayoutUtility.GetLastRect();
            }
            float extraSize = menu.Render(_root, menu.Rect) - RadialMenu.Size;
            menu.style.display = DisplayStyle.Flex;
            if (extraSize > 0) GUILayout.Label("", GUILayout.ExpandWidth(true), GUILayout.Height(extraSize));
        }
    }

    private static void OpenMenuForTwoHandedSupport(GestureManagerAv3Menu menu)
    {
        var mainMenu = menu.Runtime.gameObject.AddComponent<GestureManagerAv3Menu>();
        mainMenu.Runtime = menu.Runtime;
        mainMenu.RootMenu = menu.RootMenu;
    }

}
#endif
