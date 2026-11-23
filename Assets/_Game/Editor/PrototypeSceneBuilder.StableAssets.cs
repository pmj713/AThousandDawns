using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 빌드할 때마다 같은 결과 (git에 매번 바뀐 파일이 쌓이지 않게): 애니메이터 컨트롤러를 지우고 새로 만들면
    //   GUID와 상태·전이의 내부 fileID가 매번 새로 뽑혀서, 내용이 같아도 .controller와 그걸 쓰는 _Ink.prefab이 바뀌었어요.
    //   그래서 옆 임시 경로에 새로 만든 뒤 지금 것과 내용(상태·동작·빠르기·전이·조건·매개변수)을 견줘서
    //   같으면 지금 것을 그대로 두고, 다르면 지금 파일 자리에 덮어써요 (GUID는 meta가 그대로라 유지)
    //   새로 만든 오브젝트로 저장하는 프리팹(방 구성 모델·보상·이벤트)도 저장할 때마다 fileID가 새로 뽑혀서,
    //   지금 프리팹과 내용(계층·이름·컴포넌트 값·참조)이 같으면 저장하지 않아요
    public static partial class PrototypeSceneBuilder
    {
        static AnimatorController BeginController(string path)
        {
            string temp = BuildTempPath(path);
            AssetDatabase.DeleteAsset(temp);
            return AnimatorController.CreateAnimatorControllerAtPath(temp);
        }

        static AnimatorController FinishController(string path, AnimatorController built)
        {
            string temp = AssetDatabase.GetAssetPath(built);
            if (temp == path) return built;
            AssetDatabase.SaveAssets();
            var current = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (current != null && Describe(current) == Describe(built))
            {
                AssetDatabase.DeleteAsset(temp);
                return current;
            }
            if (current == null)
            {
                AssetDatabase.MoveAsset(temp, path);
                return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            }
            File.Copy(Path.GetFullPath(temp), Path.GetFullPath(path), true);
            AssetDatabase.DeleteAsset(temp);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        static string BuildTempPath(string path) => path.Substring(0, path.Length - ".controller".Length) + "_Build.controller";

        static string Describe(AnimatorController c)
        {
            var sb = new StringBuilder();
            foreach (var p in c.parameters)
                sb.Append($"P {p.name} {p.type} {p.defaultFloat:R} {p.defaultInt} {p.defaultBool}\n");
            foreach (var layer in c.layers)
            {
                sb.Append($"L {layer.name} {layer.defaultWeight:R} {layer.blendingMode} {Ref(layer.avatarMask)}\n");
                var sm = layer.stateMachine;
                sb.Append($"D {(sm.defaultState != null ? sm.defaultState.name : "-")}\n");
                foreach (var t in sm.anyStateTransitions) sb.Append("A ").Append(Describe(t)).Append('\n');
                foreach (var cs in sm.states.OrderBy(s => s.state.name, System.StringComparer.Ordinal))
                {
                    var s = cs.state;
                    sb.Append($"S {s.name} {Ref(s.motion)} {s.speed:R} {s.speedParameterActive} {s.speedParameter} {s.tag} {s.writeDefaultValues} {s.mirror} {s.cycleOffset:R}\n");
                    foreach (var t in s.transitions) sb.Append("  T ").Append(Describe(t)).Append('\n');
                }
                if (sm.stateMachines.Length > 0) sb.Append($"M {sm.stateMachines.Length}\n");
            }
            return sb.ToString().Replace(AssetDatabase.GetAssetPath(c) + ":", "self:");   // 컨트롤러 안에 든 하위 에셋(윗몸 마스크 등)은 임시 경로와 지금 경로가 달라요
        }

        static string Describe(AnimatorStateTransition t)
        {
            var sb = new StringBuilder();
            sb.Append($"{(t.destinationState != null ? t.destinationState.name : t.isExit ? "exit" : "-")} {t.hasExitTime} {t.exitTime:R} {t.duration:R} {t.hasFixedDuration} {t.offset:R} {t.canTransitionToSelf} {t.interruptionSource}");
            foreach (var k in t.conditions) sb.Append($" [{k.mode} {k.parameter} {k.threshold:R}]");
            return sb.ToString();
        }

        static GameObject SaveStablePrefab(GameObject root, string path)
        {
            var current = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (current != null && DescribeTree(current) == DescribeTree(root)) return current;
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        static readonly HashSet<string> SkipProps = new HashSet<string> { "m_PrefabInstance", "m_CorrespondingSourceObject", "m_PrefabAsset", "m_ObjectHideFlags" };

        static string DescribeTree(GameObject root)
        {
            var paths = new Dictionary<Object, string>();
            var order = new List<(GameObject go, string path)>();
            void Index(Transform t, string path)
            {
                paths[t.gameObject] = path;
                order.Add((t.gameObject, path));
                var comps = t.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                    if (comps[i] != null) paths[comps[i]] = $"{path}#{i}";
                for (int i = 0; i < t.childCount; i++) Index(t.GetChild(i), $"{path}/{i}");
            }
            Index(root.transform, "");
            var sb = new StringBuilder();
            foreach (var (go, path) in order)
            {
                sb.Append($"G {path} {(go == root ? "(root)" : go.name)} {go.activeSelf} {go.layer} {go.tag} {go.isStatic}\n");
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) { sb.Append(" null\n"); continue; }
                    sb.Append($" C {comp.GetType().FullName}\n");
                    var it = new SerializedObject(comp).GetIterator();
                    bool enter = true;
                    while (it.Next(enter))
                    {
                        enter = !SkipProps.Contains(it.name) && it.propertyType != SerializedPropertyType.ObjectReference;   // 참조는 아래 값 하나로 (속의 fileID는 저장마다 달라요)
                        if (SkipProps.Contains(it.name)) continue;
                        string v = it.propertyType switch
                        {
                            SerializedPropertyType.ObjectReference => it.objectReferenceValue == null ? "-" : paths.TryGetValue(it.objectReferenceValue, out var p) ? "@" + p : Ref(it.objectReferenceValue),
                            SerializedPropertyType.Integer or SerializedPropertyType.LayerMask or SerializedPropertyType.ArraySize or SerializedPropertyType.Character => it.longValue.ToString(),
                            SerializedPropertyType.Boolean => it.boolValue.ToString(),
                            SerializedPropertyType.Float => it.doubleValue.ToString("R"),
                            SerializedPropertyType.String => it.stringValue,
                            SerializedPropertyType.Enum => it.intValue.ToString(),
                            SerializedPropertyType.Color => it.colorValue.ToString("R"),
                            SerializedPropertyType.Vector2 => it.vector2Value.ToString("R"),
                            SerializedPropertyType.Vector3 => it.vector3Value.ToString("R"),
                            SerializedPropertyType.Vector4 => it.vector4Value.ToString("R"),
                            SerializedPropertyType.Quaternion => it.quaternionValue.ToString("R"),
                            SerializedPropertyType.Rect => it.rectValue.ToString("R"),
                            SerializedPropertyType.Bounds => it.boundsValue.ToString("R"),
                            _ => null,
                        };
                        if (v != null) sb.Append($"  {it.propertyPath}={v}\n");
                    }
                }
            }
            return sb.ToString();
        }

        static string Ref(Object o) => o == null ? "-" : $"{AssetDatabase.GetAssetPath(o)}:{o.name}";
    }
}
