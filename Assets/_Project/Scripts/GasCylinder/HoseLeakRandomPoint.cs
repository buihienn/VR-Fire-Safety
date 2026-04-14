using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoseLeakRandomPoint : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform hoseRoot;

    [Tooltip("Neu de trong thi se dung chinh object nay")]
    [SerializeField] private Transform fireNodeRoot;

    [Header("Segment Filter")]
    [SerializeField] private string segmentNameContains = "segment";
    [Min(0)] [SerializeField] private int skipFirstSegments = 5;
    [Min(0)] [SerializeField] private int skipLastSegments = 5;

    [Header("Placement")]
    [SerializeField] private Vector3 worldPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 worldRotationOffset = Vector3.zero;

    [Header("Timing")]
    [SerializeField] private bool randomizeOnEnable = true;
    [SerializeField] private bool waitOneFrameBeforeRandomize = true;

    [Header("Debug")]
    [SerializeField] private int foundSegmentCount;
    [SerializeField] private int usableSegmentCount;
    [SerializeField] private int chosenIndex = -1;
    [SerializeField] private string chosenSegmentName;

    private void Awake()
    {
        if (!fireNodeRoot)
            fireNodeRoot = transform;
    }

    private void OnEnable()
    {
        if (!randomizeOnEnable) return;

        if (waitOneFrameBeforeRandomize)
            StartCoroutine(RandomizeNextFrame());
        else
            RandomizeLeakPoint();
    }

    private IEnumerator RandomizeNextFrame()
    {
        yield return null;
        RandomizeLeakPoint();
    }

    [ContextMenu("Randomize Leak Point")]
    public void RandomizeLeakPoint()
    {
        if (!hoseRoot)
        {
            Debug.LogWarning("[HoseLeakRandomPoint] Missing hoseRoot.", this);
            return;
        }

        List<Transform> segments = GetEligibleSegments();
        foundSegmentCount = CountAllMatchingSegments();
        usableSegmentCount = segments.Count;

        if (segments.Count == 0)
        {
            chosenIndex = -1;
            chosenSegmentName = "";
            Debug.LogWarning("[HoseLeakRandomPoint] No usable hose segments found.", this);
            return;
        }

        chosenIndex = Random.Range(0, segments.Count);
        Transform chosenSegment = segments[chosenIndex];
        chosenSegmentName = chosenSegment.name;

        PlaceFireNode(chosenSegment);
    }

    private List<Transform> GetEligibleSegments()
    {
        List<Transform> allSegments = new List<Transform>();

        for (int i = 0; i < hoseRoot.childCount; i++)
        {
            Transform child = hoseRoot.GetChild(i);

            if (child == null) continue;
            if (!child.name.ToLower().Contains(segmentNameContains.ToLower())) continue;

            allSegments.Add(child);
        }

        List<Transform> usable = new List<Transform>();

        int start = Mathf.Clamp(skipFirstSegments, 0, allSegments.Count);
        int endExclusive = Mathf.Clamp(allSegments.Count - skipLastSegments, 0, allSegments.Count);

        for (int i = start; i < endExclusive; i++)
            usable.Add(allSegments[i]);

        return usable;
    }

    private int CountAllMatchingSegments()
    {
        int count = 0;

        for (int i = 0; i < hoseRoot.childCount; i++)
        {
            Transform child = hoseRoot.GetChild(i);
            if (child == null) continue;

            if (child.name.ToLower().Contains(segmentNameContains.ToLower()))
                count++;
        }

        return count;
    }

    private void PlaceFireNode(Transform chosenSegment)
    {
        if (!fireNodeRoot || !chosenSegment) return;

        fireNodeRoot.position = chosenSegment.position + worldPositionOffset;
        fireNodeRoot.rotation = chosenSegment.rotation * Quaternion.Euler(worldRotationOffset);
    }
}