using System;
using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;
using UniHumanoid;
using Unity.Mathematics;
using UnityEngine;
using UniVRM10;

namespace VRM2Magica.Runtime
{
    public static class Vrm10ToMagicaConverter
    {
        public static void Convert(Vrm10Instance instance)
        {
            var springBoneData = instance.SpringBone;
            if (springBoneData == null)
            {
                return;
            }

            var headBone = instance.Humanoid.Head;

            var generatedColliderCache = new Dictionary<VRM10SpringBoneCollider, ColliderComponent>();

            var rootJointGroups = GroupingJoint(springBoneData, instance.Humanoid);

            foreach (var kvp in rootJointGroups)
            {
                List<(MagicaCloth cloth, List<Vrm10InstanceSpringBone.Spring> springs)> clothAndSpringsPairs;
                if (kvp.Key == JointType.Hair || kvp.Key == JointType.Breast)
                {
                    clothAndSpringsPairs = ConvertHairSprings(instance, kvp.Value);
                }
                else
                {
                    clothAndSpringsPairs = ConvertClothSprings(instance, kvp.Value);
                }

                foreach (var (cloth, springs) in clothAndSpringsPairs)
                {
                    var colliderGroups = springs.SelectMany(x => x.ColliderGroups).Distinct().ToList();
                    ConvertColliders(cloth, colliderGroups, generatedColliderCache);
                    cloth.BuildAndRun();
                }
            }

            foreach (var spring in springBoneData.Springs)
            {
                foreach (var joint in spring.Joints)
                {
                    UnityEngine.Object.DestroyImmediate(joint);
                }

                foreach (var colliderGroup in spring.ColliderGroups)
                {
                    var colliders = colliderGroup.Colliders;
                    foreach (var collider in colliders)
                    {
                        UnityEngine.Object.DestroyImmediate(collider);
                    }

                    UnityEngine.Object.DestroyImmediate(colliderGroup);
                }
            }

            springBoneData.Springs.Clear();
        }

        private static List<(MagicaCloth cloth, List<Vrm10InstanceSpringBone.Spring> springs)> ConvertHairSprings(
            Vrm10Instance instance, List<Vrm10InstanceSpringBone.Spring> springs)
        {
            var result = new List<(MagicaCloth cloth, List<Vrm10InstanceSpringBone.Spring> springs)>();
            foreach (var spring in springs)
            {
                var joints = spring.Joints;
                if (joints.Count == 0)
                {
                    continue;
                }

                var rootJoint = joints[0];
                var clothObj = new GameObject($"MagicaCloth_{rootJoint.name}");
                clothObj.transform.SetParent(instance.transform, false);
                var cloth = clothObj.AddComponent<MagicaCloth>();
                cloth.SerializeData.clothType = ClothProcess.ClothType.BoneCloth;
                cloth.Process.SetState(ClothProcess.State_DisableAutoBuild, true);

                cloth.SerializeData.rootBones.Add(rootJoint.transform);
                cloth.SerializeData.connectionMode = RenderSetupData.BoneConnectionMode.Line;
                cloth.SerializeData.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Edge;
                cloth.SerializeData.gravityDirection = new float3(
                    rootJoint.m_gravityDir.x,
                    rootJoint.m_gravityDir.y,
                    rootJoint.m_gravityDir.z
                );
                cloth.SerializeData.gravity = rootJoint.m_gravityPower;
                cloth.SerializeData.gravityFalloff = 1.0f;
                cloth.SerializeData.damping.value = rootJoint.m_dragForce * 0.8f;
                cloth.SerializeData.angleRestorationConstraint.stiffness.value =
                    Mathf.Clamp01(rootJoint.m_stiffnessForce * 0.5f);

                result.Add((cloth, new List<Vrm10InstanceSpringBone.Spring> { spring }));
            }

            return result;
        }

