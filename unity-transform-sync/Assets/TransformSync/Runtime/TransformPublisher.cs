using System;
using UnityEngine;

namespace Hidano.TransformSync.Runtime
{
    /// <summary>
    /// キャラクタモデルのルートにアタッチし、Humanoid の主要な骨のグローバル姿勢を
    /// ラベル付きで公開する。値の更新タイミングで購読中の
    /// <see cref="TransformSubscriber"/> へ push する。
    /// Animator や HumanoidAvatar が取得できない場合は、手動で割り当てた骨
    /// （<see cref="ManualBones"/>）のうち null でないものだけを提供する。
    /// 手動の割り当てが 1 つも無い場合は、全ての骨として自身の Transform を
    /// 提供するフォールバックモードで動作する。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hidano/Transform Sync/Transform Publisher")]
    public sealed class TransformPublisher : MonoBehaviour
    {
        /// <summary>骨の姿勢をサンプリングして購読者へ反映するタイミング。</summary>
        public enum UpdateTiming
        {
            Update,
            LateUpdate,
            FixedUpdate,
            BeforeRender,
        }

        [SerializeField, Tooltip("キャラクタを識別するラベル。Subscriber からはこの名前で参照される。")]
        private string label;

        [SerializeField, Tooltip("HumanoidAvatar を持つ Animator。未設定の場合は自身の Transform を全ての骨として提供する。")]
        private Animator animator;

        [SerializeField, Tooltip("骨の姿勢をサンプリングして購読者へ反映するタイミング。")]
        private UpdateTiming timing = UpdateTiming.LateUpdate;

        [SerializeField, Tooltip("Animator が Humanoid でない場合に使う骨の手動割り当て（任意）。null でない項目だけを提供する。")]
        private ManualBones manualBones = new ManualBones();

        private readonly Transform[] _bones = new Transform[HumanoidBoneIdUtility.Count];
        private readonly BonePose[] _poses = new BonePose[HumanoidBoneIdUtility.Count];

        private string _registeredLabel;
        private bool _beforeRenderHooked;
        private bool _selfFallback;

        /// <summary>キャラクタを識別するラベル。</summary>
        public string Label
        {
            get => label;
            set
            {
                if (label == value) return;
                label = value;
                if (isActiveAndEnabled) ReRegister();
            }
        }

        /// <summary>骨の取得元となる Animator。</summary>
        public Animator Animator => animator;

        /// <summary>Animator が Humanoid でない場合に使う骨の手動割り当て。</summary>
        public ManualBones ManualBoneMap => manualBones;

        /// <summary>
        /// Animator / HumanoidAvatar が取得できず、手動の割り当ても無いため、
        /// 自身の Transform を全ての骨として提供するフォールバックモードで動作しているか。
        /// </summary>
        public bool IsSelfFallback => _selfFallback;

        /// <summary>Animator が Humanoid として使えるか。</summary>
        private bool IsHumanoid => animator != null && animator.isHuman;

        /// <summary>値の更新タイミング。</summary>
        public UpdateTiming Timing
        {
            get => timing;
            set
            {
                if (timing == value) return;
                timing = value;
                UpdateBeforeRenderHook();
            }
        }

        /// <summary>姿勢を更新した直後に発火する。</summary>
        public event Action<TransformPublisher> Published;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            label = gameObject.name;
        }

        private void OnEnable()
        {
            ResolveBones();
            Register();
            UpdateBeforeRenderHook();
            Publish();
        }

        private void OnDisable()
        {
            Unregister();

            if (_beforeRenderHooked)
            {
                Application.onBeforeRender -= Publish;
                _beforeRenderHooked = false;
            }
        }

        private void Update()
        {
            if (timing == UpdateTiming.Update) Publish();
        }

        private void LateUpdate()
        {
            if (timing == UpdateTiming.LateUpdate) Publish();
        }

        private void FixedUpdate()
        {
            if (timing == UpdateTiming.FixedUpdate) Publish();
        }

        /// <summary>骨の姿勢を今すぐサンプリングし、購読者へ反映する。</summary>
        [BeforeRenderOrder(-100)] // BeforeRender 時に他の onBeforeRender 処理より先に走らせる。
        public void Publish()
        {
            for (var i = 0; i < _bones.Length; i++)
            {
                var bone = _bones[i];
                if (bone == null)
                {
                    _poses[i] = default;
                    continue;
                }

                bone.GetPositionAndRotation(out var position, out var rotation);
                _poses[i] = new BonePose(position, rotation);
            }

            Published?.Invoke(this);
        }

        /// <summary>
        /// 直近にサンプリングしたグローバル姿勢を取得する。
        /// フォールバックモード中はどの骨を指定しても自身（Root）の姿勢を返す。
        /// </summary>
        public bool TryGetPose(HumanoidBoneId id, out Vector3 position, out Quaternion rotation)
        {
            var index = (int)id;
            if ((uint)index >= (uint)_poses.Length)
            {
                position = default;
                rotation = Quaternion.identity;
                return false;
            }

            if (_selfFallback) index = (int)HumanoidBoneId.Root;

            var pose = _poses[index];
            position = pose.Position;
            rotation = pose.HasValue ? pose.Rotation : Quaternion.identity;
            return pose.HasValue;
        }

        /// <summary>アバターが指定の骨を持つか。非再生時も Animator から直接判定する。</summary>
        public bool HasBone(HumanoidBoneId id) => GetBoneTransform(id) != null;

