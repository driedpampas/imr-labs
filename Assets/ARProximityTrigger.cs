using UnityEngine;
using Vuforia;

public class ARProximityTrigger : MonoBehaviour
{
    [Header("Image Targets")]
    [SerializeField] private ObserverBehaviour targetA;
    [SerializeField] private ObserverBehaviour targetB;

    [Header("Character Animators")]
    [SerializeField] private Animator animatorA;
    [SerializeField] private Animator animatorB;

    [Header("Interaction Settings")]
    [Tooltip("Distance in meters to trigger attack mode")]
    [SerializeField] private float attackDistance = 0.25f;
    [SerializeField] private float rotationSpeed = 5f;

    private static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");

    private void Update()
    {
        if (targetA == null || targetB == null) return;

        bool isTargetATracked = IsTracked(targetA);
        bool isTargetBTracked = IsTracked(targetB);

        // Only calculate distance if both targets are actively tracked
        if (isTargetATracked && isTargetBTracked)
        {
            Vector3 posA = targetA.transform.position;
            Vector3 posB = targetB.transform.position;

            float currentDistance = Vector3.Distance(posA, posB);
            bool inRange = currentDistance <= attackDistance;

            UpdateAnimationState(inRange);

            if (inRange)
            {
                FaceEachOther(posA, posB);
            }
        }
        else
        {
            // Reset to Idle if either target leaves the camera view
            UpdateAnimationState(false);
        }
    }

    private bool IsTracked(ObserverBehaviour target)
    {
        TargetStatus status = target.TargetStatus;
        return status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;
    }

    private void UpdateAnimationState(bool isAttacking)
    {
        if (animatorA != null && animatorA.GetBool(IsAttackingHash) != isAttacking)
        {
            animatorA.SetBool(IsAttackingHash, isAttacking);
        }

        if (animatorB != null && animatorB.GetBool(IsAttackingHash) != isAttacking)
        {
            animatorB.SetBool(IsAttackingHash, isAttacking);
        }
    }

    private void FaceEachOther(Vector3 posA, Vector3 posB)
    {
        // Smoothly rotate Cactus A toward Cactus B
        Vector3 dirAtoB = (posB - posA);
        dirAtoB.y = 0; // Keep horizontal alignment
        if (dirAtoB != Vector3.zero && animatorA != null)
        {
            Quaternion rotA = Quaternion.LookRotation(dirAtoB);
            animatorA.transform.rotation = Quaternion.Slerp(animatorA.transform.rotation, rotA, Time.deltaTime * rotationSpeed);
        }

        // Smoothly rotate Cactus B toward Cactus A
        Vector3 dirBtoA = (posA - posB);
        dirBtoA.y = 0;
        if (dirBtoA != Vector3.zero && animatorB != null)
        {
            Quaternion rotB = Quaternion.LookRotation(dirBtoA);
            animatorB.transform.rotation = Quaternion.Slerp(animatorB.transform.rotation, rotB, Time.deltaTime * rotationSpeed);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (targetA != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetA.transform.position, attackDistance);
        }
    }
}