        private static List<(MagicaCloth cloth, List<Vrm10InstanceSpringBone.Spring> springs)> ConvertClothSprings(
            Vrm10Instance instance, List<Vrm10InstanceSpringBone.Spring> springs)
        {
            var result = new List<(MagicaCloth cloth, List<Vrm10InstanceSpringBone.Spring> springs)>();

            var groupByJointParent = springs
                .Where(x => x.Joints.Count != 0)
                .GroupBy(x => x.Joints[0].transform.parent);

            foreach (var group in groupByJointParent)
            {
                var jointParent = group.Key;
                var clothObj = new GameObject($"MagicaCloth_{jointParent.name}");
                clothObj.transform.SetParent(instance.transform, false);
                var cloth = clothObj.AddComponent<MagicaCloth>();
                cloth.Process.SetState(ClothProcess.State_DisableAutoBuild, true);

                cloth.SerializeData.clothType = ClothProcess.ClothType.BoneCloth;
                cloth.SerializeData.connectionMode = RenderSetupData.BoneConnectionMode.AutomaticMesh;
                cloth.SerializeData.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Edge;
                var doneApplyParam = false;
                foreach (var spring in group)
                {
                    if (spring.Joints.Count == 0)
                    {
                        continue;
                    }

                    var rootJoint = spring.Joints[0];
                    cloth.SerializeData.rootBones.Add(rootJoint.transform);

                    // todo とりあえず最初にみつかったrootJointを使ってパラメータを構築しているが、いい感じに重みづけ平均やCurveなどを実装するべき
                    if (!doneApplyParam)
                    {
                        cloth.SerializeData.gravityDirection = new float3(
                            rootJoint.m_gravityDir.x,
                            rootJoint.m_gravityDir.y,
                            rootJoint.m_gravityDir.z
                        );
                        cloth.SerializeData.gravity = rootJoint.m_gravityPower;
                        cloth.SerializeData.gravityFalloff = 1.0f;
                        cloth.SerializeData.damping.value = rootJoint.m_dragForce * 0.2f;
                        cloth.SerializeData.angleRestorationConstraint.stiffness.value =
                            Mathf.Clamp01(rootJoint.m_stiffnessForce * 0.25f);

                        doneApplyParam = true;
                    }
                }


                result.Add((cloth, group.ToList()));
            }

            return result;
        }

        private enum JointType
        {
            Hair,
            Breast,
            Cloth
        }

        private static Dictionary<JointType, List<Vrm10InstanceSpringBone.Spring>>
            GroupingJoint(Vrm10InstanceSpringBone springBone, Humanoid uniHumanoid)
        {
            var hipsTransform = uniHumanoid.Hips;
            var breastRoot = uniHumanoid.UpperChest != null ? uniHumanoid.UpperChest : uniHumanoid.Chest;
            var breastRootChildJoints = springBone.Springs
                .Where(x => x.Joints.Count != 0)
                .Select(x => x.Joints[0])
                .Where(rootJoint => rootJoint.transform.parent == breastRoot)
                .Select(x => x.transform)
                .ToList();

            var breastPair = BreastUtil.SelectBestPair(breastRootChildJoints, hipsTransform);

            var jointTypeToSprings = Enum.GetValues(typeof(JointType))
                .OfType<JointType>()
                .ToDictionary(x => x, _ => new List<Vrm10InstanceSpringBone.Spring>());

            var hairSprings = new List<Vrm10InstanceSpringBone.Spring>();
            var breastSprings = new List<VRM10SpringBoneJoint>();
            var clothSprings = new List<VRM10SpringBoneJoint>();

            var headTransform = uniHumanoid.Head;
            foreach (var spring in springBone.Springs)
            {
                if (spring.Joints.Count == 0)
                {
                    continue;
                }

                var rootJoint = spring.Joints[0];
                var parentTransform = rootJoint.transform;
                if (parentTransform.position.y >= headTransform.position.y)
                {
                    jointTypeToSprings[JointType.Hair].Add(spring);
                }
                else if ((breastPair != null && rootJoint.transform == breastPair.Value.Left) ||
                         (breastPair != null && rootJoint.transform == breastPair.Value.Right))
                {
                    jointTypeToSprings[JointType.Breast].Add(spring);
                }
                else
                {
                    jointTypeToSprings[JointType.Cloth].Add(spring);
                }
            }

            return jointTypeToSprings;
        }