        /// <summary>
        /// 指定の骨の Transform を取得する。存在しない場合は null。
        /// Animator / HumanoidAvatar が無い場合は手動の割り当てを返し、
        /// 手動の割り当ても無ければどの骨にも自身の Transform を返す。
        /// </summary>
        public Transform GetBoneTransform(HumanoidBoneId id)
        {
            var index = (int)id;
            if ((uint)index >= (uint)_bones.Length) return null;

            if (id == HumanoidBoneId.Root) return transform;

            var cached = _bones[index];
            if (cached != null) return cached;

            if (!IsHumanoid)
            {
                // 手動の割り当てがあれば null でない項目だけを提供し、無ければ自身の Transform を提供する。
                return manualBones.HasAny ? manualBones.Get(id) : transform;
            }

            return id.TryToHumanBodyBones(out var humanBone) ? animator.GetBoneTransform(humanBone) : null;
        }

        private void ResolveBones()
        {
            var humanoid = IsHumanoid;
            var manual = !humanoid && manualBones.HasAny;
            _selfFallback = !humanoid && !manual;

            if (_selfFallback && animator != null)
            {
                Debug.LogWarning(
                    $"[{nameof(TransformPublisher)}] Animator '{animator.name}' は HumanoidAvatar を持っておらず、" +
                    "手動の骨の割り当ても無いため、自身の Transform を全ての骨として提供します。", this);
            }

            for (var i = 0; i < _bones.Length; i++)
            {
                var id = (HumanoidBoneId)i;
                if (id == HumanoidBoneId.Root)
                {
                    _bones[i] = transform;
                    continue;
                }

                if (humanoid)
                {
                    _bones[i] = id.TryToHumanBodyBones(out var humanBone) ? animator.GetBoneTransform(humanBone) : null;
                }
                else
                {
                    _bones[i] = manual ? manualBones.Get(id) : null;
                }
            }
        }

        private void Register()
        {
            _registeredLabel = label;
            TransformPublisherRegistry.Register(this);
        }

        private void Unregister()
        {
            TransformPublisherRegistry.Unregister(_registeredLabel, this);
            _registeredLabel = null;
        }

        private void ReRegister()
        {
            Unregister();
            Register();
        }

        private void UpdateBeforeRenderHook()
        {
            var required = isActiveAndEnabled && timing == UpdateTiming.BeforeRender;
            if (required == _beforeRenderHooked) return;

            if (required) Application.onBeforeRender += Publish;
            else Application.onBeforeRender -= Publish;

            _beforeRenderHooked = required;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            ResolveBones();
            if (_registeredLabel != label) ReRegister();
            UpdateBeforeRenderHook();
        }
#endif

        /// <summary>
        /// Animator が Humanoid でない場合に使う骨の手動割り当て。
        /// 全ての項目は任意で、null の骨は提供しない。Root は常に Publisher 自身。
        /// </summary>
        [Serializable]
        public sealed class ManualBones
        {
            public Transform hips;
            public Transform spine;
            public Transform chest;
            public Transform upperChest;
            public Transform neck;
            public Transform head;
            public Transform leftShoulder;
            public Transform leftUpperArm;
            public Transform leftLowerArm;
            public Transform leftHand;
            public Transform rightShoulder;
            public Transform rightUpperArm;
            public Transform rightLowerArm;
            public Transform rightHand;
            public Transform leftUpperLeg;
            public Transform leftLowerLeg;
            public Transform leftFoot;
            public Transform rightUpperLeg;
            public Transform rightLowerLeg;
            public Transform rightFoot;

            /// <summary>null でない割り当てが 1 つ以上あるか。</summary>
            public bool HasAny
            {
                get
                {
                    for (var i = 0; i < HumanoidBoneIdUtility.Count; i++)
                    {
                        if (Get((HumanoidBoneId)i) != null) return true;
                    }

                    return false;
                }
            }

            /// <summary>指定の骨に割り当てられた Transform を取得する。未割り当て・Root は null。</summary>
            public Transform Get(HumanoidBoneId id)
            {
                switch (id)
                {
                    case HumanoidBoneId.Hips: return hips;
                    case HumanoidBoneId.Spine: return spine;
                    case HumanoidBoneId.Chest: return chest;
                    case HumanoidBoneId.UpperChest: return upperChest;
                    case HumanoidBoneId.Neck: return neck;
                    case HumanoidBoneId.Head: return head;
                    case HumanoidBoneId.LeftShoulder: return leftShoulder;
                    case HumanoidBoneId.LeftUpperArm: return leftUpperArm;
                    case HumanoidBoneId.LeftLowerArm: return leftLowerArm;
                    case HumanoidBoneId.LeftHand: return leftHand;
                    case HumanoidBoneId.RightShoulder: return rightShoulder;
                    case HumanoidBoneId.RightUpperArm: return rightUpperArm;
                    case HumanoidBoneId.RightLowerArm: return rightLowerArm;
                    case HumanoidBoneId.RightHand: return rightHand;
                    case HumanoidBoneId.LeftUpperLeg: return leftUpperLeg;
                    case HumanoidBoneId.LeftLowerLeg: return leftLowerLeg;
                    case HumanoidBoneId.LeftFoot: return leftFoot;
                    case HumanoidBoneId.RightUpperLeg: return rightUpperLeg;
                    case HumanoidBoneId.RightLowerLeg: return rightLowerLeg;
                    case HumanoidBoneId.RightFoot: return rightFoot;
                    default: return null;
                }
            }
        }

        private readonly struct BonePose
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly bool HasValue;

            public BonePose(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
                HasValue = true;
            }
        }
    }
}
