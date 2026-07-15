using UnityEngine;
using MetaFirstPersonLocomotor = Oculus.Interaction.Locomotion.FirstPersonLocomotor;
using MetaLocomotionEvent = Oculus.Interaction.Locomotion.LocomotionEvent;

public class MetaXRHeadBlocking : MonoBehaviour
{
    [Header("Root Rig To Push Back")]
    [SerializeField] public GameObject player = null;

    [Header("Environment Layers To Block")]
    [SerializeField] private LayerMask _collisionLayers = 1 << 0;

    [Header("Head Blocking")]
    [SerializeField] private float _collisionRadius = 0.2f;

    [Header("Body Blocking")]
    [SerializeField] private bool _useBodyBlocking = true;
    [SerializeField] private float _bodyRadius = 0.28f;
    [SerializeField] private float _bodyTopOffsetFromHead = 0.35f;
    [SerializeField] private float _bodyBottomOffsetFromHead = 1.15f;

    [Header("Ignore")]
    [SerializeField] private string _ignoreTag = "Player";
    [SerializeField] private int _maxOverlapHits = 16;

    private Vector3 _lastSafeHeadPos;
    private Collider[] _overlapResults;
    private float _suspendedUntilUnscaledTime;
    private MetaFirstPersonLocomotor[] _locomotors;
    private Coroutine _resetAfterLocomotionRoutine;
    private bool _subscribedToLocomotion;

    private void OnEnable()
    {
        SubscribeToLocomotionEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromLocomotionEvents();
    }

    private void Start()
    {
        _lastSafeHeadPos = transform.position;
        _overlapResults = new Collider[Mathf.Max(4, _maxOverlapHits)];
        SubscribeToLocomotionEvents();
    }

    public void ResetAfterTeleport(float resumeDelaySeconds = 0.25f)
    {
        _lastSafeHeadPos = transform.position;
        _suspendedUntilUnscaledTime = Time.unscaledTime + Mathf.Max(0f, resumeDelaySeconds);
    }

    private void SubscribeToLocomotionEvents()
    {
        if (_subscribedToLocomotion)
            return;

        Transform searchRoot = player != null ? player.transform : transform.root;
        if (searchRoot == null)
            return;

        _locomotors = searchRoot.GetComponentsInChildren<MetaFirstPersonLocomotor>(true);
        foreach (MetaFirstPersonLocomotor locomotor in _locomotors)
        {
            if (locomotor != null)
            {
                locomotor.WhenLocomotionEventHandled += HandleLocomotionEventHandled;
            }
        }

        _subscribedToLocomotion = _locomotors.Length > 0;
    }

    private void UnsubscribeFromLocomotionEvents()
    {
        if (!_subscribedToLocomotion || _locomotors == null)
            return;

        foreach (MetaFirstPersonLocomotor locomotor in _locomotors)
        {
            if (locomotor != null)
            {
                locomotor.WhenLocomotionEventHandled -= HandleLocomotionEventHandled;
            }
        }

        _locomotors = null;
        _subscribedToLocomotion = false;
    }

    private void HandleLocomotionEventHandled(MetaLocomotionEvent locomotionEvent, Pose delta)
    {
        if (locomotionEvent.Translation != MetaLocomotionEvent.TranslationType.Absolute
            && locomotionEvent.Translation != MetaLocomotionEvent.TranslationType.AbsoluteEyeLevel)
        {
            return;
        }

        if (_resetAfterLocomotionRoutine != null)
        {
            StopCoroutine(_resetAfterLocomotionRoutine);
        }

        _resetAfterLocomotionRoutine = StartCoroutine(ResetAfterLocomotionAtEndOfFrame());
    }

    private System.Collections.IEnumerator ResetAfterLocomotionAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        ResetAfterTeleport(0.1f);
        _resetAfterLocomotionRoutine = null;
    }

    private bool ShouldIgnore(Collider col)
    {
        if (col == null)
            return true;

        if (player != null && col.transform.IsChildOf(player.transform))
            return true;

        if (!string.IsNullOrEmpty(_ignoreTag) && col.CompareTag(_ignoreTag))
            return true;

        return false;
    }

    private bool DetectSphereHit(Vector3 center, float radius)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            _overlapResults,
            _collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            if (!ShouldIgnore(_overlapResults[i]))
                return true;
        }

        return false;
    }

    private bool DetectCapsuleHit(Vector3 pointA, Vector3 pointB, float radius)
    {
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            pointA,
            pointB,
            radius,
            _overlapResults,
            _collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            if (!ShouldIgnore(_overlapResults[i]))
                return true;
        }

        return false;
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 currentHeadPos = transform.position;

        if (Time.unscaledTime < _suspendedUntilUnscaledTime)
        {
            _lastSafeHeadPos = currentHeadPos;
            return;
        }

        bool headBlocked = DetectSphereHit(currentHeadPos, _collisionRadius);

        bool bodyBlocked = false;
        if (_useBodyBlocking)
        {
            Vector3 capsuleTop = currentHeadPos - Vector3.up * _bodyTopOffsetFromHead;
            Vector3 capsuleBottom = currentHeadPos - Vector3.up * _bodyBottomOffsetFromHead;

            bodyBlocked = DetectCapsuleHit(capsuleTop, capsuleBottom, _bodyRadius);
        }

        bool blocked = headBlocked || bodyBlocked;

        if (!blocked)
        {
            _lastSafeHeadPos = currentHeadPos;
            return;
        }

        Vector3 headDelta = currentHeadPos - _lastSafeHeadPos;
        headDelta.y = 0f;

        if (headDelta.sqrMagnitude < 0.000001f)
            return;

        player.transform.position -= headDelta;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _collisionRadius);

        if (_useBodyBlocking)
        {
            Vector3 top = transform.position - Vector3.up * _bodyTopOffsetFromHead;
            Vector3 bottom = transform.position - Vector3.up * _bodyBottomOffsetFromHead;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(top, _bodyRadius);
            Gizmos.DrawWireSphere(bottom, _bodyRadius);
            Gizmos.DrawLine(top + Vector3.right * _bodyRadius, bottom + Vector3.right * _bodyRadius);
            Gizmos.DrawLine(top - Vector3.right * _bodyRadius, bottom - Vector3.right * _bodyRadius);
            Gizmos.DrawLine(top + Vector3.forward * _bodyRadius, bottom + Vector3.forward * _bodyRadius);
            Gizmos.DrawLine(top - Vector3.forward * _bodyRadius, bottom - Vector3.forward * _bodyRadius);
        }
    }
#endif
}
