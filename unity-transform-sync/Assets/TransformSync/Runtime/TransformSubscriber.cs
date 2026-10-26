using UnityEngine;

namespace Hidano.TransformSync.Runtime
{
    /// <summary>
    /// ラベルで指定した <see cref="TransformPublisher"/> の骨の姿勢に Target を追従させる。
    /// 主な用途はカメラの追従対象の指定。
    /// </summary>
    [AddComponentMenu("Hidano/Transform Sync/Transform Subscriber")]
    public sealed class TransformSubscriber : MonoBehaviour
    {
        [SerializeField, Tooltip("追従させる Transform。未設定の場合はこの GameObject 自身の Transform を使う。")]
        private Transform target;

        [SerializeField, Tooltip("追従対象の TransformPublisher のラベル。")]
        private string publisherLabel;

        [SerializeField, Tooltip("追従する骨。")]
        private HumanoidBoneId boneId = HumanoidBoneId.Head;

        private Transform _target;
        private TransformPublisher _publisher;

        /// <summary>追従させる Transform。null の場合は自身の Transform を使う。</summary>
        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                _target = ResolveTarget();
            }
        }

        /// <summary>追従対象の Publisher のラベル。</summary>
        public string PublisherLabel
        {
            get => publisherLabel;
            set
            {
                if (publisherLabel == value) return;
                publisherLabel = value;
                if (isActiveAndEnabled) Rebind();
            }
        }

        /// <summary>追従する骨。</summary>
        public HumanoidBoneId BoneId
        {
            get => boneId;
            set => boneId = value;
        }

        /// <summary>現在バインドされている Publisher。未バインドなら null。</summary>
        public TransformPublisher BoundPublisher => _publisher;

        private void OnEnable()
        {
            _target = ResolveTarget();
            TransformPublisherRegistry.LabelChanged += OnLabelChanged;
            Bind();
        }

        private void OnDisable()
        {
            TransformPublisherRegistry.LabelChanged -= OnLabelChanged;
            Unbind();
        }

        private Transform ResolveTarget() => target != null ? target : transform;

        private void OnLabelChanged(string changedLabel)
        {
            if (changedLabel != publisherLabel) return;
            Rebind();
        }

        private void Rebind()
        {
            Unbind();
            Bind();
        }

        private void Bind()
        {
            if (!TransformPublisherRegistry.TryGet(publisherLabel, out var publisher)) return;

            _publisher = publisher;
            _publisher.Published += OnPublished;

            // バインド直後に現在の姿勢を 1 回反映しておく。
            OnPublished(_publisher);
        }

        private void Unbind()
        {
            if (_publisher == null) return;

            _publisher.Published -= OnPublished;
            _publisher = null;
        }

        private void OnPublished(TransformPublisher publisher)
        {
            if (_target == null) return;
            if (!publisher.TryGetPose(boneId, out var position, out var rotation)) return;

            _target.SetPositionAndRotation(position, rotation);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            _target = ResolveTarget();

            var boundLabel = _publisher != null ? _publisher.Label : null;
            if (boundLabel != publisherLabel) Rebind();
        }
#endif
    }
}
