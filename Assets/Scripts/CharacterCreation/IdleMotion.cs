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

        [Header("Walking (driven by PlayerController)")]
        [Range(0, 1)] public float moveAmount;   // 0 = standing, ~0.5 = walk, 1 = run
        public float stridePhase;                // advances with distance traveled
        public float legSwing = 30f;
        public float kneeBend = 40f;
        public float armWalkSwing = 24f;
        public float bob = 0.03f;

        [Header("Combat poses (driven by PlayerController)")]
        [Range(-0.4f, 1)] public float attack;   // <0 wind-up, 0..1 strike + follow-through
        public int attackArm;                    // 0 = left, 1 = right, 2 = both (heavy)
        [Range(0, 1)] public float guard;        // arms up in front of the face
        [Range(0, 1)] public float tuck;         // curled up (rolling)

        Transform hips, spine, chest, neck, head;
        Transform[] upperArms, lowerArms, upperLegs, lowerLegs;
        Quaternion[] baseUpper, baseLower, baseUpperLeg, baseLowerLeg;
        Quaternion baseHips, baseSpine, baseChest, baseNeck, baseHead;
        Vector3 baseHipsPos;
        float seed;

        void Start()
        {
            hips = Bone("Hips"); spine = Bone("Spine"); chest = Bone("Chest"); neck = Bone("Neck"); head = Bone("Head");
            upperArms = new[] { Bone("UpperArm.L"), Bone("UpperArm.R") };
            lowerArms = new[] { Bone("LowerArm.L"), Bone("LowerArm.R") };
            upperLegs = new[] { Bone("UpperLeg.L"), Bone("UpperLeg.R") };
            lowerLegs = new[] { Bone("LowerLeg.L"), Bone("LowerLeg.R") };
            baseHips = hips.localRotation; baseSpine = spine.localRotation; baseChest = chest.localRotation;
            baseNeck = neck.localRotation; baseHead = head.localRotation; baseHipsPos = hips.localPosition;
            baseUpper = new[] { upperArms[0].localRotation, upperArms[1].localRotation };
            baseLower = new[] { lowerArms[0].localRotation, lowerArms[1].localRotation };
            baseUpperLeg = new[] { upperLegs[0].localRotation, upperLegs[1].localRotation };
            baseLowerLeg = new[] { lowerLegs[0].localRotation, lowerLegs[1].localRotation };
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
            float m = moveAmount;
            float still = 1f - Mathf.Clamp01(m * 2f);                 // idle sway fades out while walking
            float sway = Mathf.Sin(t * Mathf.PI * 2f / swaySeconds) * still;
            float stride = Mathf.Sin(stridePhase);
            float slow = Mathf.Sin(t * 0.37f) * 0.6f + Mathf.Sin(t * 0.61f) * 0.4f;   // wandering, not robotic

            // weight shift: hips roll and slide a little, the spine leans back the other way
            hips.localPosition = baseHipsPos
                + hips.parent.InverseTransformDirection(transform.right) * (sway * hipShift)
                + hips.parent.InverseTransformDirection(transform.up) * (Mathf.Abs(Mathf.Cos(stridePhase)) - 0.5f) * bob * m;
            Turn(hips, baseHips, Vector3.forward, sway * hipDegrees);
            Turn(spine, baseSpine, Vector3.forward, -sway * hipDegrees * 0.6f);

            // breathing lifts the chest; leaning forward a little when running
            Turn(chest, baseChest, Vector3.right, -breath * chestDegrees + m * 6f);

            // legs: swing forward/back in opposite phase, knees bend on the back swing
            for (int i = 0; i < 2; i++)
            {
                float phase = stridePhase + i * Mathf.PI;
                Turn(upperLegs[i], baseUpperLeg[i], Vector3.right, -Mathf.Sin(phase) * legSwing * m);
                Turn(lowerLegs[i], baseLowerLeg[i], Vector3.right, Mathf.Max(0, Mathf.Cos(phase)) * kneeBend * m);
            }

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
                // arms swing opposite to the legs while walking
                float armWalk = (i == 0 ? 1f : -1f) * stride * armWalkSwing * m;
                Turn(upperArms[i], afterUpper, Vector3.right, Mathf.Sin(t * 0.8f + i) * armSway * 0.4f * still + armWalk);
                Turn(lowerArms[i], baseLower[i], Vector3.right, -elbowBend - Mathf.Sin(t * 0.9f + i * 1.3f) * 2f);
            }
            CombatPose();
        }

        // Layered on top of everything above: punches, guard, and the tucked roll.
        void CombatPose()
        {
            if (attack == 0f && guard == 0f && tuck == 0f) return;
            for (int i = 0; i < 2; i++)
            {
                bool swinging = attack != 0f && (attackArm == 2 || attackArm == i);
                float raise = guard * 75f + tuck * 40f;
                float bend = guard * 95f + tuck * 70f;
                if (swinging)
                {
                    // wind-up pulls the arm back and bends it; the strike throws it straight forward
                    raise += attack > 0 ? attack * 95f : attack * 60f;
                    bend += attack > 0 ? -attack * 30f : -attack * 90f;
                }
                Quaternion up = upperArms[i].localRotation;
                Turn(upperArms[i], up, Vector3.right, -raise);
                Quaternion low = lowerArms[i].localRotation;
                Turn(lowerArms[i], low, Vector3.right, -bend);
            }
            // the body twists into a swing and curls into a roll
            Quaternion chestNow = chest.localRotation;
            float twist = attackArm == 2 ? 0f : (attackArm == 1 ? 1f : -1f) * attack * 22f;
            Turn(chest, chestNow, Vector3.up, twist);
            Quaternion chestBent = chest.localRotation;
            Turn(chest, chestBent, Vector3.right, tuck * 35f + Mathf.Max(0f, attack) * (attackArm == 2 ? 18f : 8f));
            for (int i = 0; i < 2; i++)
            {
                Quaternion u = upperLegs[i].localRotation;
                Turn(upperLegs[i], u, Vector3.right, -tuck * 70f);
                Quaternion l = lowerLegs[i].localRotation;
                Turn(lowerLegs[i], l, Vector3.right, tuck * 110f);
            }
        }
    }
}
