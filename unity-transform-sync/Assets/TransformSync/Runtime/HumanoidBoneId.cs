using System;
using UnityEngine;

namespace Hidano.TransformSync.Runtime
{
    /// <summary>
    /// <see cref="TransformPublisher"/> が公開する Humanoid の主要な骨。
    /// 値は 0 から連番であることを前提に配列の添字として使用する。
    /// </summary>
    public enum HumanoidBoneId
    {
        /// <summary>キャラクタのルート（Publisher がアタッチされた Transform）。</summary>
        Root = 0,
        Hips,
        Spine,
        Chest,
        UpperChest,
        Neck,
        Head,
        LeftShoulder,
        LeftUpperArm,
        LeftLowerArm,
        LeftHand,
        RightShoulder,
        RightUpperArm,
        RightLowerArm,
        RightHand,
        LeftUpperLeg,
        LeftLowerLeg,
        LeftFoot,
        RightUpperLeg,
        RightLowerLeg,
        RightFoot,
    }

    public static class HumanoidBoneIdUtility
    {
        /// <summary><see cref="HumanoidBoneId"/> の要素数。配列の確保に使う。</summary>
        public static readonly int Count = Enum.GetValues(typeof(HumanoidBoneId)).Length;

        /// <summary>
        /// 対応する <see cref="HumanBodyBones"/> を取得する。
        /// <see cref="HumanoidBoneId.Root"/> は Humanoid の骨ではないため false を返す。
        /// </summary>
        public static bool TryToHumanBodyBones(this HumanoidBoneId id, out HumanBodyBones bone)
        {
            switch (id)
            {
                case HumanoidBoneId.Hips: bone = HumanBodyBones.Hips; return true;
                case HumanoidBoneId.Spine: bone = HumanBodyBones.Spine; return true;
                case HumanoidBoneId.Chest: bone = HumanBodyBones.Chest; return true;
                case HumanoidBoneId.UpperChest: bone = HumanBodyBones.UpperChest; return true;
                case HumanoidBoneId.Neck: bone = HumanBodyBones.Neck; return true;
                case HumanoidBoneId.Head: bone = HumanBodyBones.Head; return true;
                case HumanoidBoneId.LeftShoulder: bone = HumanBodyBones.LeftShoulder; return true;
                case HumanoidBoneId.LeftUpperArm: bone = HumanBodyBones.LeftUpperArm; return true;
                case HumanoidBoneId.LeftLowerArm: bone = HumanBodyBones.LeftLowerArm; return true;
                case HumanoidBoneId.LeftHand: bone = HumanBodyBones.LeftHand; return true;
                case HumanoidBoneId.RightShoulder: bone = HumanBodyBones.RightShoulder; return true;
                case HumanoidBoneId.RightUpperArm: bone = HumanBodyBones.RightUpperArm; return true;
                case HumanoidBoneId.RightLowerArm: bone = HumanBodyBones.RightLowerArm; return true;
                case HumanoidBoneId.RightHand: bone = HumanBodyBones.RightHand; return true;
                case HumanoidBoneId.LeftUpperLeg: bone = HumanBodyBones.LeftUpperLeg; return true;
                case HumanoidBoneId.LeftLowerLeg: bone = HumanBodyBones.LeftLowerLeg; return true;
                case HumanoidBoneId.LeftFoot: bone = HumanBodyBones.LeftFoot; return true;
                case HumanoidBoneId.RightUpperLeg: bone = HumanBodyBones.RightUpperLeg; return true;
                case HumanoidBoneId.RightLowerLeg: bone = HumanBodyBones.RightLowerLeg; return true;
                case HumanoidBoneId.RightFoot: bone = HumanBodyBones.RightFoot; return true;
                default: bone = default; return false;
            }
        }
    }
}
