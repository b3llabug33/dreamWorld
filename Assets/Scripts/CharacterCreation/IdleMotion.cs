using UnityEngine;

namespace DreamWorld.CharacterCreation
{
    // Procedural idle: breathing, a slow side-to-side weight shift, small head movements and
    // relaxed swaying arms. It layers small rotations on top of the pose the bones start in,
    // so it works with every outfit and needs no animation files. Put it on the model root
    // (the object with the Animator). The Animator itself stays disabled: with a humanoid avatar
    // but no controller it resets the hips to the ground in Play mode, so bones are found by name.
    public class IdleMotion : MonoBehaviour
    {
        [Header("Breathing")]
        public float breathsPerMinute = 15f;
        public float chestDegrees = 1.6f;
        public float shoulderLift = 1.2f;

        [Header("Sway")]
        public float swaySeconds = 6f;
        public float hipDegrees = 1.8f;
        public float hipShift = 0.008f;

        [Header("Head")]
        public float headYaw = 5f;
        public float headTilt = 3f;
        public float headNod = 2f;

        [Header("Arms")]
        public float elbowBend = 9f;
        public float armSway = 2.5f;

        Transform hips, spine, chest, neck, head;
        Transform[] upperArms, lowerArms;
        Quaternion[] baseUpper, baseLower;
        Quaternion baseHips, baseSpine, baseChest, baseNeck, baseHead;
        Vector3 baseHipsPos;
        float seed;

        void Start()
        {
            hips = Bone("Hips"); spine = Bone("Spine"); chest = Bone("Chest"); neck = Bone("Neck"); head = Bone("Head");
            upperArms = new[] { Bone("UpperArm.L"), Bone("UpperArm.R") };
            lowerArms = new[] { Bone("LowerArm.L"), Bone("LowerArm.R") };
            baseHips = hips.localRotation; baseSpine = spine.localRotation; baseChest = chest.localRotation;
            baseNeck = neck.localRotation; baseHead = head.localRotation; baseHipsPos = hips.localPosition;
            baseUpper = new[] { upperArms[0].localRotation, upperArms[1].localRotation };
            baseLower = new[] { lowerArms[0].localRotation, lowerArms[1].localRotation };
            seed = Random.value * 10f;
        }

        Transform Bone(string boneName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;
            Debug.LogWarning("[IdleMotion] missing bone " + boneName);
            return null;
        }

        // Rotate a bone by `degrees` around a character-space axis, on top of its base pose.
        void Turn(Transform bone, Quaternion baseLocal, Vector3 characterAxis, float degrees)
        {
            Vector3 worldAxis = transform.TransformDirection(characterAxis);
            Quaternion parent = bone.parent.rotation;
            Quaternion delta = Quaternion.Inverse(parent) * Quaternion.AngleAxis(degrees, worldAxis) * parent;
            bone.localRotation = delta * baseLocal;
        }

        void LateUpdate()
        {
            float t = Time.time + seed;
            float breath = Mathf.Sin(t * Mathf.PI * 2f * breathsPerMinute / 60f);
            float sway = Mathf.Sin(t * Mathf.PI * 2f / swaySeconds);
            float slow = Mathf.Sin(t * 0.37f) * 0.6f + Mathf.Sin(t * 0.61f) * 0.4f;   // wandering, not robotic

            // weight shift: hips roll and slide a little, the spine leans back the other way
            hips.localPosition = baseHipsPos + hips.parent.InverseTransformDirection(transform.right) * (sway * hipShift);
            Turn(hips, baseHips, Vector3.forward, sway * hipDegrees);
            Turn(spine, baseSpine, Vector3.forward, -sway * hipDegrees * 0.6f);

            // breathing lifts the chest
            Turn(chest, baseChest, Vector3.right, -breath * chestDegrees);

            // head: slow look-around plus a tiny counter to the sway
            Turn(neck, baseNeck, Vector3.forward, -sway * hipDegrees * 0.4f);
            head.localRotation = baseHead;
            Turn(head, baseHead, Vector3.up, slow * headYaw);
            Quaternion afterYaw = head.localRotation;
            Turn(head, afterYaw, Vector3.forward, Mathf.Sin(t * 0.29f) * headTilt);
            Quaternion afterTilt = head.localRotation;
            Turn(head, afterTilt, Vector3.right, Mathf.Sin(t * 0.43f) * headNod + breath * 0.4f);

            // arms hang relaxed: slight elbow bend, sway with the body and breathing
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                Turn(upperArms[i], baseUpper[i], Vector3.forward, side * (breath * shoulderLift * 0.5f) - sway * armSway * 0.5f);
                Quaternion afterUpper = upperArms[i].localRotation;
                Turn(upperArms[i], afterUpper, Vector3.right, Mathf.Sin(t * 0.8f + i) * armSway * 0.4f);
                Turn(lowerArms[i], baseLower[i], Vector3.right, -elbowBend - Mathf.Sin(t * 0.9f + i * 1.3f) * 2f);
            }
        }
    }
}
