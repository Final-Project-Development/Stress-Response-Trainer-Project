using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DSUKPhoneBox : MonoBehaviour {

	private Animator _animator;

	void Start() {
		_animator = GetComponent<Animator>();
		if (_animator == null)
			_animator = GetComponentInChildren<Animator>();
	}

	public bool IsOpen =>
		_animator != null && _animator.GetBool("isOpen");

	public void Open() {

		if (_animator != null) {
			_animator.SetBool ("isOpen", true);
		}

	}

	public void Close() {

		if (_animator != null) {
			_animator.SetBool ("isOpen", false);
		}

	}

	public void ToggleDoor() {

		if (_animator != null) {
			_animator.SetBool ("isOpen", !_animator.GetBool ("isOpen"));
		}

	}
}
