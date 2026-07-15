using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

[DisallowMultipleComponent]
public class TrophyGrabController : MonoBehaviour
{
    private readonly HashSet<int> _hoveringPointers = new HashSet<int>();

    private GrabInteractable _grabInteractable;
    private Grabbable _grabbable;
    private Rigidbody _rigidbody;
    private MaterialOutlineHighlighter _highlighter;
    private Coroutine _disableSnapRoutine;
    private Coroutine _rearmRoutine;
    private float _releaseRearmDelay;
    private bool _initialized;
    private bool _snapArmed = true;

    public bool IsHeld => _grabbable != null && _grabbable.SelectingPointsCount > 0;
    public bool IsSnapArmed => _snapArmed;

    public void Initialize(
        GrabInteractable grabInteractable,
        Grabbable grabbable,
        Rigidbody rigidbody,
        MaterialOutlineHighlighter highlighter,
        float releaseRearmDelay)
    {
        if (_initialized)
        {
            _grabInteractable.WhenPointerEventRaised -= HandlePointerEvent;
        }

        _grabInteractable = grabInteractable;
        _grabbable = grabbable;
        _rigidbody = rigidbody;
        _highlighter = highlighter;
        _releaseRearmDelay = Mathf.Max(0f, releaseRearmDelay);
        _snapArmed = true;
        _grabInteractable.ResetGrabOnGrabsUpdated = true;
        _grabInteractable.WhenPointerEventRaised += HandlePointerEvent;
        _initialized = true;
        RefreshHighlight();
    }

    private void OnEnable()
    {
        if (_initialized && _grabInteractable != null)
        {
            _grabInteractable.WhenPointerEventRaised -= HandlePointerEvent;
            _grabInteractable.WhenPointerEventRaised += HandlePointerEvent;
            RefreshHighlight();
        }
    }

    private void OnDisable()
    {
        if (_initialized && _grabInteractable != null)
        {
            _grabInteractable.WhenPointerEventRaised -= HandlePointerEvent;
        }

        _hoveringPointers.Clear();
        _highlighter?.SetHighlighted(false);
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Hover:
                _hoveringPointers.Add(evt.Identifier);
                break;
            case PointerEventType.Unhover:
                _hoveringPointers.Remove(evt.Identifier);
                break;
            case PointerEventType.Select:
                HandleSelect();
                break;
            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                HandleRelease();
                break;
        }

        RefreshHighlight();
    }

    private void HandleSelect()
    {
        if (_rearmRoutine != null)
        {
            StopCoroutine(_rearmRoutine);
            _rearmRoutine = null;
        }

        _rigidbody.constraints = RigidbodyConstraints.None;
        _rigidbody.useGravity = true;

        if (_snapArmed)
        {
            _snapArmed = false;
            if (_disableSnapRoutine != null)
            {
                StopCoroutine(_disableSnapRoutine);
            }

            // Keep Meta's reset enabled through this selection event so the initial grab reaches the anchor.
            _disableSnapRoutine = StartCoroutine(DisableSnapAfterInitialGrab());
        }
    }

    private void HandleRelease()
    {
        if (_grabbable.SelectingPointsCount > 0)
        {
            return;
        }

        if (_rearmRoutine != null)
        {
            StopCoroutine(_rearmRoutine);
        }

        _rearmRoutine = StartCoroutine(RearmSnapAfterFullRelease());
    }

    private IEnumerator DisableSnapAfterInitialGrab()
    {
        yield return null;
        _grabInteractable.ResetGrabOnGrabsUpdated = false;
        _disableSnapRoutine = null;
    }

    private IEnumerator RearmSnapAfterFullRelease()
    {
        if (_releaseRearmDelay > 0f)
        {
            yield return new WaitForSeconds(_releaseRearmDelay);
        }

        if (_grabbable.SelectingPointsCount == 0)
        {
            _snapArmed = true;
            _grabInteractable.ResetGrabOnGrabsUpdated = true;
            RefreshHighlight();
        }

        _rearmRoutine = null;
    }

    private void RefreshHighlight()
    {
        if (_highlighter != null)
        {
            _highlighter.SetHighlighted(_snapArmed && !IsHeld && _hoveringPointers.Count > 0);
        }
    }
}
