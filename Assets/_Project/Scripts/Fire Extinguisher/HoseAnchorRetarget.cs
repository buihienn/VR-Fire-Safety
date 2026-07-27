using UnityEngine;

[DisallowMultipleComponent]
public class HoseAnchorRetarget : MonoBehaviour
{
    [Header("External rigidbodies")]
    public Rigidbody bodyRigidbody;      // Rigidbody của bình (Extinguisher_Body hoặc FireExtinguisher root)
    public Rigidbody nozzleRigidbody;    // Rigidbody của nozzle

    [Header("Anchor transforms (helper empties)")]
    public Transform bodyJointAnchor;    // BodyJointAnchor
    public Transform nozzleJointAnchor;  // NozzleJointAnchor

    [Header("Hose bones (transforms)")]
    public Transform hoseRootBone;       // Hose_Root
    public Transform hoseTipBone;        // Hose_Tip (KHÔNG phải _end)

    [Header("Options")]
    public bool alsoZeroMiddleAnchors = true; // set anchor/connectedAnchor của các bone giữa về 0
    public bool runOnStart = true;

    void Start()
    {
        if (runOnStart) Retarget();
    }

    [ContextMenu("Retarget Hose Anchors Now")]
    public void Retarget()
    {
        if (!ValidateRefs()) return;

        // --- ROOT ---
        var rootJoint = hoseRootBone.GetComponent<ConfigurableJoint>();
        if (!rootJoint)
        {
            Debug.LogError("[HoseAnchorRetarget] Hose_Root thiếu ConfigurableJoint.");
            return;
        }

        rootJoint.autoConfigureConnectedAnchor = false;
        rootJoint.connectedBody = bodyRigidbody;

        // Anchor ở local của Hose_Root (thường để 0 là đúng)
        rootJoint.anchor = Vector3.zero;

        // ConnectedAnchor phải là local của bodyRigidbody
        rootJoint.connectedAnchor = bodyRigidbody.transform.InverseTransformPoint(bodyJointAnchor.position);

        // --- TIP ---
        var tipJoint = hoseTipBone.GetComponent<ConfigurableJoint>();
        if (!tipJoint)
        {
            Debug.LogError("[HoseAnchorRetarget] Hose_Tip thiếu ConfigurableJoint.");
            return;
        }

        tipJoint.autoConfigureConnectedAnchor = false;
        tipJoint.connectedBody = nozzleRigidbody;

        tipJoint.anchor = Vector3.zero;
        tipJoint.connectedAnchor = nozzleRigidbody.transform.InverseTransformPoint(nozzleJointAnchor.position);

        // --- MIDDLE (optional) ---
        if (alsoZeroMiddleAnchors)
        {
            // đi theo chain 1-child từ root -> tip
            Transform cur = hoseRootBone;
            int safety = 0;

            while (cur != null && cur != hoseTipBone && safety++ < 200)
            {
                // bone con tiếp theo (thường là child[0])
                if (cur.childCount == 0) break;
                Transform next = cur.GetChild(0);

                // bỏ qua các _end nodes nếu có
                if (next.name.ToLower().EndsWith("_end"))
                {
                    if (next.childCount > 0) next = next.GetChild(0);
                    else break;
                }

                // set joint của bone "next" nối vào rigidbody của bone hiện tại
                var nextJoint = next.GetComponent<ConfigurableJoint>();
                var curRb = cur.GetComponent<Rigidbody>();
                if (nextJoint && curRb)
                {
                    nextJoint.autoConfigureConnectedAnchor = false;
                    nextJoint.connectedBody = curRb;
                    nextJoint.anchor = Vector3.zero;
                    nextJoint.connectedAnchor = Vector3.zero;
                }

                cur = next;
            }
        }

        Debug.Log("[HoseAnchorRetarget] Retarget xong anchors cho Root/Tip (và middle nếu bật).");
    }

    bool ValidateRefs()
    {
        if (!bodyRigidbody || !nozzleRigidbody || !bodyJointAnchor || !nozzleJointAnchor || !hoseRootBone || !hoseTipBone)
        {
            Debug.LogError("[HoseAnchorRetarget] Thiếu reference trong Inspector.");
            return false;
        }
        return true;
    }
}
