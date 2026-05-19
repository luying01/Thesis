using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class WeightSnap : MonoBehaviour
{
    public float snapDistance = 0.08f; // 吸附距离

    private XRGrabInteractable grabInteractable;
    private PulleyPhysics pulleyPhysics;
    private bool isSnapped = false;

    void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        pulleyPhysics = FindObjectOfType<PulleyPhysics>();

        // 监听放手事件
        grabInteractable.selectExited.AddListener(OnReleased);
        grabInteractable.selectEntered.AddListener(args => OnGrabbed());
    }

    void OnReleased(SelectExitEventArgs args)
    {
        if (isSnapped) return;

        // 检查是否靠近Hook_L
        TrySnapToHook(pulleyPhysics.hookLeft, true);
        // 检查是否靠近Hook_R
        TrySnapToHook(pulleyPhysics.hookRight, false);
        // 检查是否靠近其他砝码的底部挂点
        TrySnapToWeight();
    }

    void TrySnapToHook(Transform hook, bool isLeft)
    {
        if (hook == null) return;
        if (isSnapped) return;
        float dist = Vector3.Distance(transform.position, hook.position);
        if (dist < snapDistance)
        {
            transform.position = hook.position;
            transform.SetParent(null);
            if (isLeft)
                pulleyPhysics.weightChainLeft = this.gameObject;
            else
                pulleyPhysics.weightChainRight = this.gameObject;
            pulleyPhysics.velocity = 0f;
            isSnapped = true;
        }
    }

    void TrySnapToWeight()
    {
        WeightSnap[] allWeights = FindObjectsOfType<WeightSnap>();
        foreach (WeightSnap other in allWeights)
        {
            if (other == this) continue;
            Transform bottomPoint = other.transform.Find("weight_AttachPoint_Bottom");
            if (bottomPoint == null) continue;

            float dist = Vector3.Distance(transform.position, bottomPoint.position);
            if (dist < snapDistance)
            {
                // 挂到另一个砝码底部
                transform.SetParent(other.transform);
                transform.position = bottomPoint.position;
                isSnapped = true;
                return;
            }
        }
    }

    // 被抓起时取消吸附状态
    public void OnGrabbed()
    {
        if (!isSnapped) return;
        isSnapped = false;
        transform.SetParent(null);
        if (pulleyPhysics.weightChainLeft == this.gameObject)
            pulleyPhysics.weightChainLeft = null;
        if (pulleyPhysics.weightChainRight == this.gameObject)
            pulleyPhysics.weightChainRight = null;
    }
}