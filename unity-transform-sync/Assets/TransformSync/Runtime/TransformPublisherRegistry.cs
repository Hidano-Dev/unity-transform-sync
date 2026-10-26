using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hidano.TransformSync.Runtime
{
    /// <summary>
    /// ラベルから <see cref="TransformPublisher"/> を引くための実行時レジストリ。
    /// Publisher は有効化されている間だけ登録される。
    /// </summary>
    public static class TransformPublisherRegistry
    {
        private static readonly Dictionary<string, List<TransformPublisher>> Publishers =
            new Dictionary<string, List<TransformPublisher>>();

        /// <summary>
        /// 指定ラベルの登録状況が変化したときに発火する。
        /// Subscriber が後から有効化された Publisher へ遅延バインドするために使う。
        /// </summary>
        public static event Action<string> LabelChanged;

        /// <summary>現在登録されているラベルの一覧。</summary>
        public static IReadOnlyCollection<string> Labels => Publishers.Keys;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            // Domain Reload を無効化した Enter Play Mode でも静的状態を持ち越さない。
            Publishers.Clear();
            LabelChanged = null;
        }

        /// <summary>ラベルに対応する Publisher を取得する。重複時は先に登録されたものを返す。</summary>
        public static bool TryGet(string label, out TransformPublisher publisher)
        {
            publisher = null;
            if (string.IsNullOrEmpty(label)) return false;
            if (!Publishers.TryGetValue(label, out var list) || list.Count == 0) return false;

            publisher = list[0];
            return publisher != null;
        }

        internal static void Register(TransformPublisher publisher)
        {
            if (publisher == null) return;

            var label = publisher.Label;
            if (string.IsNullOrEmpty(label))
            {
                Debug.LogWarning($"[{nameof(TransformPublisher)}] Label が空のため登録できません。", publisher);
                return;
            }

            if (!Publishers.TryGetValue(label, out var list))
            {
                list = new List<TransformPublisher>();
                Publishers.Add(label, list);
            }

            if (list.Contains(publisher)) return;

            if (list.Count > 0)
            {
                Debug.LogWarning(
                    $"[{nameof(TransformPublisher)}] ラベル '{label}' が重複しています。" +
                    $"'{list[0].name}' が優先され、'{publisher.name}' は待機します。", publisher);
            }

            list.Add(publisher);
            LabelChanged?.Invoke(label);
        }

        internal static void Unregister(string label, TransformPublisher publisher)
        {
            if (string.IsNullOrEmpty(label)) return;
            if (!Publishers.TryGetValue(label, out var list)) return;
            if (!list.Remove(publisher)) return;

            if (list.Count == 0) Publishers.Remove(label);
            LabelChanged?.Invoke(label);
        }
    }
}