        private static void ConvertColliders(
            MagicaCloth cloth,
            List<VRM10SpringBoneColliderGroup> colliderGroups,
            Dictionary<VRM10SpringBoneCollider, ColliderComponent> generatedCache
        )
        {
            var colliders = new List<ColliderComponent>();

            foreach (var colliderGroup in colliderGroups)
            {
                foreach (var vrmCol in colliderGroup.Colliders)
                {
                    if (!generatedCache.TryGetValue(vrmCol, out var colliderComponent))
                    {
                        var vmrColTransform = vrmCol.transform;
                        var magicaColliderObject = new GameObject()
                        {
                            transform =
                            {
                                parent = vrmCol.transform,
                                localPosition = Vector3.zero,
                                localRotation = Quaternion.identity,
                                localScale = Vector3.one,
                            },
                        };

                        switch (vrmCol.ColliderType)
                        {
                            case VRM10SpringBoneColliderTypes.Sphere:
                            case VRM10SpringBoneColliderTypes.Plane:
                            case VRM10SpringBoneColliderTypes.SphereInside:
                                magicaColliderObject.name = $"{nameof(MagicaSphereCollider)}_{vrmCol.name}";
                                colliderComponent = AttachMagicaSphere(vrmCol, magicaColliderObject);
                                break;
                            case VRM10SpringBoneColliderTypes.Capsule:
                            case VRM10SpringBoneColliderTypes.CapsuleInside:
                                magicaColliderObject.name = $"{nameof(MagicaCapsuleCollider)}_{vrmCol.name}";
                                colliderComponent = AttachMagicaCapsule(vrmCol, magicaColliderObject);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }


                        generatedCache.Add(vrmCol, colliderComponent);
                    }

                    colliders.Add(colliderComponent);
                }
            }

            cloth.SerializeData.colliderCollisionConstraint.colliderList.AddRange(colliders);
        }

        private static MagicaSphereCollider AttachMagicaSphere(
            VRM10SpringBoneCollider vrmCol,
            GameObject attachTarget
        )
        {
            var sphere = attachTarget.AddComponent<MagicaSphereCollider>();
            var size = vrmCol.Radius;
            sphere.SetSize(size);
            sphere.center = vrmCol.Offset;

            return sphere;
        }

        private static MagicaCapsuleCollider AttachMagicaCapsule(
            VRM10SpringBoneCollider vrmCol,
            GameObject attachTarget
        )
        {
            var capsule = attachTarget.AddComponent<MagicaCapsuleCollider>();

            attachTarget.transform.localPosition = (vrmCol.Offset + vrmCol.Tail) * 0.5f;
            capsule.center = Vector3.zero;

            var direction = vrmCol.Tail - vrmCol.Offset;
            var tailWorld = vrmCol.transform.TransformPoint(vrmCol.Tail);
            var offsetWorld = vrmCol.transform.TransformPoint(vrmCol.Offset);
            var length = Vector3.Distance(tailWorld, offsetWorld);

            var radius = vrmCol.Radius;
            if (length > 0.0001f)
            {
                var rot = Quaternion.FromToRotation(Vector3.up, direction);
                attachTarget.transform.localRotation = rot;

                // VRMSpringBoneのCapsuleColliderはTransform位置を中心とした円が始点となり、終点も円の中心の指定であるのに対して
                // MagicaClothのCapsuleColliderはCenterからLength分を軸方向に伸ばした位置を外接点を始点と終点とする
                // そのため、VRMSpringBone側の半径を2つ加算する必要がある
                length += radius * 2;
            }
            else
            {
                attachTarget.transform.localRotation = Quaternion.identity;
            }

            capsule.direction = MagicaCapsuleCollider.Direction.Y;
            capsule.SetSize(radius, radius, length);

            return capsule;
        }
    }
}